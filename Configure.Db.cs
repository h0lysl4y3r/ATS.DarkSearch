using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack;
using ServiceStack.Data;
using ServiceStack.OrmLite;

[assembly: HostingStartup(typeof(ATS.DarkSearch.ConfigureDb))]

namespace ATS.DarkSearch;

public class ConfigureDb : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices((context,services) => services.AddSingleton<IDbConnectionFactory>(new OrmLiteConnectionFactory(
            context.Configuration.GetConnectionString("DefaultConnection") ?? ":memory:",
            SqliteDialect.Provider)))
        .ConfigureAppHost(appHost =>
        {
            // Create non-existing Table and add Seed Data Example
        });
}