using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

[assembly: HostingStartup(typeof(ATS.DarkSearch.ConfigureElastic))]

namespace ATS.DarkSearch;

public class ConfigureElastic : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices((context,services) =>
        {
            services.AddSingleton<OpenSearchClientFactory>();
            services.AddSingleton<PingsRepository>();
        });
}