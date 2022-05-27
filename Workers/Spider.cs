using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
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
using ServiceStack.Redis;

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
    public List<string> Blacklist { get; private set; }
    public Dictionary<string, int> PingMap { get; private set; } = new Dictionary<string, int>();
    
    public Spider(Microsoft.Extensions.Configuration.IConfiguration config, 
        IWebHostEnvironment hostEnvironment)
        : base(config, hostEnvironment)
    {
        Blacklist = config.GetSection("AppSettings:Blacklist").Get<List<string>>();
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken);
        
        var clientsManager = HostContext.AppHost.Resolve<IRedisClientsManager>();
        using var redis = clientsManager.GetClient();
        var hostEnvironment = HostContext.AppHost.Resolve<IWebHostEnvironment>();
        
        var cacheKey = $"ATS.DarkSearch:{hostEnvironment.EnvironmentName}:{nameof(Spider)}:Blacklist";
        var blacklist = redis.Get<List<string>>(cacheKey);
        if (!blacklist.IsNullOrEmpty())
            Blacklist.AddRange(blacklist);
        
        cacheKey = $"ATS.DarkSearch:{hostEnvironment.EnvironmentName}:{nameof(Spider)}:PingMap";
        var pingMap = redis.Get<Dictionary<string, int>>(cacheKey);
        PingMap = pingMap ?? new Dictionary<string, int>();
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
        if (!contentType.Contains("text/html"))
            return null;

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
        if (statusCode < 300 || statusCode == 304 /* Not modified */)
        {
            var html = await response.Content.ReadAsStringAsync();
            var textContent = GetTextContent(html);
            if (textContent != null)
            {
                ping.Title = CollapseWhitespace(textContent.Title);
                ping.Description = CollapseWhitespace(textContent.Description);
                ping.Texts = textContent.Texts
                    .Select(CollapseWhitespace)
                    .ToArray();
                ping.IsLive = true;

                if (textContent.Links.Length > 0)
                    links = textContent.Links.ToList();
            }
            else
            {
                Log.Warning($"{nameof(Spider)}: No HTML content for " + url);
            }
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
        if (string.IsNullOrEmpty(url))
            throw HttpError.BadRequest(nameof(url));

        var mqServer = HostContext.AppHost.Resolve<IMessageService>();
        using var mqClient = mqServer.CreateMessageQueueClient() as RabbitMqQueueClient;

        var pingStats = HostContext.AppHost.Resolve<PingStats>();

        if (IsPaused)
        {
            Log.Warning($"{nameof(PingService)}:{nameof(Ping)} paused but calling ping on " + url);
            if (publishUpdate)
                PublishPingUpdate(mqClient, url);
            
            pingStats.Update(url, PingStats.PingState.Paused);
            return null;
        }
        
        Log.Information($"{nameof(PingService)}:{nameof(Ping)} pinging {url}");

        var domain = UriHelpers.GetUriSafe(url).DnsSafeHost;
        if (Blacklist.Contains(domain))
        {
            Log.Warning($"{nameof(PingService)}:{nameof(Ping)} {url} is blacklisted");
            pingStats.Update(url, PingStats.PingState.Blacklisted);
            return null;
        }

        if (ThrottlePing(domain))
        {
            Log.Warning($"{nameof(PingService)}:{nameof(Ping)} {url} is throttled");
            pingStats.Update(url, PingStats.PingState.Throttled);
            return null;
        }

        pingStats.Update(url, PingStats.PingState.Ok);

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

    public void PublishPingUpdate(RabbitMqQueueClient mqClient, string url)
    {
        Log.Debug($"{nameof(PingService)}:{nameof(PublishPingUpdate)} Publishing update of " + url);
        
        var delayStr = _config.GetValue<string>("AppSettings:RefreshPingIntervalMs");
        var delay = long.Parse(delayStr);
        delay = delay + RandomNumberGenerator.GetInt32(0, 86400000); // plus random 0-1 day
        
        var accessKey = _config.GetValue<string>("AppSettings:AccessKey");
        mqClient.PublishDelayed(new Message<UpdatePing>(
            new UpdatePing()
            {
                Url = url,
                AccessKey = accessKey
            })
        {
            Meta = new Dictionary<string, string>() { { "x-delay", delay.ToString() } }
        }, RabbitMqWorker.DelayedMessagesExchange);
    }

    public bool AddOrRemoveToBlacklist(string domain, bool add)
    {
        if (domain == null)
            throw new ArgumentNullException(nameof(domain));

        var url = UriHelpers.SanitizeUrlScheme(domain);
        if (UriHelpers.GetUriSafe(url) == null)
            throw new ArgumentException(nameof(domain));

        var clientsManager = HostContext.AppHost.Resolve<IRedisClientsManager>();
        using var redis = clientsManager.GetClient();
        var hostEnvironment = HostContext.AppHost.Resolve<IWebHostEnvironment>();
        var cacheKey = $"ATS.DarkSearch:{hostEnvironment.EnvironmentName}:{nameof(Spider)}:Blacklist";

        if (add)
            Blacklist.Add(domain);
        else
            Blacklist.Remove(domain);
        
        return redis.Set(cacheKey, Blacklist);
    }

    private bool ThrottlePing(string domain)
    {
        if (domain == null)
            throw new ArgumentNullException(nameof(domain));

        if (!PingMap.ContainsKey(domain)) PingMap[domain] = 0;
        PingMap[domain]++;

        if (PingMap[domain] % 10 == 0)
        {
            var clientsManager = HostContext.AppHost.Resolve<IRedisClientsManager>();
            using var redis = clientsManager.GetClient();

            var cacheKey = $"ATS.DarkSearch:{_hostEnvironment.EnvironmentName}:{nameof(Spider)}:PingMap";
            redis.Set(cacheKey, PingMap);
        }

        return PingMap[domain] > 1000;
    }
}