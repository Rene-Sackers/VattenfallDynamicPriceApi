using Microsoft.Extensions.Primitives;
using VattenfallDynamicPriceApi.Models.Application;

namespace VattenfallDynamicPriceApi.Services;

public class SettingsProvider : IDisposable
{
	private const string SettingsFileName = "appsettings";

	private static readonly TimeSpan MinimumChangeInterval = TimeSpan.FromSeconds(1);

	public static SettingsProvider Instance { get; } = new();

	public SettingsData Settings { get; private set; }

	private readonly IDisposable _changeListener;

	private DateTime _lastChangeDate = DateTime.MinValue;

	public delegate void ConfigurationChangedHandler();

	public event ConfigurationChangedHandler? ConfigurationChanged;

	private SettingsProvider()
	{
		var configurationRoot = new ConfigurationBuilder()
			.AddJsonFile($"{SettingsFileName}.json", optional: true, reloadOnChange: true)
			.AddJsonFile($"{SettingsFileName}.Development.json", optional: true, reloadOnChange: true)
			.AddEnvironmentVariables(prefix: "VFAPI_")
			.Build();
		
		_changeListener = ChangeToken.OnChange(
			() => configurationRoot.GetReloadToken(),
			() => ConfigChanged(configurationRoot));

		Settings = configurationRoot.Get<SettingsData>() ?? new();
	}

	private void ConfigChanged(IConfiguration configurationRoot)
	{
		if (DateTime.Now.Subtract(_lastChangeDate) < MinimumChangeInterval)
			return;

		_lastChangeDate = DateTime.Now;
		
		Settings = configurationRoot.Get<SettingsData>()!;
		ConfigurationChanged?.Invoke();
	}

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		_changeListener?.Dispose();
	}
}