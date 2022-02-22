﻿using Serilog;
using System.Threading.Tasks;
using ATS.Common.Poco;
using ATS.DarkSearch.Model;
using Microsoft.Extensions.Logging;
using ServiceStack;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace ATS.DarkSearch.Services;

public class PingService : Service
{
	public ILoggerFactory LoggerFactory { get; set; }
	private ILogger _logger;
	public ILogger Logger => 
		_logger ?? (_logger = LoggerFactory.CreateLogger(typeof(PingService)));
	
	public async Task<PingResultPoco> Post(PingRequest request)
	{
		if (request.Url.IsNullOrEmpty())
			throw HttpError.BadRequest(nameof(request.Url));

		Logger.LogInformation("Pinging " + request.Url);
		Log.Information("Pinging " + request.Url);
		
		return new PingResultPoco();

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

		return ping;
	}
}
