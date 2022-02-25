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
    
}