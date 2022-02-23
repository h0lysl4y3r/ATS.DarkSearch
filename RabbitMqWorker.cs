using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceStack.Messaging;

namespace ATS.DarkSearch;

public class RabbitMqWorker : BackgroundService
{
    private const int MqStatsDescriptionDurationMs = 60 * 1000;

    private readonly ILogger<RabbitMqWorker> _logger;
    private readonly IMessageService _mqServer;

    public RabbitMqWorker(ILogger<RabbitMqWorker> logger, IMessageService mqServer)
    {
        _logger = logger;
        _mqServer = mqServer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _mqServer.Start();

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("MQ Worker running at: {stats}", this._mqServer.GetStatsDescription());
            await Task.Delay(MqStatsDescriptionDurationMs, stoppingToken);
        }

        _mqServer.Stop();
    }    
}