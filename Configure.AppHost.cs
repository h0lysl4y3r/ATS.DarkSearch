using System;
using System.Collections.Concurrent;
using ATS.Common;
using ATS.Common.Helpers;
using Funq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack;
using ServiceStack.Api.OpenApi;
using ServiceStack.Messaging;
using ServiceStack.RabbitMq;
using ServiceStack.Text;

[assembly: HostingStartup(typeof(ATS.DarkSearch.ATSAppHost))]

namespace ATS.DarkSearch;

public class ATSAppHost : AppHostBase, IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices(services => {
            services.AddSingleton<PingStats>();
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

        JsConfig.DateHandler = DateHandler.ISO8601;
        
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

    private static ConcurrentDictionary<string, uint> _brokerMessageCounts = new ConcurrentDictionary<string, uint>();
    private static ConcurrentDictionary<string, DateTimeOffset> _brokerMessageCountLastFetched = new ConcurrentDictionary<string, DateTimeOffset>();
    public static uint GetBrokerMessageCount(string typeFullName, RabbitMqQueueType queueType)
    {
        if (typeFullName.IsNullOrEmpty())
            throw new ArgumentNullException(nameof(typeFullName));
        
        var type = AssemblyHelpers.FindTypeInAllAssembliesByFullName(typeFullName);
        if (type == null)
            throw new ArgumentException(nameof(typeFullName));

        var queueNames = new QueueNames(type);
        string queueName = null;
        switch (queueType)
        {
            case RabbitMqQueueType.Priority:
                queueName = queueNames.Priority;
                break;
            case RabbitMqQueueType.In:
                queueName = queueNames.In;
                break;
            case RabbitMqQueueType.Out:
                queueName = queueNames.Out;
                break;
            case RabbitMqQueueType.Dlq:
                queueName = queueNames.Dlq;
                break;
        }
        if (queueName == null)
            throw new ArgumentException(nameof(queueType));

        var utcNow = DateTimeOffset.UtcNow;
        var getFromCache = true;
        if (!_brokerMessageCountLastFetched.ContainsKey(queueName)
            || (utcNow - _brokerMessageCountLastFetched[queueName]).TotalSeconds > 30)
        {
            getFromCache = false;
            _brokerMessageCountLastFetched[queueName] = utcNow;
        }
        
        // try from cache
        if (getFromCache && _brokerMessageCounts.ContainsKey(queueName))
            return _brokerMessageCounts[queueName];
        
        // otherwise request broker
        try
        {
            var mqServer = HostContext.AppHost.Resolve<IMessageService>();
            using var mqClient = mqServer.CreateMessageQueueClient() as RabbitMqQueueClient;

            var messageCount = mqClient.Channel.QueueDeclarePassive(queueName).MessageCount;
            _brokerMessageCounts[queueName] = messageCount;

            return messageCount;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, ex.Message);
            return 0;
        }
    }
}
