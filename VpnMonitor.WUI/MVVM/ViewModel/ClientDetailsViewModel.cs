using Microsoft.EntityFrameworkCore;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VpnMonitor.Data.Context;
using VpnMonitor.Data.Entities;
using VpnMonitor.Data.Statistics;
using VpnMonitor.WUI.Controls;
using VpnMonitor.WUI.MVVM.Model;
using VpnMonitor.WUI.Services;

namespace VpnMonitor.WUI.MVVM.ViewModel
{
    public record SessionItem(string Time, string Duration, string Traffic, string Address, bool IsCurrent);

    public record AddressItem(string Address, string Usage);

    public class ClientDetailsViewModel : PageViewModel
    {
        private readonly IDbContextFactory<DataContext> _dbFactory;
        private readonly ClientStatusService _clientStatus;

        public IReadOnlyList<Period> Periods { get; } = new[] { Period.Day, Period.Week, Period.Month };

        private Period _selectedPeriod = Period.Day;
        public Period SelectedPeriod
        {
            get => _selectedPeriod;
            set
            {
                if (value != null && SetProperty(ref _selectedPeriod, value))
                    _ = Refresh();
            }
        }

        private string _clientName = string.Empty;
        public string ClientName { get => _clientName; private set => SetProperty(ref _clientName, value); }

        private string _statusText = string.Empty;
        public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

        private string _statusColor = "Gray";
        public string StatusColor { get => _statusColor; private set => SetProperty(ref _statusColor, value); }

        private string _lastConnectedText = string.Empty;
        public string LastConnectedText { get => _lastConnectedText; private set => SetProperty(ref _lastConnectedText, value); }

        private IReadOnlyList<ChartSeries> _speedSeries = Array.Empty<ChartSeries>();
        public IReadOnlyList<ChartSeries> SpeedSeries { get => _speedSeries; private set => SetProperty(ref _speedSeries, value); }

        private DateTime _rangeStart = DateTime.UtcNow.AddDays(-1);
        public DateTime RangeStart { get => _rangeStart; private set => SetProperty(ref _rangeStart, value); }

        private DateTime _rangeEnd = DateTime.UtcNow;
        public DateTime RangeEnd { get => _rangeEnd; private set => SetProperty(ref _rangeEnd, value); }

        private IReadOnlyList<ChartBar> _trafficBars = Array.Empty<ChartBar>();
        public IReadOnlyList<ChartBar> TrafficBars { get => _trafficBars; private set => SetProperty(ref _trafficBars, value); }

        private string _trafficTotal = string.Empty;
        public string TrafficTotal { get => _trafficTotal; private set => SetProperty(ref _trafficTotal, value); }

        private string _trafficHint = string.Empty;
        public string TrafficHint { get => _trafficHint; private set => SetProperty(ref _trafficHint, value); }

        private IReadOnlyList<SessionItem> _sessions = Array.Empty<SessionItem>();
        public IReadOnlyList<SessionItem> Sessions { get => _sessions; private set => SetProperty(ref _sessions, value); }

        private IReadOnlyList<AddressItem> _addresses = Array.Empty<AddressItem>();
        public IReadOnlyList<AddressItem> Addresses { get => _addresses; private set => SetProperty(ref _addresses, value); }

        public ClientDetailsViewModel(IDbContextFactory<DataContext> dbFactory, ClientStatusService clientStatus, InfoBarService infoBarService)
            : base(infoBarService)
        {
            _dbFactory = dbFactory;
            _clientStatus = clientStatus;
        }

        public Task Load(string clientName)
        {
            ClientName = clientName;
            return Refresh();
        }

        public Task Refresh()
        {
            return Run(LoadData);
        }

