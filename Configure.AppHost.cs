using System;
using Funq;
using Microsoft.AspNetCore.Hosting;
using ServiceStack;
using ServiceStack.Admin;
using ServiceStack.Api.OpenApi;

[assembly: HostingStartup(typeof(ATS.DarkSearch.AppHost))]

namespace ATS.DarkSearch;

public class AppHost : AppHostBase, IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices(services => {
            //
        })
        .Configure(app => {
            app.UseServiceStack(new AppHost());
        });
    
    public AppHost() : base("ATS.DarkSearch", typeof(AppHost).Assembly) 
    {
    }

    public override void Configure(Container container)
    {
        Plugins.Add(new AdminUsersFeature());
        
        var hostConfig = new HostConfig
        {
            UseSameSiteCookies = true,
            DefaultContentType = MimeTypes.Json,
            DefaultRedirectPath = "/ui",
            DebugMode = true,
#if DEBUG                
            AdminAuthSecret = "adm1nSecret", // Enable Admin Access with ?authsecret=adm1nSecret
#endif
        };
        SetConfig(hostConfig);

        Plugins.Add(new CorsFeature()
        {
            AutoHandleOptionsRequests = true
        });
    }
}
