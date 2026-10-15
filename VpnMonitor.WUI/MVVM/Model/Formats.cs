using System;
using System.Collections.Generic;
using System.Linq;
using VpnMonitor.Data.Statistics;
using VpnMonitor.WUI.Controls;

namespace VpnMonitor.WUI.MVVM.Model
{
    public static class Formats
    {
        public static DateTime ToLocal(DateTime utc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.Local);
        }

        public static DateTime ToUtc(DateTime local)
        {
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZoneInfo.Local);
        }

        public static string Duration(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
                duration = TimeSpan.Zero;

            if (duration.TotalDays >= 1)
                return $"{(int)duration.TotalDays} дн. {duration.Hours} ч. {duration.Minutes} мин.";

            if (duration.TotalHours >= 1)
                return $"{duration.Hours} ч. {duration.Minutes} мин.";

            return $"{duration.Minutes} мин.";
        }

        /// <summary>"81.23.184.151:4169" → "81.23.184.151". Addresses with several colons (IPv6) are kept as is.</summary>
        public static string Host(string realAddress)
        {
            var colon = realAddress.LastIndexOf(':');
            return colon > 0 && realAddress.IndexOf(':') == colon ? realAddress[..colon] : realAddress;
        }

        public static string Traffic(long download, long upload)
        {
            return $"↓ {Units.Bytes(download)}   ↑ {Units.Bytes(upload)}";
        }

        public static List<ChartBar> TrafficBars(IEnumerable<TrafficBucket> buckets, Granularity granularity)
        {
            var (label, title) = granularity switch
            {
                Granularity.Hour => ("HH:mm", "dd.MM.yyyy HH:mm"),
                Granularity.Day => ("dd.MM", "dd.MM.yyyy"),
                _ => ("MM.yy", "MMMM yyyy"),
            };

            return buckets
                .Select(x => new ChartBar(
                    x.Start.ToString(label),
                    new double[] { x.BytesSent, x.BytesReceived },
                    $"{x.Start.ToString(title)}\nСкачано: {Units.Bytes(x.BytesSent)}\nОтдано: {Units.Bytes(x.BytesReceived)}"))
                .ToList();
        }
    }
}
