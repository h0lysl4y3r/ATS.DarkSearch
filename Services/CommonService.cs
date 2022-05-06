using System;
using System.IO;
using System.Linq;
using System.Net;
using ATS.Common;
using ATS.Common.Auth;
using ATS.Common.Model;
using ATS.Common.Model.DarkSearch;
using Nest;
using ServiceStack;
using ServiceStack.Messaging;

namespace ATS.DarkSearch.Services;

[Route("/admin/logs", "GET")]
public class GetLogs : BaseRequest, IGet, IReturn<string>
{
}

public class CommonService : Service
{
    [RequiresAccessKey]
    public object Get(GetLogs request)
    {
        try
        {
            var files = Directory.GetFiles("~Logs".MapServerPath());
            var file = files.OrderByDescending(x => x).FirstOrDefault();
            if (file == null)
                return "";
        
            return System.IO.File.ReadAllText(file);
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }
    
    [RequiresAccessKey]
    public object Get(GetHealth<GetHealthResponse> request)
    {
        var client = HostContext.AppHost.Resolve<ElasticClient>();
        var pingResponse = client.Ping();

        var mqServer = HostContext.AppHost.Resolve<IMessageService>();

        return new GetHealthResponse()
        {
            ElasticState = pingResponse.OriginalException?.Message ?? "",
            // Potential Statuses: Disposed, Stopped, Stopping, Starting, Started
            RabbitMqState = mqServer.GetStatus()
        };
    }

    [RequiresAccessKey]
    public object Get(GetVersion request)
    {
        return typeof(CommonService).Assembly.GetName().Version;
    }
}