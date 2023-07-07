using System;
using System.Net;
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

            var connectionString = context.Configuration["ConnectionStrings:Elastic"];
            Log.Information("Configuring Elasticsearch with " + connectionString);

            ServicePointManager.ServerCertificateValidationCallback +=
                (sender, cert, chain, errors) => true;

            var usernamePassword = context
                .Configuration["AppSettings:ElasticBasicAuth"].Split(new [] {':'});
            
            var pool = new SingleNodeConnectionPool(new Uri(connectionString));

            var settings = new ConnectionSettings(pool)
                .DefaultIndex(PingsRepository.PingsIndex)
                .EnableApiVersioningHeader()
                .PrettyJson();
            if (usernamePassword.Length == 2)
            {
                settings = settings
                    .BasicAuthentication(usernamePassword[0], usernamePassword[1]);

                settings = settings
                    .ServerCertificateValidationCallback((sender, cert, chain, errors) => true);
                // var certBytes = AssemblyHelpers
                //     .GetEmbeddedResourceRaw("ATS.DarkSearch.ats-elasticsearch.pfx", typeof(ConfigureElastic).Assembly);
                //settings = settings.ClientCertificate(new X509Certificate2(certBytes, usernamePassword[1]));
                settings = settings.CertificateFingerprint("9D:BF:8B:59:F7:38:7F:E5:DE:54:7A:35:5F:4D:F0:D2:54:7B:D9:22:DA:1C:46:F5:DE:EE:86:7F:E7:38:D7:B9");
            }

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