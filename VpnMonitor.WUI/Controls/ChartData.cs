using Microsoft.UI;
using System;
using System.Collections.Generic;
using VpnMonitor.Data.Statistics;
using Windows.UI;

namespace VpnMonitor.WUI.Controls
{
    public enum ChartValueKind
    {
        Count,
        Speed,
        Bytes,
    }

    /// <param name="Time">UTC.</param>
    /// <param name="StartsSegment">Not joined with the previous point: a gap in the data.</param>
    public readonly record struct ChartPoint(DateTime Time, double Value, bool StartsSegment = false);

    public sealed record ChartSeries(string Name, Color Color, IReadOnlyList<ChartPoint> Points);

    /// <param name="Values">Stacked parts of the bar, colored by <see cref="ChartColors.Stack"/>.</param>
    public sealed record ChartBar(string Label, IReadOnlyList<double> Values, string ToolTip);

    public static class ChartColors
    {
        public static readonly Color Download = Color.FromArgb(255, 0x2D, 0x7D, 0xD2);
        public static readonly Color Upload = Color.FromArgb(255, 0xF2, 0x8C, 0x28);
        public static readonly Color Online = Color.FromArgb(255, 0x10, 0x89, 0x3E);

        public static readonly Color[] Stack = { Download, Upload };

        public static Color Fallback => Colors.Gray;
    }

    public static class ChartFormat
    {
        public static string Value(double value, ChartValueKind kind)
        {
            return kind switch
            {
                ChartValueKind.Speed => Units.Speed(value),
                ChartValueKind.Bytes => Units.Bytes(value),
                _ => value.ToString("0.#"),
            };
        }

        /// <summary>The smallest of 1, 2, 5 × 10ⁿ that is not less than the value, for the top of an axis.</summary>
        public static double NiceCeiling(double value, ChartValueKind kind)
        {
            // Halves of the axis stay whole numbers
            if (kind == ChartValueKind.Count)
                return Math.Max(2, Math.Ceiling(value / 2) * 2);

            // An empty chart still gets readable labels: 1 KB, 1 Kbit/s
            value = Math.Max(value, kind == ChartValueKind.Bytes ? 1024 : 1000);

            // Byte axes look better on powers of 1024
            var step = kind == ChartValueKind.Bytes ? 1024.0 : 10.0;
            var magnitude = 1.0;
            while (magnitude * step <= value)
                magnitude *= step;

            foreach (var factor in kind == ChartValueKind.Bytes ? new[] { 1.0, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024 } : new[] { 1.0, 2, 5, 10 })
            {
                if (magnitude * factor >= value)
                    return magnitude * factor;
            }

            return magnitude * step;
        }
    }
}
