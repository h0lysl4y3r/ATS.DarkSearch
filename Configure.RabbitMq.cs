using System;
using ATS.Common.Model.DarkSearch;
using ATS.Common.ServiceStack;
using ATS.DarkSearch.Workers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using ServiceStack;
using ServiceStack.Messaging;
using RabbitMqWorker = ATS.DarkSearch.Workers.RabbitMqWorker;

[assembly: HostingStartup(typeof(ATS.DarkSearch.ConfigureRabbitMq))]

namespace ATS.DarkSearch;

public class ConfigureRabbitMq : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices(services =>
        {
            services.AddSingleton<Spider>();
            services.AddHostedService<ATS.DarkSearch.Workers.RabbitMqWorker>();
        })
        .ConfigureAppHost(appHost =>
        {
            var connectionString = appHost.AppSettings.GetString("ConnectionStrings:RabbitMq");

            var rabbitMqUri = new Uri(connectionString);
            Log.Information($"Configuring RabbitMq with {rabbitMqUri.DnsSafeHost}:{rabbitMqUri.Port}");
            
            var mqServer = new ATSRabbitMqServer(RabbitMqWorker.DelayedMessagesExchange,
                connectionString)
            {
                DisablePublishingToOutq = true,
                DisablePublishingResponses = true,
                DisablePriorityQueues = true
            };
            appHost.Register<IMessageService>(mqServer);
            
            // mqServer.RegisterHandler<Ping>(appHost.ExecuteMessage, 1);
            // mqServer.RegisterHandler<TryNewPing>(appHost.ExecuteMessage, 1);
            // mqServer.RegisterHandler<StorePing>(appHost.ExecuteMessage, 1);
            // mqServer.RegisterHandler<UpdatePing>(appHost.ExecuteMessage, 1, RabbitMqWorker.DelayedMessagesExchange);
        });
}