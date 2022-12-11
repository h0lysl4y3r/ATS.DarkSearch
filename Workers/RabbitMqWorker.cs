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
    private const int LoopDelayMs = 10 * 1000;
    
    private readonly Microsoft.Extensions.Configuration.IConfiguration _config;
    
    public RabbitMqWorker(Microsoft.Extensions.Configuration.IConfiguration config)
    {
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // startup delay
        var delay = _config.GetValue<int>($"AppSettings:RabbitMqWorkerStartupDelaySec");
        Log.Information($"{nameof(RabbitMqWorker)} will start in {delay}s");
        await Task.Delay(delay * 1000, cancellationToken);

        var mqServer = await TaskHelpers.GetAsync<IMessageService>(() => 
            HostContext.AppHost?.Resolve<IMessageService>(), CancellationToken.None, 1000, 5);
        mqServer.Start();

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(LoopDelayMs, cancellationToken);
        }

        mqServer.Stop();
    }
}