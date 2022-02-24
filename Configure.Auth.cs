using ATS.Common.Auth;
using Microsoft.AspNetCore.Hosting;
using ServiceStack;
using ServiceStack.Auth;
using ServiceStack.FluentValidation;

[assembly: HostingStartup(typeof(ATS.DarkSearch.ConfigureAuth))]

namespace ATS.DarkSearch;

public class ATSRegistrationValidator : RegistrationValidator
{
    public ATSRegistrationValidator()
    {
        RuleSet(ApplyTo.Post, () =>
        {
            RuleFor(x => x.DisplayName).NotEmpty();
            RuleFor(x => x.ConfirmPassword).NotEmpty();
        });
    }
}

public class ConfigureAuth : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        //.ConfigureServices(services => services.AddSingleton<ICacheClient>(new MemoryCacheClient()))
        .ConfigureAppHost(appHost =>
        {
            var appSettings = appHost.AppSettings;
            appHost.Plugins.Add(new AuthFeature(() => new ATSUserSession(),
                new IAuthProvider[] {
                    new JwtAuthProvider(appSettings) {
                        AuthKeyBase64 = appSettings.GetString("AuthKeyBase64") ?? "cARl12kvS/Ra4moVBIaVsrWwTpXYuZ0mZf/gNLUhDW5=",
                    },
                    new CredentialsAuthProvider(appSettings),     /* Sign In with Username / Password credentials */
                    // new FacebookAuthProvider(appSettings),        /* Create App https://developers.facebook.com/apps */
                    // new GoogleAuthProvider(appSettings),          /* Create App https://console.developers.google.com/apis/credentials */
                    // new MicrosoftGraphAuthProvider(appSettings),  /* Create App https://apps.dev.microsoft.com */
                })
            {
                IncludeDefaultLogin = false
            });

            appHost.Plugins.Add(new RegistrationFeature()); //Enable /register Service

            appHost.RegisterAs<ATSRegistrationValidator, IValidator<Register>>();
        });    
}