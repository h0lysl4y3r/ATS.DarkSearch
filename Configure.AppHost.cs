using System;
using Funq;
using Microsoft.AspNetCore.Hosting;
using ServiceStack;
using ServiceStack.Admin;
using ServiceStack.Api.OpenApi;

[assembly: HostingStartup(typeof(ATS.DarkSearch.ATSAppHost))]

namespace ATS.DarkSearch;

public class ATSAppHost : AppHostBase, IHostingStartup
{
    public static string[] Links { get; private set; }

    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices(services => {
            //
        })
        .Configure(app => {
            app.UseServiceStack(new ATSAppHost());
        });
    
    public ATSAppHost() : base("ATS.DarkSearch", typeof(ATSAppHost).Assembly) 
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
        
        //Links = System.IO.File.ReadAllLines(Path.Combine(_hostEnvironment.ContentRootPath, "Data", "links.txt"));
        Links = new string[] { "http://lldan5gahapx5k7iafb3s4ikijc4ni7gx5iywdflkba5y2ezyg6sjgyd.onion" };
    }
}
