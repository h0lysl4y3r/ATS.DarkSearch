using ATS.DarkSearch.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack;
using ServiceStack.Messaging;
using ServiceStack.RabbitMq;

[assembly: HostingStartup(typeof(ATS.DarkSearch.ConfigureRabbitMq))]

namespace ATS.DarkSearch;

public class ConfigureRabbitMq : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices(services => {
            services.AddSingleton<Spider>();
            services.AddHostedService<RabbitMqWorker>();
        })
        .ConfigureAppHost(appHost =>
        {
            var mqServer = new RabbitMqServer(appHost.AppSettings.GetString("ConnectionStrings:RabbitMq"))
            {
                DisablePublishingToOutq = true,
                DisablePriorityQueues = true
            };
            appHost.Register<IMessageService>(mqServer);
        
            mqServer.RegisterHandler<Ping>(appHost.ExecuteMessage, 2);
          
            // using var mqClient = mqServer.CreateMessageQueueClient();
            // mqClient.Publish(new Hello { Name = "Bugs Bunny" });
        });
}