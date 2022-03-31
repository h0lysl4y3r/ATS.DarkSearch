using System.Threading.Tasks;
using ATS.Common.Auth;
using ATS.DarkSearch.Model;
using ATS.DarkSearch.Workers;
using Serilog;
using ServiceStack;
using ServiceStack.Messaging;

namespace ATS.DarkSearch.Services;

public class PingService : Service
{
	[RequiresAccessKey]
	public async Task<object> Any(Ping request)
	{
		if (request.Url.IsNullOrEmpty())
			throw HttpError.BadRequest(nameof(request.Url));

		var spider = HostContext.Resolve<Spider>();
		if (spider.IsPaused)
		{
			Log.Debug($"{nameof(PingService)}:{nameof(Ping)} {nameof(Spider)} is paused when pinging " + request.Url);
			return null;
		}
		
		return await spider.Ping(request.Url, true);
	}
	
	[RequiresAccessKey]
	public object Any(StorePing request)
	{
		if (request.Ping == null)
			throw HttpError.BadRequest(nameof(request.Ping));
		
		var repo = HostContext.Resolve<PingsRepository>();
		var ping = repo.Get(request.Ping.Url);

		if (ping != null)
		{
			var dateCreated = ping.Date;
			ping.PopulateWith(request.Ping);
			ping.Date = dateCreated;
			repo.Update(ping);
		}
		else
		{
			repo.Add(request.Ping);
		}

		return new HttpResult();
	}
	
	[RequiresAccessKey]
	public object Any(TryNewPing request)
	{
		if (request.Url.IsNullOrEmpty())
			throw HttpError.BadRequest(nameof(request.Url));
		
		var repo = HostContext.Resolve<PingsRepository>();
		var ping = repo.Get(request.Url);

		if (ping != null)
		{
			Log.Warning($"{nameof(PingService)}:{nameof(TryNewPing)} Ping already stored for " + request.Url);
			return new HttpResult();
		}

		var mqServer = HostContext.AppHost.Resolve<IMessageService>();
		using var mqClient = mqServer.CreateMessageQueueClient();
		mqClient.Publish(new Ping()
		{
			Url = request.Url,
			AccessKey = request.AccessKey
		});

		return new HttpResult();
	}

	[RequiresAccessKey]
	public object Any(UpdatePing request)
	{
		if (request.Url.IsNullOrEmpty())
			throw HttpError.BadRequest(nameof(request.Url));

		UpdatePing(request.Url, request.AccessKey);

		return new HttpResult();
	}

	public static void UpdatePing(string url, string accessKey)
	{
		var mqServer = HostContext.Resolve<IMessageService>();
		using var mqClient = mqServer.CreateMessageQueueClient();

		Log.Debug($"{nameof(PingService)}:{nameof(UpdatePing)} Update ping of " + url);
        
		mqClient.Publish(new Ping()
		{
			Url = url,
			AccessKey = accessKey
		});
	}
}
