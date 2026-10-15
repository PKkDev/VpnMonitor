using Microsoft.EntityFrameworkCore;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VpnMonitor.Data.Context;
using VpnMonitor.Data.Statistics;
using VpnMonitor.WUI.Controls;
using VpnMonitor.WUI.MVVM.Model;
using VpnMonitor.WUI.Services;

namespace VpnMonitor.WUI.MVVM.ViewModel
{
    /// <param name="Share">Part of the biggest consumer, 0..100.</param>
    public record ClientTrafficItem(string Client, string Total, string Traffic, double Share);

    public class TrafficStatisticsViewModel : PageViewModel
    {
        private readonly IDbContextFactory<DataContext> _dbFactory;
        private readonly NavigationHelperService _navigationHelperService;

        public IReadOnlyList<Period> Periods { get; } = new[] { Period.Today, Period.Week, Period.Month, Period.AllTime };

        private Period _selectedPeriod = Period.Week;
        public Period SelectedPeriod
        {
            get => _selectedPeriod;
            set
            {
                if (value != null && SetProperty(ref _selectedPeriod, value))
                    _ = Refresh();
            }
        }

        private IReadOnlyList<ChartBar> _bars = Array.Empty<ChartBar>();
        public IReadOnlyList<ChartBar> Bars { get => _bars; private set => SetProperty(ref _bars, value); }

        private string _total = string.Empty;
        public string Total { get => _total; private set => SetProperty(ref _total, value); }

        private string _hint = string.Empty;
        public string Hint { get => _hint; private set => SetProperty(ref _hint, value); }

        private IReadOnlyList<ClientTrafficItem> _clients = Array.Empty<ClientTrafficItem>();
        public IReadOnlyList<ClientTrafficItem> Clients { get => _clients; private set => SetProperty(ref _clients, value); }

        public TrafficStatisticsViewModel(
            IDbContextFactory<DataContext> dbFactory,
            NavigationHelperService navigationHelperService,
            InfoBarService infoBarService)
            : base(infoBarService)
        {
            _dbFactory = dbFactory;
            _navigationHelperService = navigationHelperService;
        }

        public void OpenClient(string client)
        {
            _navigationHelperService.Navigate(NavigationHelperService.ClientDetails, client);
        }

        public Task Refresh()
        {
            return Run(LoadData);
        }

        private async Task LoadData(CancellationToken ct)
        {
            var nowLocal = DateTime.Now;
            var fromLocal = SelectedPeriod.FromLocal(nowLocal);
            var fromHour = fromLocal == null ? DateTime.MinValue : TrafficCalculator.HourOf(Formats.ToUtc(fromLocal.Value));

            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            List<Data.Entities.ClientTrafficHourly> rows;
            try
            {
                var query = db.ClientTrafficHourly.AsNoTracking();
                if (fromLocal != null)
                    query = query.Where(x => x.Hour >= fromHour);

                rows = await query.ToListAsync(ct);
                ct.ThrowIfCancellationRequested();
                Hint = rows.Count == 0 ? "За этот период трафика нет." : string.Empty;
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                rows = new List<Data.Entities.ClientTrafficHourly>();
                Hint = "Статистика трафика появится после обновления сервиса: он создаст таблицу и заполнит её из накопленной истории.";
            }

            // All the time starts with the first recorded hour
            var start = fromLocal ?? (rows.Count > 0 ? Formats.ToLocal(rows.Min(x => x.Hour)) : nowLocal);

            var buckets = TrafficBuckets.Group(rows, SelectedPeriod.Granularity, start, nowLocal, TimeZoneInfo.Local);
            Bars = Formats.TrafficBars(buckets, SelectedPeriod.Granularity);
            Total = Formats.Traffic(buckets.Sum(x => x.BytesSent), buckets.Sum(x => x.BytesReceived));

            var byClient = rows
                .GroupBy(x => x.CommonName)
                .Select(x => new { Client = x.Key, Sent = x.Sum(y => y.BytesSent), Received = x.Sum(y => y.BytesReceived) })
                .OrderByDescending(x => x.Sent + x.Received)
                .ToList();

            var max = Math.Max(1, byClient.Select(x => x.Sent + x.Received).DefaultIfEmpty(0).Max());
            Clients = byClient
                .Select(x => new ClientTrafficItem(
                    x.Client,
                    Units.Bytes(x.Sent + x.Received),
                    Formats.Traffic(x.Sent, x.Received),
                    100.0 * (x.Sent + x.Received) / max))
                .ToList();
        }
    }
}
