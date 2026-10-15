using VpnMonitor.Data.Entities;
using VpnMonitor.Data.Statistics;

namespace VpnMonitor.Tests;

public class TrafficBucketsTests
{
    private static readonly TimeZoneInfo Utc4 =
        TimeZoneInfo.CreateCustomTimeZone("UTC+4", TimeSpan.FromHours(4), "UTC+4", "UTC+4");

    private static ClientTrafficHourly Row(string client, DateTime hourUtc, long sent, long received)
    {
        return new ClientTrafficHourly { CommonName = client, Hour = hourUtc, BytesSent = sent, BytesReceived = received };
    }

    [Fact]
    public void Group_ByDay_UsesLocalDayBoundaries()
    {
        var rows = new[]
        {
            // 23:00 on Feb 9 and 00:00 on Feb 10 in UTC+4
            Row("a", new DateTime(2025, 2, 9, 19, 0, 0, DateTimeKind.Utc), 100, 10),
            Row("a", new DateTime(2025, 2, 9, 20, 0, 0, DateTimeKind.Utc), 200, 20),
        };

        var buckets = TrafficBuckets.Group(rows, Granularity.Day, new DateTime(2025, 2, 9), new DateTime(2025, 2, 10, 12, 0, 0), Utc4);

        Assert.Equal(2, buckets.Count);
        Assert.Equal(new TrafficBucket(new DateTime(2025, 2, 9), 100, 10), buckets[0]);
        Assert.Equal(new TrafficBucket(new DateTime(2025, 2, 10), 200, 20), buckets[1]);
    }

    [Fact]
    public void Group_SumsClientsAndFillsEmptyBuckets()
    {
        var rows = new[]
        {
            Row("a", new DateTime(2025, 2, 9, 8, 0, 0, DateTimeKind.Utc), 100, 10),
            Row("b", new DateTime(2025, 2, 9, 8, 0, 0, DateTimeKind.Utc), 50, 5),
            Row("a", new DateTime(2025, 2, 9, 10, 0, 0, DateTimeKind.Utc), 1, 1),
        };

        var buckets = TrafficBuckets.Group(rows, Granularity.Hour, new DateTime(2025, 2, 9, 12, 0, 0), new DateTime(2025, 2, 9, 14, 30, 0), Utc4);

        Assert.Equal(
            new[]
            {
                new TrafficBucket(new DateTime(2025, 2, 9, 12, 0, 0), 150, 15),
                new TrafficBucket(new DateTime(2025, 2, 9, 13, 0, 0), 0, 0),
                new TrafficBucket(new DateTime(2025, 2, 9, 14, 0, 0), 1, 1),
            },
            buckets);
    }

    [Fact]
    public void Group_ByMonth_StartsFromFirstDayOfMonth()
    {
        var rows = new[]
        {
            Row("a", new DateTime(2025, 1, 31, 21, 0, 0, DateTimeKind.Utc), 7, 0),
        };

        var buckets = TrafficBuckets.Group(rows, Granularity.Month, new DateTime(2025, 1, 15), new DateTime(2025, 3, 1), Utc4);

        Assert.Equal(
            new[]
            {
                new TrafficBucket(new DateTime(2025, 1, 1), 0, 0),
                new TrafficBucket(new DateTime(2025, 2, 1), 7, 0),
                new TrafficBucket(new DateTime(2025, 3, 1), 0, 0),
            },
            buckets);
    }

    [Fact]
    public void Total_SumsClientsAtEveryPoll()
    {
        var session = new DateTime(2025, 2, 9, 16, 0, 0, DateTimeKind.Utc);
        var start = new DateTime(2025, 2, 9, 17, 0, 0, DateTimeKind.Utc);

        VpnClient Sample(string client, int seconds, long sent) => new()
        {
            CommonName = client,
            ConnectedSince = session,
            Inserted = start.AddSeconds(seconds),
            BytesSent = sent,
        };

        var total = ClientSpeedCalculator.Total(new[]
        {
            Sample("a", 0, 0), Sample("a", 120, 15_000_000),
            Sample("b", 0, 0), Sample("b", 120, 30_000_000),
        });

        var point = Assert.Single(total);
        Assert.Equal(3_000_000, point.SentBps);
        Assert.True(point.StartsSegment);
    }
}
