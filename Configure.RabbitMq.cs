using System.Collections.Generic;
using ATS.Common.Model.DarkSearch;
using ATS.Common.ServiceStack;
using ATS.DarkSearch.Workers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nest;
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
            var mqServer = new ATSRabbitMqServer(RabbitMqWorker.DelayedMessagesExchange,
                appHost.AppSettings.GetString("ConnectionStrings:RabbitMq"))
            {
                DisablePublishingToOutq = true,
                DisablePublishingResponses = true,
                DisablePriorityQueues = true
            };
            appHost.Register<IMessageService>(mqServer);
            
            mqServer.RegisterHandler<Ping>(appHost.ExecuteMessage, 1);
            mqServer.RegisterHandler<TryNewPing>(appHost.ExecuteMessage, 1);
            mqServer.RegisterHandler<StorePing>(appHost.ExecuteMessage, 1);
            mqServer.RegisterHandler<UpdatePing>(appHost.ExecuteMessage, 1, RabbitMqWorker.DelayedMessagesExchange);
            
            // using var mqClient = mqServer.CreateMessageQueueClient();
            // mqClient.Publish(new Hello { Name = "Bugs Bunny" });
        });
}