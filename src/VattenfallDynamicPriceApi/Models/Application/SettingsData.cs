namespace VattenfallDynamicPriceApi.Models.Application;

public class SettingsData
{
	public bool UseKnownValues { get; set; } = false;
	
	public string ScrapePageUrl { get; set; } = "https://www.vattenfall.nl/klantenservice/alles-over-je-dynamische-contract/";
	
	public string KnownApiBaseUrl { get; set; } = string.Empty;

	public string KnownApiKey { get; set; } = string.Empty;

	public int RefreshIntervalSeconds { get; set; } = 3600;

	public LoggingSettings Logging { get; set; } = new();
}

public class LoggingSettings
{
	public string LogLevel { get; set; }

	public string AspNetLogLevel { get; set; }
}