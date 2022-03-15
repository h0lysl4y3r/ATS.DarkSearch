using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using ATS.Common.Extensions;
using ATS.Common.Helpers;
using ATS.Common.Poco;
using Knapcode.TorSharp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;
using Serilog;

namespace ATS.DarkSearch.Workers;

public class Spider : IDisposable
{
    public class TextContent
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string[] Texts { get; set; }
        public string[] Links { get; set; }
    }
    
    private readonly Microsoft.Extensions.Configuration.IConfiguration _config;
    private readonly IWebHostEnvironment _hostEnvironment;

    private TorSharpProxy _proxy;
    private TorSharpSettings _settings;
    private HttpClient _httpClient;

    public Spider(Microsoft.Extensions.Configuration.IConfiguration config, 
        IWebHostEnvironment hostEnvironment)
    {
        _config = config;
        _hostEnvironment = hostEnvironment;
    }
    
    public async Task StartAsync()
    {
        if (_proxy != null)
            return;
        
        Log.Information($"Starting {nameof(Spider)}...");

        // configure
        _settings = new TorSharpSettings
        {
            ZippedToolsDirectory = Path.Combine(_hostEnvironment.ContentRootPath, "Tor", "TorZipped"),
            ExtractedToolsDirectory = Path.Combine(_hostEnvironment.ContentRootPath, "Tor", "TorExtracted"),
            PrivoxySettings = { Port = _config.GetValue<int>("Tor:PrivoxyPort") },
            TorSettings =
            {
                SocksPort = _config.GetValue<int>("Tor:SocksPort"),
                AdditionalSockPorts = _config.GetValue<List<int>>("Tor:AdditionalSockPorts"),
                ControlPort = _config.GetValue<int>("Tor:ControlPort"),
                ControlPassword = _config.GetValue<string>("Tor:ControlPassword"),
            },
        };
        
        // download tools
        try
        {
            Log.Debug($"Fetching tor...");
            await new TorSharpToolFetcher(_settings, new HttpClient()).FetchAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, ex.Message);
            throw;
        }

        _proxy = new TorSharpProxy(_settings);

        var handler = new HttpClientHandler
        {
            Proxy = new WebProxy(new Uri("http://localhost:" + _settings.PrivoxySettings.Port))
        };
        
        _httpClient = new HttpClient(handler);
        
        Log.Debug($"Starting proxy...");
        await _proxy.ConfigureAndStartAsync();

        Log.Information($"{nameof(Spider)} started");
    }

    public void Stop()
    {
        if (_proxy == null)
            return;

        Log.Information($"Stopping {nameof(Spider)}...");

        _httpClient?.Dispose();
        _httpClient = null;
        
        _proxy.Stop();
        _proxy.Dispose();
        _proxy = null;

        Log.Information($"{nameof(Spider)} stopped");
    }

    public async Task<PingResultPoco> ExecuteAsync(string url)
    {
        if (_proxy == null || _httpClient == null)
            return null;

        Log.Debug($"{nameof(Spider)} requesting " + url);

        try
        {
            await _proxy.GetNewIdentityAsync();

            // GET request url with 30s timeout 
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Options.Set(new HttpRequestOptionsKey<TimeSpan>("RequestTimeout"),
                TimeSpan.FromSeconds(30));
            
            var response = await _httpClient.SendAsync(request);
            var reason = response.ReasonPhrase ?? "N/A";
            Log.Debug($"{nameof(Spider)} with response {response.StatusCode} ({reason})");

            // document must be of text/html content-type
            var contentType = response.GetHeaderValueSafe(HeaderNames.ContentType);
            if (contentType == null
                || !contentType.Contains("text/html"))
            {
                return null;
            }

            // create ping result
            var utcNow = DateTimeOffset.UtcNow;
            var ping = new PingResultPoco()
            {
                Url = url,
                Date = utcNow,
                LastModified = utcNow,
                Domain = new Uri(url).DnsSafeHost
            };
            var links = new List<string>();
            
            ping.StatusCode = response.StatusCode;
            var statusCode = (int) ping.StatusCode;
            
            // get content if 2xx result code
            if (statusCode < 300)
            {
                var html = await response.Content.ReadAsStringAsync();
                var textContent = GetTextContent(html);
                ping.Title = CollapseWhitespace(textContent.Title);
                ping.Description = CollapseWhitespace(textContent.Description);
                ping.Texts = textContent.Texts
                    .Select(CollapseWhitespace)
                    .ToArray();
                ping.IsLive = true;

                if (textContent.Links.Length > 0)
                    links = textContent.Links.ToList();
            }
            
            // get location if 3xx result code
            if (statusCode >= 300 && statusCode < 400
                                  && response.Headers.TryGetValues(HeaderNames.Location, out var locations))
            {
                foreach (var item in locations)
                {
                    if (!Uri.IsWellFormedUriString(item, UriKind.Absolute))
                        continue;
                    
                    links.Add(item);
                }
                ping.IsLive = true;
            }

            // sanitize links
            // 1) top level domain must be .onion
            // 2) query and fragment is stripped
            links = SanitizeLinks(links, url);

            if (links.Count > 0)
                ping.Links = links.ToArray();
            
            return ping;
        }
        catch (Exception ex)
        {
            Log.Error(ex, ex.Message);
            throw;
        }
    }
    
    private TextContent GetTextContent(string html)
    {
        try
        {
            var config = Configuration.Default.WithDefaultLoader();
            using (var context = BrowsingContext.New(config))
            {
                var parser = context.GetService<IHtmlParser>();
                var document = parser.ParseDocument(html);

                var textContent = new TextContent();

                var title = document.All.FirstOrDefault(m => m.LocalName == "title");
                textContent.Title = title?.TextContent ?? "";
            
                var description = document.All.FirstOrDefault(m => m.LocalName == "description");
                textContent.Description = description?.TextContent ?? "";

                if (document.Body != null)
                {
                    textContent.Texts = document.Body.Children
                        .Select(x => x.TextContent)
                        .ToArray();
                }
                
                textContent.Links = document
                    .Links
                    .OfType<IHtmlAnchorElement>()
                    .Where(x => x.Href != null
                                && (Uri.IsWellFormedUriString(x.Href, UriKind.Relative)
                                    || Uri.IsWellFormedUriString(x.Href, UriKind.Absolute)))
                    .Select(x => x.Href)
                    .ToArray();
                
                return textContent;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, ex.Message);
            return null;
        }
    }

    private string CollapseWhitespace(string text)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));
        if (text.Length == 0)
            return text;

        var result = text;
        while (true)
        {
            var length = result.Length;
            result = result.Replace("  ", " ");
            if (result.Length == length)
                break;
        }

        return result;
    }

    public void Dispose()
    {
        Stop();
    }
    
    private List<string> SanitizeLinks(List<string> links, string originalUrl)
    {
        var originalUri = new Uri(originalUrl);
        var result = new List<string>();
        
        for (int i = 0; i < links.Count; i++)
        {
            Uri uri = null;
            if (links[i].StartsWith("/"))
                uri = UriHelpers.GetUriSafe($"{originalUri.Scheme}://{originalUri.DnsSafeHost}{links[i]}");
            else
                uri = UriHelpers.GetUriSafe(UriHelpers.SanitizeUrlScheme(links[i]));
            
            if (uri == null
                || !uri.TopLevelDomain().Equals("onion", StringComparison.InvariantCultureIgnoreCase))
            {
                continue;
            }

            var strippedLink = UriHelpers.StripUrlQuery(links[i], false);
            if (strippedLink.Equals(originalUrl, StringComparison.InvariantCultureIgnoreCase))
            {
                continue;
            }

            result.Add(strippedLink);
        }

        return result.GroupBy(x => x)
            .Select(x => x.Key)
            .ToList();
    }
}