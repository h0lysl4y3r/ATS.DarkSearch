using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ATS.DarkSearch.Model;
using ATS.DarkSearch.Workers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using ServiceStack;
using ServiceStack.Messaging;

namespace ATS.DarkSearch.Services;

public class AdminService : Service
{
    public ILoggerFactory LoggerFactory { get; set; }
    private ILogger _logger;
    public ILogger Logger => 
        _logger ?? (_logger = LoggerFactory.CreateLogger(typeof(AdminService)));

    public object Get(GetAllUrls request)
    {
        var repo = HostContext.AppHost.Resolve<PingsRepository>();
        var urls = repo.GetUrls();
        return new GetAllUrlsResponse()
        {
            Urls = urls
        };
    }
    
    public async Task<object> Post(RestartSpider request)
    {
        var spider = this.Resolve<Spider>();
        spider.Stop();
        await spider.StartAsync();
        return new HttpResult();
    }

    public object Post(PingAll request)
    {
        if (request.LinkFileName.IsNullOrEmpty())
            throw HttpError.BadRequest(nameof(request.LinkFileName));

        var hostEnvironment = TryResolve<IWebHostEnvironment>();
        var filePath = Path.Combine(hostEnvironment.ContentRootPath, "Data", request.LinkFileName);
        if (!File.Exists(filePath))
            throw HttpError.NotFound(nameof(request.LinkFileName));
            
        var links = File.ReadAllLines(filePath)
            .Where(x => x.Length > 0)
            .ToArray();
        if (links.Length == 0)
            throw HttpError.ExpectationFailed(nameof(request.LinkFileName));
        
        var mqServer = HostContext.AppHost.Resolve<IMessageService>();
        using var mqClient = mqServer.CreateMessageQueueClient();

        foreach (var link in links)
        {
            Logger.LogDebug("Scheduling ping of " + link);
            
            mqClient.Publish(new Ping()
            {
                Url = link
            });
        }

        return new HttpResult();
    }
    
    public object Post(RepublishPings request)
    {
        Republish<Ping>(request.Count);
        return new HttpResult();
    }
    
    public object Post(RepublishPingsStore request)
    {
        Republish<StorePing>(request.Count);
        return new HttpResult();
    }

    private void Republish<T>(int count)
    {
        var mqServer = HostContext.AppHost.Resolve<IMessageService>();
        using var mqClient = mqServer.CreateMessageQueueClient();

        count = Math.Clamp(count, 1, 100);
        for (int i = 0; i < count; i++)
        {
            IMessage<T> dlqMsg = mqClient.Get<T>(QueueNames<T>.Dlq);
            if (dlqMsg == null)
                break;
            
            mqClient.Ack(dlqMsg);
            mqClient.Publish(dlqMsg.GetBody());
        }
    }
}