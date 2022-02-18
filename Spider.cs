using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Html.Parser;
using ATS.Common.Poco;
using Knapcode.TorSharp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ATS.DarkSearch;

public class Spider : IDisposable
{
    private readonly Microsoft.Extensions.Configuration.IConfiguration _config;
    private readonly ILogger<Spider> _logger;
    private readonly IWebHostEnvironment _hostEnvironment;

    private TorSharpProxy _proxy;
    private TorSharpSettings _settings;
    private HttpClient _httpClient;

    public Spider(Microsoft.Extensions.Configuration.IConfiguration config, 
        ILogger<Spider> logger,
        IWebHostEnvironment hostEnvironment)
    {
        _config = config;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
    }
    
    public async Task StartAsync()
    {
        if (_proxy != null)
            return;
        
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
            await new TorSharpToolFetcher(_settings, new HttpClient()).FetchAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw;
        }

        _proxy = new TorSharpProxy(_settings);

        var handler = new HttpClientHandler
        {
            Proxy = new WebProxy(new Uri("http://localhost:" + _settings.PrivoxySettings.Port))
        };
        
        _httpClient = new HttpClient(handler);
        
        await _proxy.ConfigureAndStartAsync();
    }

    public void Stop()
    {
        if (_proxy == null)
            return;

        _httpClient?.Dispose();
        _httpClient = null;
        
        _proxy.Stop();
        _proxy.Dispose();
        _proxy = null;
    }

    public async Task<PingResultPoco> ExecuteAsync(string link)
    {
        if (_proxy == null || _httpClient == null)
            return null;
        
        try
        {
            await _proxy.GetNewIdentityAsync();

            var headRequest = new HttpRequestMessage(HttpMethod.Head, link);
            headRequest.Options.Set(new HttpRequestOptionsKey<TimeSpan>("RequestTimeout"),
                TimeSpan.FromSeconds(10));

            var ping = new PingResultPoco()
            {
                Url = link,
                DateCreated = DateTimeOffset.UtcNow
            };

            var headResponse = await _httpClient.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead);

            ping.StatusCode = headResponse.StatusCode;

            // get title
            if ((int) ping.StatusCode < 300)
            {
                var html = await _httpClient.GetStringAsync(link);
                ping.Title = GetHtmlTitle(html);
                ping.IsLive = true;
            }

            return ping;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
        }

        return null;
    }
    
    private string GetHtmlTitle(string html)
    {
        try
        {
            var config = Configuration.Default.WithDefaultLoader();
            using (var context = BrowsingContext.New(config))
            {
                var parser = context.GetService<IHtmlParser>();
                var document = parser.ParseDocument(html);

                var title = document.All.FirstOrDefault(m => m.LocalName == "title");
                return title?.TextContent ?? "";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            return "";
        }
    }

    public void Dispose()
    {
        Stop();
    }
}