using System.Text.Json.Serialization;

namespace VattenfallDynamicPriceApi.Models.Vattenfall;

public class AverageTariff
{
	[JsonPropertyName("date")]
	public DateTimeOffset Date { get; set; }

	[JsonPropertyName("amountInclVat")]
	public decimal AmountInclVat { get; set; }

	[JsonPropertyName("amountExclVat")]
	public decimal AmountExclVat { get; set; }
}