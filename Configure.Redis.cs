using Microsoft.AspNetCore.Hosting;
using ServiceStack;
using ServiceStack.Redis;

[assembly: HostingStartup(typeof(ATS.DarkSearch.ConfigureRedis))]

namespace ATS.DarkSearch;

public class ConfigureRedis : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureAppHost(appHost =>
        {
            appHost.Register<IRedisClientsManager>(
                new RedisManagerPool(appHost.AppSettings.GetString("ConnectionStrings:Redis")));

            // var clientsManager = appHost.Resolve<IRedisClientsManager>();
            // using var redis = clientsManager.GetClient();
        });
}