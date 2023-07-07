using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ATS.Common;
using ATS.Common.Auth;
using ATS.Common.Extensions;
using ATS.Common.Helpers;
using ATS.Common.Model;
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

[Route("/admin/pings/count", "GET", Summary = "Get ping index document count.")]
public class GetPingCount : BaseRequest, IGet, IReturn<long>
{
}

public class AdminService : Service
{
    [RequiresAccessKey]
    public object Get(GetPingCount request)
    {
        var repo = HostContext.AppHost.Resolve<PingsRepository>();
        return new HttpResult(repo.Count());
    }

    [RequiresAccessKey]
    public object Post(AddOrRemoveToBlacklist request)
    {
        var spider = HostContext.AppHost.Resolve<Spider>();
        if (!spider.AddOrRemoveToBlacklist(request.Domain, request.Add))
            throw HttpError.ServiceUnavailable(nameof(request.Domain));
        
        return new HttpResult();
    }

    [RequiresAccessKey]
    public object Get(GetAllUrls request)
    {
        if (request.MaxResults <= 0)
            throw HttpError.BadRequest(nameof(request.MaxResults));
        
        var repo = HostContext.AppHost.Resolve<PingsRepository>();
        var maxResults = Math.Min(request.MaxResults, 1000);
        var urls = repo.GetUrls(request.InputScrollId, out var outputScrollId, request.MaxResults);
        return new GetAllUrlsResponse()
        {
            OutputScrollId = outputScrollId,
            Urls = urls
        };
    }

    [RequiresAccessKey]
    public object Get(DumpAllUrls request)
    {
        var repo = HostContext.AppHost.Resolve<PingsRepository>();

        var dumpPath = $"~/out/dump_{DateTimeOffset.UtcNow.ToString("yyMMdd_hhmmss")}.txt".MapServerPath();
        Task.Run(() =>
        {
            var urls = repo.GetUrls(null, out var outputScrollId, 1000 * 1000 * 1000)
                .ToList();
            urls.Insert(0, urls.Count.ToString());
        
            File.WriteAllLines(dumpPath, urls);
        });
        
        return new HttpResult(dumpPath);
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
        var repo = HostContext.AppHost.Resolve<PingsRepository>();

