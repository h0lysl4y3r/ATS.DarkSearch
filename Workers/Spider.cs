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

            var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
            headRequest.Options.Set(new HttpRequestOptionsKey<TimeSpan>("RequestTimeout"),
                TimeSpan.FromSeconds(10));

            var utcNow = DateTimeOffset.UtcNow;
            var ping = new PingResultPoco()
            {
                Url = url,
                Date = utcNow,
                LastModified = utcNow,
                Domain = new Uri(url).DnsSafeHost
            };
            var links = new List<string>();

            var headResponse = await _httpClient.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead);

            Log.Debug($"{nameof(Spider)} with response " + headResponse.StatusCode);

            ping.StatusCode = headResponse.StatusCode;
            var statusCode = (int) ping.StatusCode;

            // get title
            if (statusCode < 300)
            {
                var html = await _httpClient.GetStringAsync(url);
                var textContent = GetTextContent(html);
                ping.Title = textContent.Title;
                ping.Description = textContent.Description;
                ping.Texts = textContent.Texts;
                ping.IsLive = true;

                if (textContent.Links.Length > 0)
                    links = textContent.Links.ToList();
            }
            
            // location?
            if (statusCode >= 300 && statusCode < 400
                && headResponse.Headers.TryGetValues(HeaderNames.Location, out var locations))
            {
                foreach (var item in locations)
                {
                    if (!Uri.IsWellFormedUriString(item, UriKind.Absolute))
                        continue;
                    
                    links.Add(item);
                }
                ping.IsLive = true;
            }
            
            SanitizeLinks(links);

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

    public void Dispose()
    {
        Stop();
    }

    private Uri GetUriSafe(string url)
    {
        if (url == null)
            return null;
        
        try
        {
            return new Uri(url);
        }
        catch
        {
            return null;
        }
    }
    
    private string SanitizeUrlSchema(string url, string schema = "http")
    {
        if (url == null)
            throw new ArgumentNullException(nameof(url));
        if (schema != "http" && schema != "https")
            throw new ArgumentException(nameof(schema));

        return url.ToLower().StartsWith("http") ? url : $"{schema}://{url}";
    }
    
    private string StripUrlQuery(string url)
    {
        if (url == null)
            throw new ArgumentNullException(nameof(url));

        try
        {
            var uri = new Uri(SanitizeUrlSchema(url));
            return $"{uri.Scheme}://{uri.DnsSafeHost}{uri.AbsolutePath}";
        }
        catch
        {
            return url;
        }
    }

    private bool HasFirstLevelDomain(Uri uri, string domain)
    {
        if (uri == null)
            throw new ArgumentNullException(nameof(uri));
        if (domain == null)
            throw new ArgumentNullException(nameof(domain));

        if (uri.DnsSafeHost.Length == 0)
            return false;

        var dotIndex = uri.DnsSafeHost.LastIndexOf('.');
        if (dotIndex < 0 || dotIndex == uri.DnsSafeHost.Length - 1)
            return uri.DnsSafeHost.Equals(domain, StringComparison.InvariantCultureIgnoreCase);

        var firstLevelDomain = uri.DnsSafeHost.Substring(dotIndex + 1);

        return firstLevelDomain.Equals(domain, StringComparison.InvariantCultureIgnoreCase);
    }

    private List<string> SanitizeLinks(List<string> links)
    {
        for (int i = 0; i < links.Count; i++)
        {
            var uri = GetUriSafe(SanitizeUrlSchema(links[i]));
            if (uri == null
                || uri.IsFile
                || !HasFirstLevelDomain(uri, "onion"))
            {
                links.RemoveAt(i);
                i--;
                continue;
            }

            links[i] = StripUrlQuery(links[i]);
        }

        return links.GroupBy(x => x)
            .Select(x => x.Key)
            .ToList();
    }
}