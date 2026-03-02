using System.Text.Json.Serialization;

namespace VattenfallDynamicPriceApi.Models.Vattenfall;

public class TariffData
{
	[JsonPropertyName("startTime")]
	public DateTimeOffset StartTime { get; set; }

	[JsonPropertyName("endTime")]
	public DateTimeOffset EndTime { get; set; }

	[JsonPropertyName("isMissingPeriod")]
	public bool IsMissingPeriod { get; set; }

	[JsonPropertyName("cheapestOfDay")]
	public bool CheapestOfDay { get; set; }

	[JsonPropertyName("amountInclVat")]
	public decimal AmountInclVat { get; set; }

	[JsonPropertyName("amountExclVat")]
	public decimal AmountExclVat { get; set; }

	[JsonPropertyName("details")]
	public List<Detail> Details { get; set; } = [];
}