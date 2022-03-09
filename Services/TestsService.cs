using System;
using System.Linq;
using System.Net;
using ATS.Common.Poco;
using ATS.DarkSearch.Model;
using ServiceStack;
using ServiceStack.Messaging;

namespace ATS.DarkSearch.Services;

public class TestsService : Service
{
    public object Post(TestPing request)
    {
        if (request.Url.IsNullOrEmpty())
            throw HttpError.BadRequest(nameof(request.Url));
        
        var mqServer = HostContext.AppHost.Resolve<IMessageService>();
        using var mqClient = mqServer.CreateMessageQueueClient();
        mqClient.Publish(new Ping()
        {
            Url = request.Url
        });
        
        return new HttpResult();
    }

    public object Post(IndexAdd request)
    {
        var repo = HostContext.AppHost.Resolve<PingsRepository>();
        // repo.AddPing(new PingResultPoco()
        // {
        //     Url = "http://2gzyxa5ihm7nsggfxnu52rck2vv4rvmdlkiu3zzui5du4xyclen53wid.onion",
        //     Date = DateTimeOffset.UtcNow,
        //     Title = "my onion site",
        //     Description = "this is a great onion site to look at"
        // });
        // repo.AddPing(new PingResultPoco()
        // {
        //     Url = "http://lldan5gahapx5k7iafb3s4ikijc4ni7gx5iywdflkba5y2ezyg6sjgyd.onion",
        //     Date = DateTimeOffset.UtcNow,
        //     Title = "hacking heaven",
        //     Description = "find all your hacking tools on this site"
        // });
        repo.Add(new PingResultPoco()
        {
            Url = request.Url,
            Date = request.Date,
            Description = request.Description,
            Domain = request.Domain,
            IsLive = request.IsLive,
            Links = request.Links,
            StatusCode = request.StatusCode,
            Title = request.Title
        });
        return new HttpResult();
    }
    
    public object Put(IndexUpdate request)
    {
        var repo = HostContext.AppHost.Resolve<PingsRepository>();
        // repo.UpdatePing(new PingResultPoco()
        // {
        //     Url = "http://2gzyxa5ihm7nsggfxnu52rck2vv4rvmdlkiu3zzui5du4xyclen53wid.onion",
        //     Date = DateTimeOffset.UtcNow,
        //     Title = "my onion site 2",
        //     Description = "this is a great onion site to look at 2"
        // });
        repo.Update(new PingResultPoco()
        {
            Url = request.Url,
            Date = request.Date,
            Description = request.Description,
            Domain = request.Domain,
            IsLive = request.IsLive,
            Links = request.Links,
            StatusCode = request.StatusCode,
            Title = request.Title
        });
        return new HttpResult();
    }

    public object Get(IndexGet request)
    {
        var repo = HostContext.AppHost.Resolve<PingsRepository>();
        var ping = repo.Get(request.Url);
        return new IndexGetResponse()
        {
            Ping = ping
        };
    }

    public object Get(IndexGetAllUrls request)
    {
        var repo = HostContext.AppHost.Resolve<PingsRepository>();
        var urls = repo.GetUrls();
        return new IndexGetAllUrlsResponse()
        {
            Urls = urls
        };
    }

    public object Post(IndexSearch request)
    {
        var repo = HostContext.AppHost.Resolve<PingsRepository>();
        var pings = repo.Search(request.Text);
        return new IndexSearchResponse()
        {
            Pings = pings.ToArray()
        };
    }
}