using System;
using System.Threading;
using ATS.Common.Poco;
using Elasticsearch.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Nest;
using Serilog;

[assembly: HostingStartup(typeof(ATS.DarkSearch.ConfigureElastic))]

namespace ATS.DarkSearch;

public class ConfigureElastic : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices((context,services) =>
        {
            services.AddSingleton<PingsRepository>();

            // startup delay
            var delay = 10;
            Log.Information($"{nameof(ConfigureElastic)} will start in {delay}s");
            Thread.Sleep(delay * 1000);

            Log.Information("Configuring Elasticsearch");
            
            var pool = new SingleNodeConnectionPool(new Uri(context.Configuration["ConnectionStrings:Elastic"]));
            var settings = new ConnectionSettings(pool)
                .DefaultIndex(PingsRepository.PingsIndex)
                .PrettyJson();
            var client = new ElasticClient(settings);
            services.AddSingleton(client);
            
            if (!client.Indices.Exists(Indices.Parse(PingsRepository.PingsIndex)).Exists)
            {
                var response = client.Indices.Create(Indices.Index(PingsRepository.PingsIndex),
                    index => index.Map<PingResultPoco>(
                        x => x.AutoMap()
                    ));
            }
        });
}