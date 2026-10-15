using System.Globalization;

namespace VpnMonitor.Data.Statistics;

/// <summary>
/// Formatting for the UI. Speed uses decimal bit units like network equipment does (1 Mbit/s = 1 000 000 bit/s),
/// amounts of data use binary byte units like Windows does (1 MB = 1024 KB).
/// </summary>
public static class Units
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("ru-RU");

    private static readonly string[] ByteUnits = { "Б", "КБ", "МБ", "ГБ", "ТБ" };

    public static string Speed(double bitsPerSecond)
    {
        if (bitsPerSecond >= 1_000_000_000)
            return Format(bitsPerSecond / 1_000_000_000, "Гбит/с");

        if (bitsPerSecond >= 1_000_000)
            return Format(bitsPerSecond / 1_000_000, "Мбит/с");

        return Format(bitsPerSecond / 1_000, "Кбит/с");
    }

    public static string Bytes(double bytes)
    {
        var unit = 0;
        while (bytes >= 1024 && unit < ByteUnits.Length - 1)
        {
            bytes /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes:0} {ByteUnits[0]}" : Format(bytes, ByteUnits[unit]);
    }

    /// <summary>Three significant digits: 1,23 / 12,3 / 123.</summary>
    private static string Format(double value, string unit)
    {
        var format = value >= 100 || value == 0 ? "0" : value >= 10 ? "0.#" : "0.##";
        return value.ToString(format, Culture) + " " + unit;
    }
}
