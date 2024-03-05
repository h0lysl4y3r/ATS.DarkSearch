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
    public const string DelayedMessagesExchange = "mx.darksearch.delayed";
    private const int LoopDelayMs = 10 * 1000;

    private readonly IConfiguration _config;

    public RabbitMqWorker(IConfiguration config)
    {
        _config = config;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        Log.Debug("Starting {Service}...", nameof(RabbitMqWorker));
        
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // startup delay
        var delay = _config.GetValue<int>($"AppSettings:RabbitMqWorkerStartupDelaySec");
        Log.Information("{Service}:{Method} will start in {Delay}s", nameof(RabbitMqWorker), nameof(ExecuteAsync), delay);
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