namespace Notifier.Calls.Domain;

public static class SpainTime
{
    private static readonly TimeZoneInfo SpainTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");

    public static DateTime NormalizeUtc(DateTime date)
    {
        if (date.Kind == DateTimeKind.Unspecified)
        {
            return DateTime.SpecifyKind(date, DateTimeKind.Utc);
        }

        if (date.Kind == DateTimeKind.Local)
        {
            return date.ToUniversalTime();
        }

        return date;
    }

    public static DateTime ToSpainTime(DateTime utcDate)
    {
        var utc = NormalizeUtc(utcDate);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, SpainTimeZone);
    }
}
