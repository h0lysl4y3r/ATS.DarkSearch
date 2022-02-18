using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using System;
using Knapcode.TorSharp;
using System.Net.Http;
using System.Net;
using ATS.Common.Poco;
using AngleSharp;
using AngleSharp.Html.Parser;

namespace ATS.DarkSearch.Controllers
{
	[ApiController]
	[Route("/ping/")]
	public class DarkSearchController : ControllerBase
	{
		private readonly Microsoft.Extensions.Configuration.IConfiguration _config;
		private readonly ILogger<DarkSearchController> _logger;
		private readonly IDiagnosticContext _diagnosticContext;
		private readonly IWebHostEnvironment _hostEnvironment;
		private readonly Spider _spider;
		private static string[] _links;

		public DarkSearchController(Microsoft.Extensions.Configuration.IConfiguration config,
			ILogger<DarkSearchController> logger,
			IDiagnosticContext diagnosticContext,
			IWebHostEnvironment hostEnvironment,
			Spider spider)
		{
			_logger = logger;
			_config = config;
			_diagnosticContext = diagnosticContext;
			_hostEnvironment = hostEnvironment;
			_spider = spider;

			InitConfiguration();
		}

		protected void InitConfiguration()
		{
			//_links = System.IO.File.ReadAllLines(Path.Combine(_hostEnvironment.ContentRootPath, "Data", "links.txt"));
			_links = new string[] { "http://lldan5gahapx5k7iafb3s4ikijc4ni7gx5iywdflkba5y2ezyg6sjgyd.onion" };
		}

		[HttpPost("tests/urls")]
		public string[] GetUrls()
		{
			return _links;
		}

		[HttpPost("ping")]
		public async Task<PingResultPoco> Ping(string link)
		{
			PingResultPoco ping = null;
			try
			{
				await _spider.StartAsync();
				ping = await _spider.ExecuteAsync(link);
			}
			catch
			{
				_spider.Stop();
				throw;
			}

			return ping;
		}
	}
}
