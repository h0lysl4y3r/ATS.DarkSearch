using System;
using System.Threading;
using System.Threading.Tasks;
using ATS.Common.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using ServiceStack;
using ServiceStack.Messaging;

namespace ATS.DarkSearch.Workers;

public class RabbitMqWorker : BackgroundService
{
    public const string DelayedMessagesExchange = "mx.servicestack.delayed";
    private const int MqStatsDescriptionDurationMs = 60 * 1000;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var mqServer = await TaskHelpers.GetAsync<IMessageService>(() => 
            HostContext.AppHost?.Resolve<IMessageService>(), 1000, CancellationToken.None, true);
        mqServer.Start();

        while (!stoppingToken.IsCancellationRequested)
        {
            Log.Information("MQ Worker running at: {stats}", mqServer.GetStatsDescription());
            await Task.Delay(MqStatsDescriptionDurationMs, stoppingToken);
        }

        mqServer.Stop();
    }
}