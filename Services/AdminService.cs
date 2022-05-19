using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ATS.Common.Auth;
using ATS.Common.Extensions;
using ATS.Common.Model.DarkSearch;
using ATS.Common.ServiceStack;
using ATS.DarkSearch.Workers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;
using ServiceStack;
using ServiceStack.Messaging;
using ServiceStack.RabbitMq;
using RabbitMqWorker = ATS.DarkSearch.Workers.RabbitMqWorker;

namespace ATS.DarkSearch.Services;

public class AdminService : Service
{
    [RequiresAccessKey]
    public object Get(GetAllUrls request)
    {
        var repo = HostContext.AppHost.Resolve<PingsRepository>();
        var urls = repo.GetUrls(request.InputScrollId, out var outputScrollId, request.MaxResults);
        return new GetAllUrlsResponse()
        {
            OutputScrollId = outputScrollId,
            Urls = urls
        };
    }
    
    [RequiresAccessKey]
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

    [RequiresAccessKey]
    public object Delete(DeletePing request)
    {
        if (request.Url.IsNullOrEmpty())
            throw HttpError.BadRequest(nameof(request.Url));

        var repo = HostContext.AppHost.Resolve<PingsRepository>();
        if (!repo.Delete(request.Url))
            throw HttpError.ServiceUnavailable("ServiceUnavailable");
        
        return new HttpResult();
    }

    [RequiresAccessKey]
    public object Delete(DeleteAllPings request)
    {
        var config = Request.Resolve<IConfiguration>();
        if (request.AccessKey != config.GetValue<string>("AppSettings:AccessKey"))
            throw HttpError.Forbidden(nameof(request.AccessKey));
        
        var repo = HostContext.AppHost.Resolve<PingsRepository>();

        string inputScrollId = null;
        while (true)
        {
            var urls = repo.GetUrls(inputScrollId, out var outputScrollId);
            if (urls.Length == 0)
                break;

            inputScrollId = outputScrollId;
            foreach (var url in urls)
            {
                if (!repo.Delete(url))
                    throw HttpError.ServiceUnavailable(url);
            }
        }
        
        return new HttpResult();
    }

    [RequiresAccessKey]
    public async Task<object> Put(RestartSpider request)
    {
        var spider = this.Resolve<Spider>();
        spider.Stop();
        await spider.StartAsync(CancellationToken.None);
        return new HttpResult();
    }

    [RequiresAccessKey]
    public object Put(PauseSpider request)
    {
        var spider = this.Resolve<Spider>();
        spider.IsPaused = request.IsPaused;
        return new HttpResult();
    }

    [RequiresAccessKey]
    public object Get(GetSpiderState request)
    {
        var spider = this.Resolve<Spider>();
        return new GetSpiderStateResponse()
        {
            IsPaused = spider.IsPaused
        };
    }

    [RequiresAccessKey]
    public object Delete(PurgeQueues request)
    {
        if (request.TypeFullName.IsNullOrEmpty())
            throw HttpError.BadRequest(nameof(request.TypeFullName));

        var type = FindTypeInAllAssembliesByFullName(request.TypeFullName);
        if (type == null)
            throw HttpError.NotFound(nameof(request.TypeFullName));
        
        var mqServer = HostContext.AppHost.Resolve<IMessageService>() as ATSRabbitMqServer;
        var queueNames = new QueueNames(type);
        mqServer.PurgeQueues(queueNames.In, queueNames.Priority, queueNames.Out, queueNames.Dlq);
        return new HttpResult();
    }

    [RequiresAccessKey]
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

        var repo = HostContext.AppHost.Resolve<PingsRepository>();
        var mqServer = HostContext.AppHost.Resolve<IMessageService>();
        using var mqClient = mqServer.CreateMessageQueueClient();

        var config = Request.Resolve<IConfiguration>();
        foreach (var link in links)
        {
            var existingPing = repo.Get(link);
            if (existingPing != null)
            {
                Log.Debug($"{nameof(AdminService)}:{nameof(PingAll)} Skipping, ping of " + link + " exists");
                continue;
            }

            Log.Debug($"{nameof(AdminService)}:{nameof(PingAll)} Scheduling ping of " + link);
            mqClient.Publish(new Ping()
            {
                Url = link,
                AccessKey = config.GetValue<string>("AppSettings:AccessKey")
            });
        }

        return new HttpResult();
    }

    [RequiresAccessKey]
    public async Task<object> Post(PingSingle request)
    {
        if (request.Url.IsNullOrEmpty())
            throw HttpError.BadRequest(nameof(request.Url));
        
        var spider = HostContext.Resolve<Spider>();
            return new PingSingleResponse()
        {
            Ping = await spider.Ping(request.Url, true)
        };
    }
    
    [RequiresAccessKey]
    public object Post(RepublishPings request)
    {
        Republish<Ping>(request.Count);
        return new HttpResult();
    }
    
    [RequiresAccessKey]
    public object Post(RepublishPingsStore request)
    {
        Republish<StorePing>(request.Count);
        return new HttpResult();
    }

    [RequiresAccessKey]
    public object Post(RepublishTryNewPing request)
    {
        Republish<TryNewPing>(request.Count);
        return new HttpResult();
    }

    [RequiresAccessKey]
    public object Post(RepublishUpdatePing request)
    {
        Republish<UpdatePing>(request.Count, true);
        return new HttpResult();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="count">Max 100</param>
    /// <param name="delayed"></param>
    /// <typeparam name="T"></typeparam>
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
    
    private static Type FindTypeInAllAssembliesByFullName(string typeFullName)
    {
        if (typeFullName == null)
            throw new ArgumentNullException(nameof(typeFullName));

        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(x => x.GetTypes())
            .FirstOrDefault(x => x.FullName.Equals(typeFullName, StringComparison.InvariantCulture));
    }
}