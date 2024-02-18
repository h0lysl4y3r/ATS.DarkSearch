using Microsoft.AspNetCore.Hosting;
using Serilog;
using ServiceStack;
using ServiceStack.Redis;

[assembly: HostingStartup(typeof(ATS.DarkSearch.ConfigureRedis))]

namespace ATS.DarkSearch;

public class ConfigureRedis : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices(services =>
        {
        })
        .ConfigureAppHost(appHost =>
        {
            var connectionString = appHost.AppSettings.GetString("ConnectionStrings:Redis");
            Log.Information($"Configuring Redis with {connectionString}");

            var clientsManager = new RedisManagerPool(connectionString);
            appHost.Register<IRedisClientsManager>(clientsManager);
        });
}