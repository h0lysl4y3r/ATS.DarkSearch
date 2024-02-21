using System;
using Serilog;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using ServiceStack;

try
{
	var builder = WebApplication.CreateBuilder(args);

	builder.Host
		.UseSerilog((ctx, lc) => lc
			.Enrich.FromLogContext()
			.WriteTo.Console()
			.WriteTo.Debug()
			.WriteTo.Seq(serverUrl: builder.Configuration["ConnectionStrings:Seq"], apiKey: builder.Configuration["AppSettings:SeqToken"])
			.WriteTo.File(path: "~Logs/log.txt".MapServerPath(), rollingInterval: RollingInterval.Day)
			.ReadFrom.Configuration(ctx.Configuration)
	);

	var app = builder.Build();
	if (app.Environment.IsDevelopment())
	{
		app.UseDeveloperExceptionPage();
	}
	else
	{
		app.UseExceptionHandler("/Error");
		// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
		app.UseHsts();
		app.UseHttpsRedirection();
	}

	app.UseSerilogRequestLogging();

	Licensing.RegisterLicense("18238-e1JlZjoxODIzOCxOYW1lOkVMSUEgcy5yLm8uLFR5cGU6SW5kaWUsTWV0YTowLEhhc2g6UVI3WUI4UFpYOTlDY1Q0WWQ5bVpiYnQrRUorRG5kUldVVVYrdE1tMlVsWWl3S3pQb010ME4yVW1PR0g0VkFtUktDMW5zOWtYUlFKOFAyVHh3Sm8va0VHWVpVanZsZWZuOFlmeUkwNWFQbDI1QnRrWVBkNjVFcUk5OWhWb0RKSkM0R0dPOS9RZkpyR3JQVTRoT1l3VEhYV1BqdnFlaXl3RlVnd1lqTEQyVmdRPSxFeHBpcnk6MjAyMi0xMC0xM30=");

	Log.Information("Starting web host");
	app.Run();
}
catch (Exception ex)
{
	Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
	Log.CloseAndFlush();
}
