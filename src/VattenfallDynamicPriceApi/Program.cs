using Serilog;
using Serilog.Events;
using VattenfallDynamicPriceApi;
using VattenfallDynamicPriceApi.Services;

try
{
	SetUpSerilog();
	await RunAppAsync(args);
}
catch (Exception ex)
{
	Console.WriteLine("Application crashed on startup: " + ex);
	Log.Fatal(ex, "Application crashed on startup");
}
finally
{
	await Log.CloseAndFlushAsync();
}

return;

static async Task RunAppAsync(string[] args)
{
	var builder = WebApplication.CreateSlimBuilder(args);
	
	builder.Host.UseSerilog();

	builder.Services.ConfigureHttpJsonOptions(options =>
	{
		options.SerializerOptions.TypeInfoResolverChain.Insert(0, SourceGenerationContext.Default);
	});

	var app = builder.Build();
	var dataService = new VattenfallDataService();
	await dataService.InitializeAsync();

	var version1Group = app.MapGroup("/v1");
	version1Group.MapGet("/data", () => dataService.Data);
	version1Group.MapGet("/evcc", () => dataService.EvccData);
	version1Group.MapGet("/now/electricity", () => dataService.GetCurrentElectricityTariff());
	version1Group.MapGet("/now/gas", () => dataService.GetCurrentGasTariff());

	await app.RunAsync();
}

static void SetUpSerilog()
{
	if (!Enum.TryParse(SettingsProvider.Instance.Settings.Logging.LogLevel, out LogEventLevel logLevel))
		logLevel = LogEventLevel.Warning;

	if (!Enum.TryParse(SettingsProvider.Instance.Settings.Logging.AspNetLogLevel, out LogEventLevel aspNetLogLevel))
		aspNetLogLevel = LogEventLevel.Warning;

	var loggerConfiguration = new LoggerConfiguration();

	loggerConfiguration
		.Enrich.FromLogContext()
		.MinimumLevel.Is(logLevel)
		.MinimumLevel.Override("Microsoft.Hosting.Lifetime", logLevel)
		.MinimumLevel.Override("Microsoft.Hosting", aspNetLogLevel)
		.MinimumLevel.Override("Microsoft.AspNetCore", aspNetLogLevel);
		
	loggerConfiguration = loggerConfiguration.WriteTo.Console(
		outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

	Log.Logger = loggerConfiguration.CreateLogger();
}