using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using ATS.Common.Extensions;
using ATS.Common.Helpers;
using ATS.Common.Model.DarkSearch;
using ATS.Common.Poco;
using ATS.Common.Tor;
using ATS.DarkSearch.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;
using Serilog;
using ServiceStack;
using ServiceStack.Messaging;
using ServiceStack.RabbitMq;

namespace ATS.DarkSearch.Workers;

public class Spider : TorClient
{
    public class TextContent
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string[] Texts { get; set; }
        public string[] Links { get; set; }
    }

    public bool IsPaused { get; set; }
    
    public Spider(Microsoft.Extensions.Configuration.IConfiguration config, 
        IWebHostEnvironment hostEnvironment)
        : base(config, hostEnvironment)
    {
    }
    
    public async Task<PingResultPoco> ExecuteAsync(string url)
    {
        if (url == null)
            throw new ArgumentNullException(nameof(url));

        // GET request url with 30s timeout 
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Options.Set(new HttpRequestOptionsKey<TimeSpan>("RequestTimeout"),
            TimeSpan.FromSeconds(30));

        var response = await SendAsync(request, CancellationToken.None);
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
    
    public async Task<PingResultPoco> Ping(string url, bool publishUpdate)
    {
        if (IsPaused)
        {
            Log.Warning($"{nameof(PingService)}:{nameof(Ping)} paused but calling ping on " + url);
            return null;
        }
        
        if (url.IsNullOrEmpty())
            throw HttpError.BadRequest(nameof(url));

        var mqServer = HostContext.AppHost.Resolve<IMessageService>();
        using var mqClient = mqServer.CreateMessageQueueClient() as RabbitMqQueueClient;

        // Ping
        PingResultPoco ping = null;
        try
        {
            await StartAsync(CancellationToken.None);
            ping = await ExecuteAsync(url);
        }
        catch (Exception ex)
        {
            Log.Debug(ex.Message);

            if (publishUpdate)
            {
                PublishPingUpdate(mqClient, url);
                return null;
            }
            
            // if we don't ping update, let's just re-throw
            throw;
        }

        // ping will be null if we tried to read a non-html content
        if (ping == null)
        {
            var message = $"{nameof(PingService)}:{nameof(Ping)} no ping result on " + url;
            Log.Error(message);

            throw new Exception(message);
        }
		
        // Schedule elastic store
        var accessKey = _config.GetValue<string>("AppSettings:AccessKey");

        Log.Debug($"{nameof(PingService)}:{nameof(Ping)} Scheduling store of " + url);
        mqClient.Publish(new StorePing()
        {
            Ping = ping,
            AccessKey = accessKey
        });

        // Try schedule new pings for links
        if (ping.Links != null && ping.Links.Length > 0)
        {
            for (int i = 0; i < ping.Links.Length; i++)
            {
                var link = ping.Links[i];
                Log.Debug($"{nameof(PingService)}:{nameof(Ping)} Scheduling new ping for " + link);
                mqClient.Publish(new Ping()
                {
                    Url = link,
                    AccessKey = accessKey
                });
            }
        }
		
        // update ping
        if (publishUpdate)
            PublishPingUpdate(mqClient, ping.Url);

        return ping;
    }

    private void PublishPingUpdate(RabbitMqQueueClient mqClient, string url)
    {
        var accessKey = _config.GetValue<string>("AppSettings:AccessKey");
        mqClient.PublishDelayed(new Message<UpdatePing>(
            new UpdatePing()
            {
                Url = url,
                AccessKey = accessKey
            })
        {
            Meta = new Dictionary<string, string>() { { "x-delay", _config.GetValue<string>("AppSettings:RefreshPingIntervalMs") } }
        }, RabbitMqWorker.DelayedMessagesExchange);
    }
}