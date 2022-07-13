using System;
using System.Globalization;
using System.IO;
using System.Linq;
using ATS.Common.Auth;
using ATS.Common.Model;
using ATS.Common.Model.DarkSearch;
using Nest;
using ServiceStack;
using ServiceStack.Messaging;
using ServiceStack.Redis;

namespace ATS.DarkSearch.Services;

public class CommonService : Service
{
    [RequiresAccessKey]
    public object Get(GetLogs request)
    {
        if (request.DateStr != null && !DateTimeOffset.TryParseExact(request.DateStr, "yyMMdd",
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dateTime))
            throw HttpError.BadRequest(nameof(request.DateStr));

        try
        {
            var files = Directory.GetFiles("~Logs".MapServerPath());

            string file = null;
            if (request.DateStr != null)
            {
                file = files.FirstOrDefault(x => x.Contains(request.DateStr));
                if (file == null)
                    throw HttpError.NotFound(request.DateStr);
            }
            else
            {
                file = files.OrderByDescending(x => x).FirstOrDefault();
                if (file == null)
                    return "";
            }
            
            return System.IO.File.ReadAllText(file);
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }
    
    public object Get(GetHealth<GetHealthResponse> request)
    {
        var utcNow = DateTimeOffset.UtcNow;
        if (_lastHealthCheck != null && (utcNow - _lastHealthCheckTime).TotalSeconds <= 5)
            return _lastHealthCheck;
        _lastHealthCheckTime = utcNow;
        
        var client = HostContext.AppHost.Resolve<ElasticClient>();
        var pingResponse = client.Ping();

        var clientsManager = HostContext.AppHost.Resolve<IRedisClientsManager>();
        using var redis = clientsManager.GetClient();
        var redisPing = redis.Ping();

        var mqServer = HostContext.AppHost.Resolve<IMessageService>();

        _lastHealthCheck = new GetHealthResponse()
        {
            RedisState = redisPing ? "" : "No ping",
            ElasticState = pingResponse.OriginalException?.Message ?? "",
            // Potential Statuses: Disposed, Stopped, Stopping, Starting, Started
            RabbitMqState = mqServer.GetStatus()
        };
        return _lastHealthCheck;
    }
    private static DateTimeOffset _lastHealthCheckTime;
    private static GetHealthResponse _lastHealthCheck;

    public object Get(GetVersion request)
    {
        return typeof(CommonService).Assembly.GetName().Version;
    }
}