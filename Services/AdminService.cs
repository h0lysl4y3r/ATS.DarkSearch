using System.Threading.Tasks;
using ATS.DarkSearch.Model;
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
    
    public object Get(GetUrlsRequest request)
    {
        return ATSAppHost.Links;
    }

    public async Task<object> Post(RestartSpiderRequest request)
    {
        var spider = this.Resolve<Spider>();
        spider.Stop();
        await spider.StartAsync();
        return new HttpResult();
    }

    public object Post(PingAllRequest request)
    {
        var mqServer = HostContext.AppHost.Resolve<IMessageService>();
        using var mqClient = mqServer.CreateMessageQueueClient();

        var links = ATSAppHost.Links;
        foreach (var link in links)
        {
            Logger.LogInformation("Scheduling to ping: " + link);
            
            mqClient.Publish(new Ping()
            {
                Url = link
            });
        }

        return new HttpResult();
    }
}