using Funq;
using Microsoft.AspNetCore.Hosting;
using ServiceStack;
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
        Plugins.Add(new OpenApiFeature());

        var hostConfig = new HostConfig
        {
            DefaultContentType = MimeTypes.Json,
            DefaultRedirectPath = "/swagger-ui",
            DebugMode = true
        };
        SetConfig(hostConfig);

        Plugins.Add(new CorsFeature()
        {
            AutoHandleOptionsRequests = true
        });
    }
}
