using System;
using ATS.Common;
using ATS.Common.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack;

[assembly: HostingStartup(typeof(ATS.DarkSearch.ConfigureMessaging))]

namespace ATS.DarkSearch;

public class ConfigureMessaging : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices((context, services) =>
        {

            services.AddSingleton<ISettings<MessageSettings>>(new MessageSettings()
            {
                HostUri = new Uri("amqp://ats:ats@ats-dependencies.internal:30010")
            });
            services.AddSingleton<RabbitMqConsumer>();
            services.AddHostedService<RabbitMqConsumer>(p => p.GetService<RabbitMqConsumer>());
            services.AddSingleton<Spider>();

        })
        .ConfigureAppHost(appHost =>
        {

            var rabbitMqConsumer = appHost.Resolve<RabbitMqConsumer>();
            rabbitMqConsumer.Consume("ats-darksearch", "fanout", "test", message =>
            {
            });

        });

    public ConfigureMessaging()
    {
    }
}