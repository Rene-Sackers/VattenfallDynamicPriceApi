using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Serilog;
using VattenfallDynamicPriceApi.Extensions;
using VattenfallDynamicPriceApi.Models.Evcc;
using VattenfallDynamicPriceApi.Models.Vattenfall;

namespace VattenfallDynamicPriceApi.Services;

public partial class VattenfallDataService : IDisposable, IAsyncDisposable
{
	private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

	public FlexTariffData[]? Data { get; private set; } = [];
	
	public EvccApiHourlyData[]? EvccData { get; private set; } = [];
	
	private Timer? _timer;

	public async Task InitializeAsync()
	{
		Log.Information("Refresh interval: " + CacheDuration);
		
		await UpdateDataAsync();
		_timer = new Timer(RefreshTimerElapsed, null, CacheDuration, CacheDuration);
	}

	private decimal GetCurrentTariffForProductType(string productType, string description)
	{
		var productData = Data?.FirstOrDefault(d => d.Product == productType);
		if (productData == null)
		{
			Log.Error("Could not get current {Description} tariff, no data", description);
			return 999;
		}

		var now = DateTime.Now;
		var currentTariff = productData.TariffData.FirstOrDefault(d => d.StartTime <= now && d.EndTime >= now);
		if (currentTariff != null)
			return currentTariff.AmountInclVat;

		var highestTariff = productData.TariffData.Max(t => t.AmountInclVat);
		Log.Error("Could not get current {Description} tariff, no value found for current time, returning highest value: {HighestTariff}", description, highestTariff);

		return highestTariff;
	}

	public decimal GetCurrentElectricityTariff()
		=> GetCurrentTariffForProductType("E", "electricity");
	
	public decimal GetCurrentGasTariff()
		=> GetCurrentTariffForProductType("G", "gas");

	private void RefreshTimerElapsed(object? _)
	{
		try
		{
			Log.Information("Updating data");
			Task.Run(UpdateDataAsync).Wait();
			Log.Information("Updated data");
		}
		catch (Exception e)
		{
			Log.Error(e, "Failed to update data");
		}
	}

	private async Task UpdateDataAsync()
	{
		var (apiBaseUrl, apiKey) = await TryGetApiUrlAndKeyAsync();
		Data = await GetFlexTariffDataAsync(apiBaseUrl, apiKey);
		
		var electricityData = Data.FirstOrDefault(d => d.Product == "E");
		if (electricityData == null)
		{
			Log.Error("Could not find electricity data in API response");
			return;
		}
		
		EvccData = electricityData.TariffData.Select(td => new EvccApiHourlyData
			{
				Start = td.StartTime.ToUtcKeepTimeAsIs(),
				End = td.EndTime.ToUtcKeepTimeAsIs(),
				Value = td.AmountInclVat
			})
			.OrderBy(td => td.Start)
			.ToArray();
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