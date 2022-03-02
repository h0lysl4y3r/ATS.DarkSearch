using System;
using System.Threading;
using System.Threading.Tasks;
using ATS.Common.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceStack;
using ServiceStack.Messaging;

namespace ATS.DarkSearch.Workers;

public class RabbitMqWorker : BackgroundService
{
    private const int MqStatsDescriptionDurationMs = 60 * 1000;

    private readonly ILogger<RabbitMqWorker> _logger;

    public RabbitMqWorker(ILogger<RabbitMqWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var mqServer = await TaskHelpers.GetAsync<IMessageService>(() => 
            HostContext.AppHost?.Resolve<IMessageService>(), 1000, CancellationToken.None, true);
        mqServer.Start();

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("MQ Worker running at: {stats}", mqServer.GetStatsDescription());
            await Task.Delay(MqStatsDescriptionDurationMs, stoppingToken);
        }

        mqServer.Stop();
    }
}