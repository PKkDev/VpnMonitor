using VpnMonitor.Data.Entities;
using VpnMonitor.Data.Statistics;

namespace VpnMonitor.Tests;

public class ClientSpeedCalculatorTests
{
    private static readonly DateTime Session = new(2025, 2, 9, 16, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Start = new(2025, 2, 9, 17, 0, 0, DateTimeKind.Utc);

    // 15 000 000 bytes in 120 s = 1 000 000 bit/s
    private const long OneMbitFor120Seconds = 15_000_000;

    private static VpnClient Sample(DateTime connectedSince, int insertedAfterSeconds, long sent, long received)
    {
        return new VpnClient
        {
            CommonName = "client",
            ConnectedSince = connectedSince,
            Inserted = Start.AddSeconds(insertedAfterSeconds),
            BytesSent = sent,
            BytesReceived = received,
        };
    }

    [Fact]
    public void Calculate_WithoutSamples_ReturnsNull()
    {
        Assert.Null(ClientSpeedCalculator.Calculate(Array.Empty<VpnClient>()));
    }

    [Fact]
    public void Calculate_WithSingleSample_ReturnsNull()
    {
        Assert.Null(ClientSpeedCalculator.Calculate(new[] { Sample(Session, 0, 100, 100) }));
    }

    [Fact]
    public void Calculate_ConvertsByteDeltaToBitsPerSecond()
    {
        var speed = ClientSpeedCalculator.Calculate(new[]
        {
            Sample(Session, 0, 1000, 5000),
            Sample(Session, 120, 1000 + OneMbitFor120Seconds, 5000 + 2 * OneMbitFor120Seconds),
        });

        Assert.NotNull(speed);
        Assert.Equal(1_000_000, speed.SentBps);
        Assert.Equal(2_000_000, speed.ReceivedBps);
        Assert.Equal(Session, speed.ConnectedSince);
    }

    [Fact]
    public void Calculate_UsesTwoLatestSamples_RegardlessOfInputOrder()
    {
        var speed = ClientSpeedCalculator.Calculate(new[]
        {
            Sample(Session, 240, OneMbitFor120Seconds, 0),
            Sample(Session, 0, 0, 0),
            Sample(Session, 120, 0, 0),
        });

        Assert.NotNull(speed);
        Assert.Equal(1_000_000, speed.SentBps);
        Assert.Equal(0, speed.ReceivedBps);
    }

    [Fact]
    public void Calculate_AfterReconnect_IgnoresPreviousSession()
    {
        var reconnected = Session.AddHours(1);

        // Counters were reset by the reconnect: a delta across sessions would be negative
        var speed = ClientSpeedCalculator.Calculate(new[]
        {
            Sample(Session, 0, 900_000_000, 900_000_000),
            Sample(Session, 120, 900_000_000, 900_000_000),
            Sample(reconnected, 240, 0, 0),
            Sample(reconnected, 360, OneMbitFor120Seconds, 0),
        });

        Assert.NotNull(speed);
        Assert.Equal(1_000_000, speed.SentBps);
        Assert.Equal(reconnected, speed.ConnectedSince);
    }

    [Fact]
    public void Calculate_WhenLatestSessionHasSingleSample_ReturnsNull()
    {
        var speed = ClientSpeedCalculator.Calculate(new[]
        {
            Sample(Session, 0, 100, 100),
            Sample(Session, 120, 200, 200),
            Sample(Session.AddHours(1), 240, 0, 0),
        });

        Assert.Null(speed);
    }

    [Fact]
    public void Calculate_WhenSamplesHaveSameTime_ReturnsNull()
    {
        var speed = ClientSpeedCalculator.Calculate(new[]
        {
            Sample(Session, 0, 100, 100),
            Sample(Session, 0, 200, 200),
        });

        Assert.Null(speed);
    }

    [Fact]
    public void Series_HasPointForEveryPairOfConsecutiveSamples()
    {
        var series = ClientSpeedCalculator.Series(new[]
        {
            Sample(Session, 240, 3 * OneMbitFor120Seconds, 0),
            Sample(Session, 0, 0, 0),
            Sample(Session, 120, OneMbitFor120Seconds, 0),
        });

        Assert.Equal(2, series.Count);
        Assert.Equal(Start.AddSeconds(120), series[0].Time);
        Assert.Equal(1_000_000, series[0].SentBps);
        Assert.True(series[0].StartsSegment);
        Assert.Equal(Start.AddSeconds(240), series[1].Time);
        Assert.Equal(2_000_000, series[1].SentBps);
        Assert.False(series[1].StartsSegment);
    }

    [Fact]
    public void Series_AfterReconnect_StartsNewSegment()
    {
        var reconnected = Session.AddHours(1);

        var series = ClientSpeedCalculator.Series(new[]
        {
            Sample(Session, 0, 0, 0),
            Sample(Session, 120, OneMbitFor120Seconds, 0),
            Sample(reconnected, 240, 0, 0),
            Sample(reconnected, 360, OneMbitFor120Seconds, 0),
        });

        Assert.Equal(2, series.Count);
        Assert.True(series[0].StartsSegment);
        Assert.True(series[1].StartsSegment);
        Assert.Equal(1_000_000, series[1].SentBps);
    }

    [Fact]
    public void Series_AfterGapInData_StartsNewSegment()
    {
        var gap = (int)ClientSpeedCalculator.MaxGap.TotalSeconds + 120;

        var series = ClientSpeedCalculator.Series(new[]
        {
            Sample(Session, 0, 0, 0),
            Sample(Session, 120, 100, 0),
            Sample(Session, 120 + gap, 200, 0),
            Sample(Session, 240 + gap, 300, 0),
        });

        Assert.Equal(2, series.Count);
        Assert.True(series[0].StartsSegment);
        Assert.True(series[1].StartsSegment);
        Assert.Equal(Start.AddSeconds(240 + gap), series[1].Time);
    }
}
