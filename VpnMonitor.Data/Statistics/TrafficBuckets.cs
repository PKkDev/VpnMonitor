using VpnMonitor.Data.Entities;

namespace VpnMonitor.Data.Statistics;

public enum Granularity
{
    Hour,
    Day,
    Month,
}

/// <param name="Start">Start of the bucket in the local time zone.</param>
public record TrafficBucket(DateTime Start, long BytesSent, long BytesReceived)
{
    public long Total => BytesSent + BytesReceived;
}

public static class TrafficBuckets
{
    /// <summary>
    /// Sums hourly traffic into local hours, days or months between <paramref name="fromLocal"/> and <paramref name="toLocal"/>.
    /// Buckets without traffic are included with zeros, so a chart shows them.
    /// </summary>
    public static List<TrafficBucket> Group(
        IEnumerable<ClientTrafficHourly> rows,
        Granularity granularity,
        DateTime fromLocal,
        DateTime toLocal,
        TimeZoneInfo zone)
    {
        var sums = rows
            .GroupBy(x => Truncate(TimeZoneInfo.ConvertTimeFromUtc(x.Hour, zone), granularity))
            .ToDictionary(
                x => x.Key,
                x => (Sent: x.Sum(y => y.BytesSent), Received: x.Sum(y => y.BytesReceived)));

        var result = new List<TrafficBucket>();
        for (var start = Truncate(fromLocal, granularity); start <= toLocal; start = Next(start, granularity))
        {
            sums.TryGetValue(start, out var sum);
            result.Add(new TrafficBucket(start, sum.Sent, sum.Received));
        }

        return result;
    }

    public static DateTime Truncate(DateTime time, Granularity granularity)
    {
        return granularity switch
        {
            Granularity.Hour => new DateTime(time.Year, time.Month, time.Day, time.Hour, 0, 0),
            Granularity.Day => new DateTime(time.Year, time.Month, time.Day),
            _ => new DateTime(time.Year, time.Month, 1),
        };
    }

    public static DateTime Next(DateTime start, Granularity granularity)
    {
        return granularity switch
        {
            Granularity.Hour => start.AddHours(1),
            Granularity.Day => start.AddDays(1),
            _ => start.AddMonths(1),
        };
    }
}
