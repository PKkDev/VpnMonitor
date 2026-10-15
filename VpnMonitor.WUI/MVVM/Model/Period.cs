using System;
using VpnMonitor.Data.Statistics;

namespace VpnMonitor.WUI.MVVM.Model
{
    /// <summary>
    /// Period selectable on the chart pages, with the size of a bar for it.
    /// </summary>
    public sealed class Period
    {
        public static readonly Period Day = new("24 часа", now => now.AddHours(-24), Granularity.Hour);
        public static readonly Period Today = new("Сегодня", now => now.Date, Granularity.Hour);
        public static readonly Period Week = new("7 дней", now => now.Date.AddDays(-6), Granularity.Day);
        public static readonly Period Month = new("30 дней", now => now.Date.AddDays(-29), Granularity.Day);
        public static readonly Period AllTime = new("Всё время", _ => null, Granularity.Month);

        private readonly Func<DateTime, DateTime?> _fromLocal;

        public string Title { get; }

        public Granularity Granularity { get; }

        private Period(string title, Func<DateTime, DateTime?> fromLocal, Granularity granularity)
        {
            Title = title;
            _fromLocal = fromLocal;
            Granularity = granularity;
        }

        /// <summary>Local start of the period, null for all the data.</summary>
        public DateTime? FromLocal(DateTime nowLocal)
        {
            return _fromLocal(nowLocal);
        }

        public override string ToString()
        {
            return Title;
        }
    }
}
