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
        .ConfigureServices((context, services) =>
        {
            services.AddSingleton<Spider>();
        })
        .ConfigureAppHost(appHost =>
        {
            var mqServer = new RabbitMqServer(appHost.AppSettings.GetString("ConnectionStrings:RabbitMq")) {
                DisablePublishingToOutq = true,
            };
            //mqServer.RegisterHandler<Hello>(host.ExecuteMessage);
            appHost.Register<IMessageService>(mqServer);
            
            // using var mqClient = mqServer.CreateMessageQueueClient();
            // mqClient.Publish(new Hello { Name = "Bugs Bunny" });
            
            services.AddSingleton(AppHost.Resolve<IMessageService>());
            services.AddHostedService<MqWorker>();
        });
}