namespace VpnMonitor.Data.Statistics;

public static class TrafficCalculator
{
    /// <summary>
    /// Bytes transferred between two readings of a cumulative counter.
    /// Without a previous reading of the same session the whole counter is new traffic, since counters start from zero on connect.
    /// A decreasing counter means it was reset, so the current value is new traffic as well.
    /// </summary>
    public static long Delta(long current, long? previous)
    {
        if (previous == null || current < previous)
            return current;

        return current - previous.Value;
    }

    /// <summary>Start of the UTC hour the moment belongs to.</summary>
    public static DateTime HourOf(DateTime utc)
    {
        return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc);
    }
}
