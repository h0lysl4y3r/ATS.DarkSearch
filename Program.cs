using Serilog;
using Serilog.Events;
using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using ServiceStack;

try
{
	var builder = WebApplication.CreateBuilder(args);

	builder.Host.UseSerilog((ctx, lc) => lc
		.MinimumLevel.Override("Microsoft", LogEventLevel.Information)
		.MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
		.Enrich.FromLogContext()
		.WriteTo.Console()
		.WriteTo.Debug()
		.WriteTo.File(path: "Logs/log.txt", rollingInterval: RollingInterval.Day)
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

	var licensePath = "~/Data/ServiceStackLicense.txt".MapServerPath();
	if (!File.Exists(licensePath))
		Log.Logger.Error("License path does not exist: " + licensePath);

	Licensing.RegisterLicenseFromFileIfExists(licensePath);
	
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
