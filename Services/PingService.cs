﻿using System.Threading.Tasks;
using ATS.Common.Auth;
using ATS.Common.Model.DarkSearch;
using ATS.DarkSearch.Workers;
using Serilog;
using ServiceStack;
using ServiceStack.Messaging;

namespace ATS.DarkSearch.Services;

public class PingService : Service
{
	[RequiresAccessKey]
	public async Task Any(Ping request)
	{
		if (request.Url.IsNullOrEmpty())
			throw HttpError.BadRequest(nameof(request.Url));

		var spider = HostContext.Resolve<Spider>();
		if (spider.IsPaused)
		{
			Log.Warning($"[{nameof(PingService)}:{nameof(Ping)}] {nameof(Spider)} is paused when pinging " + request.Url);
			return;
		}
		
		await spider.Ping(request.Url);
	}
	
	[RequiresAccessKey]
	public void Any(StorePing request)
	{
		if (request.Ping == null)
			throw HttpError.BadRequest(nameof(request.Ping));
		
		var repo = HostContext.Resolve<PingsRepository>();
		var ping = repo.Get(request.Ping.Url);

		var ok = false;
		if (ping != null)
		{
			var dateCreated = ping.Date;
			ping.PopulateWith(request.Ping);
			ping.Date = dateCreated;
			ok = repo.Update(ping);
		}
		else
		{
			ok = repo.Add(request.Ping);
		}

		if (!ok)
		{
			Log.Warning($"[{nameof(PingService)}:{nameof(StorePing)}] Failed to store ping for " + request.Ping.Url);
			return;
		}
	}

	[RequiresAccessKey]
	public void Any(TryNewPing request)
	{
		if (request.Url.IsNullOrEmpty())
			throw HttpError.BadRequest(nameof(request.Url));

		var repo = HostContext.Resolve<PingsRepository>();
		var ping = repo.Get(request.Url);

		if (ping != null)
		{
			Log.Warning($"[{nameof(PingService)}:{nameof(TryNewPing)}] Ping already stored for " + request.Url);
			return;
		}

		var mqServer = HostContext.AppHost.Resolve<IMessageService>();
		using var mqClient = mqServer.CreateMessageQueueClient();
		mqClient.Publish(new Ping()
		{
			Url = request.Url,
			AccessKey = request.AccessKey
		});
	}

	[RequiresAccessKey]
	public void Any(UpdatePing request)
	{
		if (request.Url.IsNullOrEmpty())
			throw HttpError.BadRequest(nameof(request.Url));

		UpdatePing(request.Url, request.AccessKey);
	}

	public static void UpdatePing(string url, string accessKey)
	{
		var mqServer = HostContext.Resolve<IMessageService>();
		using var mqClient = mqServer.CreateMessageQueueClient();

		Log.Information($"{nameof(PingService)}:{nameof(UpdatePing)} Update ping of " + url);

		mqClient.Publish(new Ping()
		{
			Url = url,
			AccessKey = accessKey
		});
	}
}
