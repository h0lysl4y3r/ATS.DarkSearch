using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceStack;
using ServiceStack.Messaging;

namespace ATS.DarkSearch;

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
        var mqServer = await GetAsync<IMessageService>(() => 
            HostContext.AppHost?.Resolve<IMessageService>(), 1000, CancellationToken.None, true);
        mqServer.Start();

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("MQ Worker running at: {stats}", mqServer.GetStatsDescription());
            await Task.Delay(MqStatsDescriptionDurationMs, stoppingToken);
        }

        mqServer.Stop();
    }
    
    public static async Task<T> GetAsync<T>(Func<T> fn, int delayMs, CancellationToken cancellationToken, bool supressExceptions)
        where T : class
    {
        return await Task.Run(async () =>
        {
            T instance = null;
            while (true)
            {
                if (supressExceptions)
                {
                    try
                    {
                        instance = fn();
                    }
                    catch { }
                }
                else
                {
                    instance = fn();
                }

                if (instance != null) break;
                
                if (delayMs > 0)
                    await Task.Delay(delayMs, cancellationToken);
            }
            return instance;
        }, cancellationToken);
    }
}