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
		protected readonly Microsoft.Extensions.Configuration.IConfiguration _config;
		protected readonly ILogger<DarkSearchController> _logger;
		protected readonly IDiagnosticContext _diagnosticContext;
		protected readonly IWebHostEnvironment _hostEnvironment;
		protected static string[] _links;

		public DarkSearchController(Microsoft.Extensions.Configuration.IConfiguration config,
			ILogger<DarkSearchController> logger,
			IDiagnosticContext diagnosticContext,
			IWebHostEnvironment hostEnvironment)
		{
			_logger = logger;
			_config = config;
			_diagnosticContext = diagnosticContext;
			_hostEnvironment = hostEnvironment;

			InitConfiguration();
		}

		protected void InitConfiguration()
		{
			//_links = System.IO.File.ReadAllLines(Path.Combine(_hostEnvironment.ContentRootPath, "Data", "links.txt"));
			_links = new string[] { "http://lldan5gahapx5k7iafb3s4ikijc4ni7gx5iywdflkba5y2ezyg6sjgyd.onion" };
		}

		[HttpPost("urls")]
		public string[] GetUrls()
		{
			return _links;
		}

		[HttpPost("once")]
		public async Task<List<PingResultPoco>> PingOnceAll()
		{
			// configure
			var settings = new TorSharpSettings
			{
				ZippedToolsDirectory = Path.Combine(_hostEnvironment.ContentRootPath, "Tor", "TorZipped"),
				ExtractedToolsDirectory = Path.Combine(_hostEnvironment.ContentRootPath, "Tor", "TorExtracted"),
				PrivoxySettings = { Port = 18118 },
				TorSettings =
				{
					SocksPort = 19050,
					AdditionalSockPorts = { 19052 },
					ControlPort = 19051,
					ControlPassword = "",
				},
			};

			// download tools
			try
			{
				await new TorSharpToolFetcher(settings, new HttpClient()).FetchAsync();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, ex.Message);
				throw ex;
			}

			var result = new List<PingResultPoco>();

			// execute
			using (var proxy = new TorSharpProxy(settings))
			{
				var handler = new HttpClientHandler
				{
					Proxy = new WebProxy(new Uri("http://localhost:" + settings.PrivoxySettings.Port))
				};
				var httpClient = new HttpClient(handler);
				await proxy.ConfigureAndStartAsync();

				var utcNow = DateTimeOffset.UtcNow;
				foreach (var link in _links)
				{
					try
					{
						await proxy.GetNewIdentityAsync();

						var headRequest = new HttpRequestMessage(HttpMethod.Head, link);
						headRequest.Options.Set(new HttpRequestOptionsKey<TimeSpan>("RequestTimeout"), TimeSpan.FromSeconds(10));

						var ping = new PingResultPoco()
						{
							Url = link,
							DateCreated = utcNow
						};

						var headResponse = await httpClient.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead);

						ping.StatusCode = headResponse.StatusCode;

						// get title
						if ((int)ping.StatusCode < 300)
						{
							var html = await httpClient.GetStringAsync(link);
							ping.Title = GetHtmlTitle(html);
							ping.IsLive = true;
						}

						result.Add(ping);
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, ex.Message);
					}
				}

				proxy.Stop();
			}

			return result;
		}

		private string GetHtmlTitle(string html)
		{
			try
			{
				var config = Configuration.Default.WithDefaultLoader();
				using (var context = BrowsingContext.New(config))
				{
					var parser = context.GetService<IHtmlParser>();
					var document = parser.ParseDocument(html);

					var title = document.All.FirstOrDefault(m => m.LocalName == "title");
					return title?.TextContent ?? "";
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, ex.Message);
				return "";
			}
		}
	}
}
