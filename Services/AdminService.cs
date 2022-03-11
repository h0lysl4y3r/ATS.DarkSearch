using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ATS.Common.Extensions;
using ATS.Common.ServiceStack;
using ATS.DarkSearch.Model;
using ATS.DarkSearch.Workers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using ServiceStack;
using ServiceStack.Messaging;
using ServiceStack.RabbitMq;
using RabbitMqWorker = ATS.DarkSearch.Workers.RabbitMqWorker;

namespace ATS.DarkSearch.Services;

public class AdminService : Service
{
    public object Get(GetAllUrls request)
    {
        var repo = HostContext.AppHost.Resolve<PingsRepository>();
        var urls = repo.GetUrls();
        return new GetAllUrlsResponse()
        {
            Urls = urls
        };
    }
    
    public object Get(GetPing request)
    {
        if (request.Url.IsNullOrEmpty())
            throw HttpError.BadRequest(nameof(request.Url));

        var repo = HostContext.AppHost.Resolve<PingsRepository>();
        var ping = repo.Get(request.Url);

        return new GetPingResponse()
        {
            Ping = ping
        };
    }

    public object Delete(DeletePing request)
    {
        if (request.Url.IsNullOrEmpty())
            throw HttpError.BadRequest(nameof(request.Url));

        var repo = HostContext.AppHost.Resolve<PingsRepository>();
        if (!repo.Delete(request.Url))
            throw HttpError.ServiceUnavailable("ServiceUnavailable");
        
        return new HttpResult();
    }

    public object Delete(DeleteAllPings request)
    {
        var repo = HostContext.AppHost.Resolve<PingsRepository>();
        var urls = repo.GetUrls();
        foreach (var url in urls)
        {
            if (!repo.Delete(url))
                throw HttpError.ServiceUnavailable(url);
        }
        
        return new HttpResult();
    }

    public async Task<object> Put(RestartSpider request)
    {
        var spider = this.Resolve<Spider>();
        spider.Stop();
        await spider.StartAsync();
        return new HttpResult();
    }
    
    public object Delete(PurgeQueues request)
    {
        if (request.TypeFullName.IsNullOrEmpty())
            throw HttpError.BadRequest(nameof(request.TypeFullName));

        var type = Type.GetType(request.TypeFullName);
        if (type == null)
            throw HttpError.NotFound(nameof(request.TypeFullName));
        
        var mqServer = HostContext.AppHost.Resolve<IMessageService>() as ATSRabbitMqServer;
        var queueNames = new QueueNames(type);
        mqServer.PurgeQueues(queueNames.In, queueNames.Priority, queueNames.Out, queueNames.Dlq);
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
            .Where(x => x.Trim().Length > 0
                && !x.Trim().StartsWith("#"))
            .ToArray();
        if (links.Length == 0)
            throw HttpError.ExpectationFailed(nameof(request.LinkFileName));
        
        var mqServer = HostContext.AppHost.Resolve<IMessageService>();
        using var mqClient = mqServer.CreateMessageQueueClient();

        foreach (var link in links)
        {
            Log.Debug("Scheduling ping of " + link);
            
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

    public object Post(RepublishTryNewPing request)
    {
        Republish<TryNewPing>(request.Count);
        return new HttpResult();
    }

    public object Post(RepublishUpdatePing request)
    {
        Republish<UpdatePing>(request.Count, true);
        return new HttpResult();
    }

    private void Republish<T>(int count, bool delayed = false)
    {
        var mqServer = HostContext.AppHost.Resolve<IMessageService>();
        using var mqClient = mqServer.CreateMessageQueueClient() as RabbitMqQueueClient;

        count = Math.Clamp(count, 1, 100);
        for (int i = 0; i < count; i++)
        {
            IMessage<T> dlqMsg = mqClient.Get<T>(QueueNames<T>.Dlq);
            if (dlqMsg == null)
                break;
            
            mqClient.Ack(dlqMsg);
            if (delayed)
            {
                var config = Request.Resolve<IConfiguration>();
                var message = MessageFactory.Create(dlqMsg.GetBody());
                message.Meta = new Dictionary<string, string>()
                {
                    {"x-delay", config.GetValue<string>("AppSettings:RefreshPingIntervalMs")}
                };
                mqClient.PublishDelayed(message, RabbitMqWorker.DelayedMessagesExchange);
            }
            else
            {
                mqClient.Publish(dlqMsg.GetBody());
            }
        }
    }
}