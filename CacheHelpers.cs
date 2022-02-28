using System;
using ATS.Common.Poco;
using Microsoft.AspNetCore.Hosting;
using ServiceStack;
using ServiceStack.Redis;

namespace ATS.DarkSearch;

public class CacheHelpers
{
    private readonly IWebHostEnvironment _environment;
    private readonly string _assemblyName;
    
    public CacheHelpers(IWebHostEnvironment environment)
    {
        _environment = environment;
        _assemblyName = typeof(CacheHelpers).Assembly.GetName().Name;
    }

    public void AddPingResult(PingResultPoco result)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));
        
        var clientsManager = HostContext.AppHost.Resolve<IRedisClientsManager>();
        using var redis = clientsManager.GetClient();

        var key = GetPingCacheKey(result.Url);
        redis.Set(key, result);
    }

    public void GetAllPingsResults()
    {
        var clientsManager = HostContext.AppHost.Resolve<IRedisClientsManager>();
        using var redis = clientsManager.GetClient();

        var key = GetAllPingsCacheKey();
        redis.Get<PingResultPoco>(key);
    }
    
    public string GetPingCacheKey(string url)
    {
        return $"{_assemblyName}:{_environment.EnvironmentName}:Ping:{url}";
    }

    public string GetAllPingsCacheKey()
    {
        return $"{_assemblyName}:{_environment.EnvironmentName}:Ping";
    }
}