using System;
using Amazon;
using ATS.Common.Poco;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using OpenSearch.Client;
using OpenSearch.Net.Auth.AwsSigV4;
using Serilog;

[assembly: HostingStartup(typeof(ATS.DarkSearch.ConfigureElastic))]

namespace ATS.DarkSearch;

public class ConfigureElastic : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices((context,services) =>
        {
            services.AddSingleton<PingsRepository>();

            var connectionString = context.Configuration["ConnectionStrings:Elastic"];
            Log.Information("Configuring Elasticsearch with " + connectionString);

            var endpoint = new Uri(connectionString);

            var connection = new AwsSigV4HttpConnection(RegionEndpoint.EUWest2);
            var config = new ConnectionSettings(endpoint, connection)
                .DefaultIndex(PingsRepository.PingsIndex);
            var client = new OpenSearchClient(config);

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