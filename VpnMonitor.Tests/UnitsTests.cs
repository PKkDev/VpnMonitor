using VpnMonitor.Data.Statistics;

namespace VpnMonitor.Tests;

public class UnitsTests
{
    [Theory]
    [InlineData(0, "0 Кбит/с")]
    [InlineData(500, "0,5 Кбит/с")]
    [InlineData(12_345, "12,3 Кбит/с")]
    [InlineData(999_000, "999 Кбит/с")]
    [InlineData(1_500_000, "1,5 Мбит/с")]
    [InlineData(36_260_000, "36,3 Мбит/с")]
    [InlineData(250_000_000, "250 Мбит/с")]
    [InlineData(1_200_000_000, "1,2 Гбит/с")]
    public void Speed_PicksUnitByMagnitude(double bitsPerSecond, string expected)
    {
        Assert.Equal(expected, Units.Speed(bitsPerSecond));
    }

    [Theory]
    [InlineData(0, "0 Б")]
    [InlineData(512, "512 Б")]
    [InlineData(1536, "1,5 КБ")]
    [InlineData(10 * 1024 * 1024, "10 МБ")]
    [InlineData(5L * 1024 * 1024 * 1024, "5 ГБ")]
    public void Bytes_PicksUnitByMagnitude(double bytes, string expected)
    {
        Assert.Equal(expected, Units.Bytes(bytes));
    }
}
