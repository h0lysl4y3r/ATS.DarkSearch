using System;
using System.Globalization;
using System.IO;
using System.Linq;
using ATS.Common.Auth;
using ATS.Common.Model;
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

    [RequiresAccessKey]
    public object Get(GetStatus request)
    {
        var utcNow = DateTimeOffset.UtcNow;
        if (_lastStatus != null && (utcNow - _lastHealthCheckTime).TotalSeconds <= 5)
            return _lastStatus;
        _lastHealthCheckTime = utcNow;

        var clientFactory = HostContext.AppHost.Resolve<OpenSearchClientFactory>();
        var client = clientFactory.Create();
        var pingResponse = client.Ping();

        var clientsManager = HostContext.AppHost.Resolve<IRedisClientsManager>();
        using var redis = clientsManager.GetClient();
        var redisPing = redis.Ping();

        var mqServer = HostContext.AppHost.Resolve<IMessageService>();

        _lastStatus = $"Redis:{(redisPing ? "OK" : "No ping")}\n"
                      + $"RabbitMq:{mqServer.GetStatus()}\n"
                      + $"Elastic:{pingResponse.OriginalException?.Message ?? "OK"}\n";

        return new HttpResult(_lastStatus);
    }
    private static DateTimeOffset _lastHealthCheckTime;
    private static string _lastStatus;

    public object Get(GetVersion request)
    {
        return new HttpResult(typeof(CommonService).Assembly.GetName().Version);
    }
}