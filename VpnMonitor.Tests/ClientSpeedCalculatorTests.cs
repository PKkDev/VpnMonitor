using VpnMonitor.Data.Entities;
using VpnMonitor.Data.Statistics;

namespace VpnMonitor.Tests;

public class ClientSpeedCalculatorTests
{
    private static readonly DateTime Session = new(2025, 2, 9, 16, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Start = new(2025, 2, 9, 17, 0, 0, DateTimeKind.Utc);

    private static VpnClient Sample(DateTime connectedSince, int insertedAfterSeconds, long sent, long received)
    {
        return new VpnClient
        {
            CommonName = "client",
            ConnectedSince = connectedSince,
            Inserted = Start.AddSeconds(insertedAfterSeconds),
            BytesSent = sent.ToString(),
            BytesReceived = received.ToString(),
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
    public void Calculate_ConvertsByteDeltaToMbitPerSecond()
    {
        // 15 728 640 bytes in 120 s = 1 Mbit/s, 31 457 280 bytes in 120 s = 2 Mbit/s
        var speed = ClientSpeedCalculator.Calculate(new[]
        {
            Sample(Session, 0, 1000, 5000),
            Sample(Session, 120, 1000 + 15_728_640, 5000 + 31_457_280),
        });

        Assert.NotNull(speed);
        Assert.Equal(1, speed.SentMbit);
        Assert.Equal(2, speed.ReceivedMbit);
        Assert.Equal(Session, speed.ConnectedSince);
    }

    [Fact]
    public void Calculate_UsesTwoLatestSamples_RegardlessOfInputOrder()
    {
        var speed = ClientSpeedCalculator.Calculate(new[]
        {
            Sample(Session, 240, 15_728_640, 0),
            Sample(Session, 0, 0, 0),
            Sample(Session, 120, 0, 0),
        });

        Assert.NotNull(speed);
        Assert.Equal(1, speed.SentMbit);
        Assert.Equal(0, speed.ReceivedMbit);
    }

    [Fact]
    public void Calculate_RoundsToTwoDecimals()
    {
        var speed = ClientSpeedCalculator.Calculate(new[]
        {
            Sample(Session, 0, 0, 0),
            Sample(Session, 120, 5_000_000, 0),
        });

        Assert.NotNull(speed);
        Assert.Equal(0.32, speed.SentMbit);
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
            Sample(reconnected, 360, 15_728_640, 0),
        });

        Assert.NotNull(speed);
        Assert.Equal(1, speed.SentMbit);
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
}
