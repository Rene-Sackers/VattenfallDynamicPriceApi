using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Serilog;
using VattenfallDynamicPriceApi.Extensions;
using VattenfallDynamicPriceApi.Models.Evcc;
using VattenfallDynamicPriceApi.Models.Vattenfall;

namespace VattenfallDynamicPriceApi.Services;

public partial class VattenfallDataService : IVattenfallDataService
{
	public FlexTariffData[]? Data { get; private set; } = [];
	
	public EvccApiHourlyData[]? EvccData { get; private set; } = [];
	
	private TimeSpan _cacheDuration = TimeSpan.FromSeconds(60);
	private Timer? _timer;

	public async Task InitializeAsync()
	{
		Log.Information("Initializing Vattenfall data service...");
		
		_cacheDuration = TimeSpan.FromSeconds(Math.Max(60, SettingsProvider.Instance.Settings.RefreshIntervalSeconds));
		Log.Information("Refresh interval: {Interval}", _cacheDuration);
		
		await UpdateDataAsync();
		_timer = new Timer(RefreshTimerElapsed, null, _cacheDuration, _cacheDuration);
	}

	private TariffData? GetCurrentTariffForProductType(string productType, string description)
	{
		var productData = Data?.FirstOrDefault(d => d.Product == productType);
		if (productData == null)
		{
			Log.Error("Could not get current {Description} tariff, no data", description);
			return null;
		}

		var now = DateTimeOffset.Now;
		var currentTariff = productData.TariffData.FirstOrDefault(d => d.StartTime <= now && d.EndTime >= now);
		if (currentTariff != null)
			return currentTariff;

		Log.Error("Could not get current {Description} tariff, no value found for current time", description);

		return null;
	}

	public decimal GetCurrentElectricityTariff()
		=> GetCurrentTariffForProductType("E", "electricity")?.AmountInclVat ?? 0;

	public decimal GetCurrentGasTariff()
		=> GetCurrentTariffForProductType("G", "gas")?.AmountInclVat ?? 0;

	public decimal GetCurrentElectricityExportTariff()
		=> GetCurrentTariffForProductType("E", "electricity export")?.Details.FirstOrDefault(d => d.Type == "PRICE")?.AmountExclVat ?? 0;

	private void RefreshTimerElapsed(object? _)
	{
		try
		{
			Task.Run(UpdateDataAsync).Wait();
		}
		catch (Exception e)
		{
			Log.Error(e, "Failed to update data");
		}
	}

	private async Task UpdateDataAsync()
	{
		Log.Information("Updating data");
		
		var (apiBaseUrl, apiKey) = await TryGetApiUrlAndKeyAsync();
		if (string.IsNullOrWhiteSpace(apiBaseUrl) || string.IsNullOrWhiteSpace(apiKey))
		{
			Log.Error("Could not get API URL or key");
			return;
		}
		
		Data = await GetFlexTariffDataAsync(apiBaseUrl, apiKey);
		
		var electricityData = Data.FirstOrDefault(d => d.Product == "E");
		if (electricityData == null)
		{
			Log.Error("Could not find electricity data in API response");
			return;
		}
		
		EvccData = electricityData.TariffData.Select(td => new EvccApiHourlyData
			{
				Start = td.StartTime.UtcDateTime,
				End = td.EndTime.UtcDateTime,
				Value = td.AmountInclVat
			})
			.OrderBy(td => td.Start)
			.ToArray();
		
		Log.Information("Updated data");
	}

	private static async Task<FlexTariffData[]> GetFlexTariffDataAsync(string apiBaseUrl, string apiKey)
	{
		var apiEndpoint = apiBaseUrl + "/DynamicTariff";
		var apiRequest = new HttpRequestMessage(HttpMethod.Get, apiEndpoint);
		apiRequest.Headers.Add("ocp-apim-subscription-key", apiKey);

		using var httpClient = new HttpClient();
		var apiResponse = await httpClient.SendAsync(apiRequest);
		apiResponse.EnsureSuccessStatusCode();

		var jsonData = await apiResponse.Content.ReadAsStringAsync();
		return JsonSerializer.Deserialize(jsonData, SourceGenerationContext.Default.FlexTariffDataArray)!;
	}

	private static async Task<(string apiBaseUrl, string apiKey)> TryGetApiUrlAndKeyAsync()
	{
		if (SettingsProvider.Instance.Settings.UseKnownValues)
			return (SettingsProvider.Instance.Settings.KnownApiBaseUrl, SettingsProvider.Instance.Settings.KnownApiKey);

		try
		{
			return await GetApiUrlAndKeyAsync();
		}
		catch (Exception e)
		{
			Log.Error(e, "Failed to get API URL and key dynamically, falling back to known values");
			return (SettingsProvider.Instance.Settings.KnownApiBaseUrl, SettingsProvider.Instance.Settings.KnownApiKey);
		}
	}

	private static async Task<(string apiBaseUrl, string apiKey)> GetApiUrlAndKeyAsync()
	{
		var handler = new HttpClientHandler
		{
			AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
		};

		using var httpClient = new HttpClient(handler);
		httpClient.Timeout = TimeSpan.FromSeconds(10);
		httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");

		var webPageHtml = await httpClient.GetStringAsync(SettingsProvider.Instance.Settings.ScrapePageUrl);
		
		// Find page script
		var scriptUrl = EpiScriptRegex().Match(webPageHtml).Groups["url"].Value;
		if (string.IsNullOrWhiteSpace(scriptUrl) || !Uri.IsWellFormedUriString(scriptUrl, UriKind.Absolute))
			throw new Exception("Could not find the epi-es2015.js script URL");

		Log.Information("Found epi JS script: {Url}", scriptUrl);

		// Find API base URL in page script
		var js = await httpClient.GetStringAsync(scriptUrl);
		var apiBaseUrl = ApiBaseUrlRegex().Match(js).Groups["url"].Value.TrimEnd('/');
		if (string.IsNullOrWhiteSpace(apiBaseUrl))
			throw new Exception("Could not find the API base URL");

		Log.Information("API base URL: {ApiBaseUrl}", apiBaseUrl);

		// Find API key in page script
		var apiKey = TariffApiKeyRegex().Match(js).Groups["key"].Value;
		if (string.IsNullOrWhiteSpace(apiKey))
			throw new Exception("Could not find the API key");
		
		Log.Information("API key: {ApiKey}", apiKey);

		// Update known values
		SettingsProvider.Instance.Settings.KnownApiBaseUrl = apiBaseUrl;
		SettingsProvider.Instance.Settings.KnownApiKey = apiKey;
		
		// Really don't like this, but it'll do for now
		SettingsProvider.Instance.Settings.UseKnownValues = true;
		
		return (apiBaseUrl, apiKey);
	}

	[GeneratedRegex(@"src=""(?<url>https:\/\/cdn\.vattenfall\.nl\/vattenfallnlprd\/features\/epi\/(?:[^\/]*)\/epi-es2015\.js)""", RegexOptions.Compiled)]
	private static partial Regex EpiScriptRegex();
	
	[GeneratedRegex(@"dynamicTariffsBaseApiURL:""(?<url>[^""]*)", RegexOptions.Compiled)]
	private static partial Regex ApiBaseUrlRegex();
	
	[GeneratedRegex(@"ocpApimSubscriptionFeaturesDynamicTariffsKey:""(?<key>[^""]*)")]
	private static partial Regex TariffApiKeyRegex();

	public void Dispose()
	{
		_timer?.Dispose();
	}

	public async ValueTask DisposeAsync()
	{
		if (_timer != null)
			await _timer.DisposeAsync();
	}
}