        private async Task LoadData(CancellationToken ct)
        {
            if (string.IsNullOrEmpty(ClientName))
                return;

            var nowLocal = DateTime.Now;
            var fromLocal = SelectedPeriod.FromLocal(nowLocal)!.Value;
            var fromUtc = Formats.ToUtc(fromLocal);
            var nowUtc = DateTime.UtcNow;

            var current = _clientStatus.Clients.FirstOrDefault(x => x.Client == ClientName);
            var isOnline = current?.IsOnline == true;

            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var last = await db.VpnClient
                .Where(x => x.CommonName == ClientName)
                .OrderByDescending(x => x.Inserted)
                .Select(x => new { x.ConnectedSince, x.Inserted })
                .FirstOrDefaultAsync(ct);
            ct.ThrowIfCancellationRequested();

            StatusColor = isOnline ? "Green" : "Gray";
            StatusText = isOnline
                ? $"В сети {Formats.Duration(nowUtc - last!.ConnectedSince)}   ↓ {current!.DownloadSpeed}   ↑ {current.UploadSpeed}"
                : last != null ? $"Не в сети с {Formats.ToLocal(last.Inserted):dd.MM.yyyy HH:mm}" : "Нет данных";
            LastConnectedText = last != null ? $"Последнее подключение: {Formats.ToLocal(last.ConnectedSince):dd.MM.yyyy HH:mm}" : string.Empty;

            var samples = await db.VpnClient
                .Where(x => x.CommonName == ClientName && x.Inserted >= fromUtc)
                .Select(x => new VpnClient
                {
                    CommonName = x.CommonName,
                    ConnectedSince = x.ConnectedSince,
                    Inserted = x.Inserted,
                    BytesSent = x.BytesSent,
                    BytesReceived = x.BytesReceived,
                })
                .ToListAsync(ct);
            ct.ThrowIfCancellationRequested();

            var series = ClientSpeedCalculator.Series(samples);
            RangeStart = fromUtc;
            RangeEnd = nowUtc;
            SpeedSeries = new[]
            {
                new ChartSeries("Скачивание", ChartColors.Download, series.Select(x => new ChartPoint(x.Time, x.SentBps, x.StartsSegment)).ToList()),
                new ChartSeries("Отдача", ChartColors.Upload, series.Select(x => new ChartPoint(x.Time, x.ReceivedBps, x.StartsSegment)).ToList()),
            };

            var sessions = await db.VpnClient
                .Where(x => x.CommonName == ClientName && x.Inserted >= fromUtc)
                .GroupBy(x => new { x.ConnectedSince, x.RealAddress })
                .Select(x => new
                {
                    x.Key.ConnectedSince,
                    x.Key.RealAddress,
                    LastSeen = x.Max(y => y.Inserted),
                    Sent = x.Max(y => y.BytesSent),
                    Received = x.Max(y => y.BytesReceived),
                })
                .OrderByDescending(x => x.ConnectedSince)
                .ToListAsync(ct);
            ct.ThrowIfCancellationRequested();

            Sessions = sessions
                .Select(x =>
                {
                    var isCurrent = isOnline && x.ConnectedSince == last?.ConnectedSince;
                    var start = Formats.ToLocal(x.ConnectedSince);
                    var end = isCurrent ? "сейчас" : Formats.ToLocal(x.LastSeen).ToString(Formats.ToLocal(x.LastSeen).Date == start.Date ? "HH:mm" : "dd.MM HH:mm");

                    return new SessionItem(
                        $"{start:dd.MM HH:mm} — {end}",
                        Formats.Duration((isCurrent ? nowUtc : x.LastSeen) - x.ConnectedSince),
                        Formats.Traffic(x.Sent, x.Received),
                        Formats.Host(x.RealAddress),
                        isCurrent);
                })
                .ToList();

            Addresses = sessions
                .GroupBy(x => Formats.Host(x.RealAddress))
                .Select(x => new
                {
                    Address = x.Key,
                    Count = x.Count(),
                    LastSeen = x.Max(y => y.LastSeen),
                })
                .OrderByDescending(x => x.LastSeen)
                .Select(x => new AddressItem(x.Address, $"Сессий: {x.Count}, последняя {Formats.ToLocal(x.LastSeen):dd.MM.yyyy HH:mm}"))
                .ToList();

            await LoadTraffic(db, fromLocal, fromUtc, nowLocal, ct);
        }

        private async Task LoadTraffic(DataContext db, DateTime fromLocal, DateTime fromUtc, DateTime nowLocal, CancellationToken ct)
        {
            // Hours are whole: the first one may have started before the period
            var fromHour = TrafficCalculator.HourOf(fromUtc);

            List<ClientTrafficHourly> rows;
            try
            {
                rows = await db.ClientTrafficHourly
                    .AsNoTracking()
                    .Where(x => x.CommonName == ClientName && x.Hour >= fromHour)
                    .ToListAsync(ct);
                ct.ThrowIfCancellationRequested();
                TrafficHint = string.Empty;
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                rows = new List<ClientTrafficHourly>();
                TrafficHint = "Статистика трафика появится после обновления сервиса.";
            }

            var buckets = TrafficBuckets.Group(rows, SelectedPeriod.Granularity, fromLocal, nowLocal, TimeZoneInfo.Local);
            TrafficBars = Formats.TrafficBars(buckets, SelectedPeriod.Granularity);
            TrafficTotal = Formats.Traffic(buckets.Sum(x => x.BytesSent), buckets.Sum(x => x.BytesReceived));
        }
    }
}
