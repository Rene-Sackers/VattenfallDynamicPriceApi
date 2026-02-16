using VattenfallDynamicPriceApi.Models.Evcc;
using VattenfallDynamicPriceApi.Models.Vattenfall;

namespace VattenfallDynamicPriceApi.Services;

public interface IVattenfallDataService : IDisposable, IAsyncDisposable
{
	FlexTariffData[]? Data { get; }
	EvccApiHourlyData[]? EvccData { get; }
	Task InitializeAsync();
	decimal GetCurrentElectricityTariff();
	decimal GetCurrentGasTariff();
}