using Microsoft.AspNetCore.Hosting;
using ServiceStack;
using ServiceStack.Redis;
using Serilog;

[assembly: HostingStartup(typeof(ATS.DarkSearch.ConfigureRedis))]

namespace ATS.DarkSearch;

public class ConfigureRedis : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices((webHost, services) =>
        {
        })
        .ConfigureAppHost(appHost =>
        {
            var connectionString = appHost.AppSettings.GetString("ConnectionStrings:Redis");
            Log.Information("Configuring Redis with {ConnectionString}", connectionString);

            var clientsManager = new RedisManagerPool(connectionString);
            appHost.Register<IRedisClientsManager>(clientsManager);
            
        });
}