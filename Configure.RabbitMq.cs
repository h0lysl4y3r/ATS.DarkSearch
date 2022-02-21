using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack;
using ServiceStack.Messaging;
using ServiceStack.RabbitMq;

[assembly: HostingStartup(typeof(ATS.DarkSearch.ConfigureRabbitMq))]

namespace ATS.DarkSearch;

public class ConfigureRabbitMq
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices((context, services) =>
        {
            services.AddSingleton<Spider>();
        })
        .ConfigureAppHost(appHost =>
        {
            appHost.Register<IMessageService>(
                new RabbitMqServer(appHost.AppSettings.GetString("ConnectionStrings:RabbitMq")));
            var mqServer = appHost.Resolve<IMessageService>();
            mqServer.Start();

            // using var mqClient = mqServer.CreateMessageQueueClient();
            // mqClient.Publish(new Hello { Name = "Bugs Bunny" });
        });
}