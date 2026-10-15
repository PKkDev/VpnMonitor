using VpnMonitor.Data.Statistics;

namespace VpnMonitor.Tests;

public class TrafficCalculatorTests
{
    [Fact]
    public void Delta_WithoutPreviousReading_IsWholeCounter()
    {
        Assert.Equal(100, TrafficCalculator.Delta(100, null));
    }

    [Fact]
    public void Delta_IsDifferenceOfReadings()
    {
        Assert.Equal(50, TrafficCalculator.Delta(150, 100));
    }

    [Fact]
    public void Delta_WhenCounterWasReset_IsWholeCounter()
    {
        Assert.Equal(30, TrafficCalculator.Delta(30, 100));
    }

    [Fact]
    public void HourOf_TruncatesToHour()
    {
        var hour = TrafficCalculator.HourOf(new DateTime(2025, 2, 9, 17, 59, 59, 999, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2025, 2, 9, 17, 0, 0, DateTimeKind.Utc), hour);
        Assert.Equal(DateTimeKind.Utc, hour.Kind);
    }
}
