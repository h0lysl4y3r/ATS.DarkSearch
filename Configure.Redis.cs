using System.Threading;
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
            Log.Information("Configuring Redis");

            var clientsManager = new RedisManagerPool(appHost.AppSettings.GetString("ConnectionStrings:Redis"));
            appHost.Register<IRedisClientsManager>(clientsManager);
        });
}