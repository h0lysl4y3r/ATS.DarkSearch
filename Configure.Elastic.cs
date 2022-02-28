using System;
using Elasticsearch.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Nest;

[assembly: HostingStartup(typeof(ATS.DarkSearch.ConfigureElastic))]

namespace ATS.DarkSearch;

public class ConfigureElastic : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices((context,services) =>
        {
            var pool = new SingleNodeConnectionPool(new Uri(context.Configuration["ConnectionStrings:Elastic"]));
            var settings = new ConnectionSettings(pool)
                .DefaultIndex("");
            var client = new ElasticClient(settings);
            services.AddSingleton(client);            
        });
}