namespace VattenfallDynamicPriceApi.Extensions;

public static class DateTimeExtension
{
	public static DateTime ToUtcKeepTimeAsIs(this DateTime dateTime)
		=> new(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second, DateTimeKind.Utc);
}