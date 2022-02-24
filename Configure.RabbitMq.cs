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
            appHost.Register<IMessageService>(
                new RabbitMqServer(appHost.AppSettings.GetString("ConnectionStrings:RabbitMq"))
                {
                    DisablePublishingToOutq = true,
                });
        
            //mqServer.RegisterHandler<Hello>(host.ExecuteMessage);
            // using var mqClient = mqServer.CreateMessageQueueClient();
            // mqClient.Publish(new Hello { Name = "Bugs Bunny" });
        });
}