        string inputScrollId = null;
        while (true)
        {
            var urls = repo.GetUrls(inputScrollId, out var outputScrollId, 1000);
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
    public object Put(ArchivePings request)
    {
        if (request.Domain.IsNullOrEmpty())
            throw HttpError.BadRequest(nameof(request.Domain));
        
        var spider = HostContext.AppHost.Resolve<Spider>();

        Task.Run(() =>
        {
            var mqServer = HostContext.AppHost.Resolve<IMessageService>();
            using var mqClient = mqServer.CreateMessageQueueClient() as RabbitMqQueueClient;

            var count = 0;
            var archived = new List<string>();

            while (true)
            {
                if (count % (100 * 1000) == 0 && count > 0)
                {
                    if (archived.Count > 0)
                    {
                        IOHelpers.EnsureDirectory($"~/out/archived/{request.Domain}".MapServerPath());
                        var dumpPath = $"~/out/archived/{request.Domain}/{DateTimeOffset.UtcNow.ToString("yyMMdd_hhmmss")}.txt".MapServerPath();
                        File.WriteAllLines(dumpPath, archived);
                        Log.Debug($"[{nameof(AdminService)}:{nameof(ArchivePings)}] archived " + archived.Count + " into " + dumpPath);
                        archived.Clear();
                    }
                }
                count++;

                // no more messages?
                var message = mqClient.Get<Ping>(QueueNames<Ping>.In);
                if (message == null)
                {
                    Log.Debug($"[{nameof(AdminService)}:{nameof(ArchivePings)}] archived {count}, looks like no more to archive");
                    break;
                }
            
                // no ping to update?
                mqClient.Ack(message);
                var ping = message.GetBody();
                if (ping == null)
                    continue;

                var pingStats = HostContext.AppHost.Resolve<PingStats>();

                // archive selected pings (they are essentially blacklisted)
                if (ping.Url.Contains(request.Domain))
                {
                    archived.Add(ping.Url);
                    pingStats.Update(ping.Url, PingStats.PingState.Blacklisted);
                    continue;
                }

                // schedule to update if not throttled or blacklisted
                if (spider.IsThrottledOrBlacklisted(ping.Url) == PingStats.PingState.Ok)
                    spider.PublishPingUpdate(mqClient, ping.Url);
            }

            Log.Information($"[{nameof(AdminService)}:{nameof(ArchivePings)}] archiving finished");
        });
        
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

        var type = AssemblyHelpers.FindTypeInAllAssembliesByFullName(request.TypeFullName);
        if (type == null)
            throw HttpError.NotFound(nameof(request.TypeFullName));
        
        var mqServer = HostContext.AppHost.Resolve<IMessageService>() as ATSRabbitMqServer;
        var queueNames = new QueueNames(type);
        mqServer.PurgeQueues(queueNames.In, queueNames.Priority, queueNames.Out, queueNames.Dlq);
        return new HttpResult();
    }

    [RequiresAccessKey]
    public object Put(PingAllByFile request)
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
            if (!request.PingOnExists)
            {            
                var existingPing = repo.Get(link);
                if (existingPing != null)
                {
                    Log.Debug($"[{nameof(AdminService)}:{nameof(PingAllByFile)}] Skipping, ping of " + link + " exists");
                    continue;
                }
            }
            
            Log.Information($"[{nameof(AdminService)}:{nameof(PingAllByFile)}] Scheduling ping of " + link);
            mqClient.Publish(new Ping()
            {
                Url = link,
                AccessKey = config.GetValue<string>("AppSettings:AccessKey")
            });
        }

        return new HttpResult();
    }

    [RequiresAccessKey]
    public object Post(PingAll request)
    {
        if (request.Urls == null || request.Urls.Length == 0)
            throw HttpError.BadRequest(nameof(request.Urls));

        var links = request.Urls
            .Select(x => UriHelpers.GetUriSafe(x)?.ToString())
            .Where(x => x != null)
            .ToArray();
        if (links.Length == 0)
            throw HttpError.ExpectationFailed(nameof(request.Urls));

        var repo = HostContext.AppHost.Resolve<PingsRepository>();
        var mqServer = HostContext.AppHost.Resolve<IMessageService>();
        using var mqClient = mqServer.CreateMessageQueueClient();

        var config = Request.Resolve<IConfiguration>();
        foreach (var link in links)
        {
            if (!request.PingOnExists)
            {            
                var existingPing = repo.Get(link);
                if (existingPing != null)
                {
                    Log.Debug($"[{nameof(AdminService)}:{nameof(PingAll)}] Skipping, ping of " + link + " exists");
                    continue;
                }
            }
            
            Log.Information($"[{nameof(AdminService)}:{nameof(PingAll)}] Scheduling ping of " + link);
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
            Ping = await spider.Ping(request.Url)
        };
    }

    [RequiresAccessKey]
    public object PUt(UpdatePingSingle request)
    {
        if (request.Url.IsNullOrEmpty())
            throw HttpError.BadRequest(nameof(request.Url));
        
        var mqServer = HostContext.AppHost.Resolve<IMessageService>();
        using var mqClient = mqServer.CreateMessageQueueClient() as RabbitMqQueueClient;

        var spider = HostContext.Resolve<Spider>();
        spider.PublishPingUpdate(mqClient, request.Url);

        return new HttpResult();
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

    [RequiresAccessKey]
    public object Get(GetPingStats request)
    {
        var pingStats = HostContext.AppHost.Resolve<PingStats>();
        var now = request.HourTicks > 0
            ? new DateTimeOffset(request.HourTicks, TimeSpan.Zero)
            : DateTimeOffset.UtcNow;
        var minus24h = now.AddDays(-1);
        var minus72h = now.AddDays(-3);
        
