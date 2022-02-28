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
}