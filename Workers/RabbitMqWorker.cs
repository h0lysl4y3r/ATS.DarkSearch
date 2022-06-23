using System.Threading;
using System.Threading.Tasks;
using ATS.Common.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using ServiceStack;
using ServiceStack.Messaging;

namespace ATS.DarkSearch.Workers;

public class RabbitMqWorker : BackgroundService
{
    public const string DelayedMessagesExchange = "mx.servicestack.delayed";
    private const int MqStatsDescriptionDurationMs = 600 * 1000;
    
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var config = HostContext.Resolve<Microsoft.Extensions.Configuration.IConfiguration>();

        // startup delay
        var delay = config.GetValue<int>($"AppSettings:RabbitMqWorkerStartupDelaySec");
        Log.Information($"{nameof(RabbitMqWorker)} will start in {delay}s");
        await Task.Delay(delay * 1000, cancellationToken);

        var mqServer = await TaskHelpers.GetAsync<IMessageService>(() => 
            HostContext.AppHost?.Resolve<IMessageService>(), 1000, CancellationToken.None, true);
        mqServer.Start();

        while (!cancellationToken.IsCancellationRequested)
        {
            //Log.Information("MQ Worker running at: {stats}", mqServer.GetStatsDescription());
            await Task.Delay(MqStatsDescriptionDurationMs, cancellationToken);
        }

        mqServer.Stop();
    }
}