        return new GetPingStatsResponse()
        {
            Ok1h = pingStats.Get(now, PingStats.PingState.Ok),
            Blacklisted1h = pingStats.Get(now, PingStats.PingState.Blacklisted),
            Throttled1h = pingStats.Get(now, PingStats.PingState.Throttled),
            Paused1h = pingStats.Get(now, PingStats.PingState.Paused),
            Ok24h = pingStats.Get(minus24h, PingStats.PingState.Ok),
            Blacklisted24h = pingStats.Get(minus24h, PingStats.PingState.Blacklisted),
            Throttled24h = pingStats.Get(minus24h, PingStats.PingState.Throttled),
            Paused24h = pingStats.Get(minus24h, PingStats.PingState.Paused),
            Ok72h = pingStats.Get(minus72h, PingStats.PingState.Ok),
            Blacklisted72h = pingStats.Get(minus72h, PingStats.PingState.Blacklisted),
            Throttled72h = pingStats.Get(minus72h, PingStats.PingState.Throttled),
            Paused72h = pingStats.Get(minus72h, PingStats.PingState.Paused),
        };
    }
    
    [RequiresAccessKey]
    public object Get(GetPingStatsBlacklisted request)
    {
        var spider = HostContext.AppHost.Resolve<Spider>();
        return new HttpResult(spider.Blacklist.ToList());
    }    

    [RequiresAccessKey]
    public object Get(GetPingStatsThrottled request)
    {
        var spider = HostContext.AppHost.Resolve<Spider>();
        return new HttpResult(spider.ThrottleMap.Keys.ToList());
    }

    [RequiresAccessKey]
    public object Delete(ClearThrottled request)
    {
        var spider = Request.Resolve<Spider>();
        if (request.Domain != null)
        {
            var result = spider.ThrottleMap.Remove(request.Domain, out _);
            return new HttpResult(result);
        }
        
        spider.ThrottleMap.Clear();
        return new HttpResult();
    }

    [RequiresAccessKey]
    public object Get(GetMessageBrokerStats request)
    {
        var mqServer = HostContext.AppHost.Resolve<IMessageService>();
        return new HttpResult(mqServer.GetStatsDescription());
    }

    [RequiresAccessKey]
    public object Get(GetMessageCount request)
    {
        var count = ATSAppHost.GetBrokerMessageCount(request.TypeFullName, request.QueueType);
        return new HttpResult(count);
    }

    [RequiresAccessKey]
    public object Put(UpdateServiceUrl request)
    {
        if (!AppHostHelpers.UpdateServiceUrl(Request, request.Url, request.ServiceName))
            throw HttpError.ServiceUnavailable($"Failed to update {request.ServiceName}");
        
        return new HttpResult();
    }

    [RequiresAccessKey]
    public object Get(GetUtcNowTicks request)
    {
        var utcNow = DateTimeOffset.UtcNow;
        var now = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, 0, 0);
        return new HttpResult(now.Ticks);
    }

    [RequiresAccessKey]
    public object Get(GetDomain request)
    {
        if (request.Url.IsNullOrEmpty())
            throw HttpError.BadRequest(nameof(request.Url));

        var uri = UriHelpers.GetUriSafe(request.Url);
        if (uri == null)
            throw HttpError.BadRequest(nameof(request.Url));
        
        return new HttpResult(uri.DnsSafeHost);
    }

    private void Republish<T>(int count, bool delayed = false)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        var config = Request.Resolve<IConfiguration>();
        
        Task.Run(() =>
        {
            var mqServer = HostContext.AppHost.Resolve<IMessageService>();
            using var mqClient = mqServer.CreateMessageQueueClient() as RabbitMqQueueClient;

            for (int i = 0; i < count; i++)
            {
                IMessage<T> dlqMsg = mqClient.Get<T>(QueueNames<T>.Dlq);
                if (dlqMsg == null)
                    break;
            
                mqClient.Ack(dlqMsg);
                if (delayed)
                {
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
        });
    }
}