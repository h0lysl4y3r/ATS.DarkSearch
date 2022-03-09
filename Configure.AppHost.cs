using Funq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack;
using ServiceStack.Admin;
using ServiceStack.Api.OpenApi;

[assembly: HostingStartup(typeof(ATS.DarkSearch.ATSAppHost))]

namespace ATS.DarkSearch;

public class ATSAppHost : AppHostBase, IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices(services => {
        })
        .Configure(app => {
            if (!HasInit)
                app.UseServiceStack(new ATSAppHost());
        });
    
    public ATSAppHost() : base("ATS.DarkSearch", typeof(ATSAppHost).Assembly) 
    {
    }

    public override void Configure(Container container)
    {
        //Plugins.Add(new AdminUsersFeature());
        Plugins.Add(new OpenApiFeature());
        
        var hostConfig = new HostConfig
        {
            UseSameSiteCookies = true,
            DefaultContentType = MimeTypes.Json,
            //DefaultRedirectPath = "/ui",
            DefaultRedirectPath = "/swagger-ui",
            DebugMode = true,
#if DEBUG                
            //AdminAuthSecret = "adm1nSecret", // Enable Admin Access with ?authsecret=adm1nSecret
#endif
        };
        SetConfig(hostConfig);

        Plugins.Add(new CorsFeature()
        {
            AutoHandleOptionsRequests = true
        });
    }
}
