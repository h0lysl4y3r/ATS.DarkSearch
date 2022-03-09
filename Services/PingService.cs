using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ATS.Common.Poco;
using ATS.DarkSearch.Extensions;
using ATS.DarkSearch.Model;
using ATS.DarkSearch.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ServiceStack;
using ServiceStack.Messaging;
using ServiceStack.RabbitMq;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace ATS.DarkSearch.Services;

public class PingService : Service
{
	public ILoggerFactory LoggerFactory { get; set; }
	private ILogger _logger;
	public ILogger Logger => 
		_logger ?? (_logger = LoggerFactory.CreateLogger(typeof(PingService)));
	
	public async Task<object> Any(Ping request)
	{
		if (request.Url.IsNullOrEmpty())
			throw HttpError.BadRequest(nameof(request.Url));

		// Ping
		var spider = this.Resolve<Spider>();
		PingResultPoco ping = null;
		try
		{
			await spider.StartAsync();
			ping = await spider.ExecuteAsync(request.Url);
		}
		catch
		{
			spider.Stop();
			throw;
		}

		if (ping == null)
		{
			var message = $"{nameof(PingService)}:{nameof(Ping)} no ping result on " + request.Url;
			_logger.LogError(message);
			throw new Exception(message);
		}
		
		// Schedule elastic store
		var mqServer = HostContext.AppHost.Resolve<IMessageService>();
		using var mqClient = mqServer.CreateMessageQueueClient() as RabbitMqQueueClient;

		Logger.LogDebug("Scheduling store of " + request.Url);
		mqClient.Publish(new StorePing()
		{
			Ping = ping
		});

		// Try schedule new pings for links
		if (ping.Links != null && ping.Links.Length > 0)
		{
			for (int i = 0; i < ping.Links.Length; i++)
			{
				var link = ping.Links[i];
				Logger.LogDebug("Scheduling new ping for " + link);
				mqClient.Publish(new Ping()
				{
					Url = link
				});
			}
		}
		
		// update ping
		var config = Request.Resolve<Microsoft.Extensions.Configuration.IConfiguration>();
		mqClient.PublishDelayed(new Message<UpdatePing>(
			new UpdatePing()
			{
				Url = ping.Url
			})
		{
			Meta = new Dictionary<string, string>() { { "x-delay", config.GetValue<string>("AppSettings:RefreshPingIntervalMs") } }
		});

		return ping;
	}

	public object Any(StorePing request)
	{
		if (request.Ping == null)
			throw HttpError.BadRequest(nameof(request.Ping));
		
		var repo = HostContext.AppHost.Resolve<PingsRepository>();
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
	
	public object Any(TryNewPing request)
	{
		if (request.Url.IsNullOrEmpty())
			throw HttpError.BadRequest(nameof(request.Url));
		
		var repo = HostContext.AppHost.Resolve<PingsRepository>();
		var ping = repo.Get(request.Url);

		if (ping != null)
		{
			Logger.LogWarning("Ping already stored for " + request.Url);
			return new HttpResult();
		}

		var mqServer = HostContext.AppHost.Resolve<IMessageService>();
		using var mqClient = mqServer.CreateMessageQueueClient();
		mqClient.Publish(new Ping()
		{
			Url = request.Url
		});

		return new HttpResult();
	}

	public object Any(UpdatePing request)
	{
		Logger.LogDebug(nameof(UpdatePing) + " called");
		return new HttpResult();
	}
}
