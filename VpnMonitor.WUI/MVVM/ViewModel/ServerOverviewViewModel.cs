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
    public class ServerOverviewViewModel : PageViewModel
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

        private string _serviceState = string.Empty;
        public string ServiceState { get => _serviceState; private set => SetProperty(ref _serviceState, value); }

        private string _serviceStateColor = "Gray";
        public string ServiceStateColor { get => _serviceStateColor; private set => SetProperty(ref _serviceStateColor, value); }

        private string _lastPoll = string.Empty;
        public string LastPoll { get => _lastPoll; private set => SetProperty(ref _lastPoll, value); }

        /// <summary>An older error is not shown once the service works again.</summary>
        private static readonly TimeSpan ShowErrorFor = TimeSpan.FromHours(24);

        private string _lastError = string.Empty;
        public string LastError { get => _lastError; private set => SetProperty(ref _lastError, value); }

        private string _lastErrorColor = "Gray";
        public string LastErrorColor { get => _lastErrorColor; private set => SetProperty(ref _lastErrorColor, value); }

        private bool _hasLastError;
        public bool HasLastError { get => _hasLastError; private set => SetProperty(ref _hasLastError, value); }

        private string _serverTitle = string.Empty;
        public string ServerTitle { get => _serverTitle; private set => SetProperty(ref _serverTitle, value); }

        private string _onlineNow = string.Empty;
        public string OnlineNow { get => _onlineNow; private set => SetProperty(ref _onlineNow, value); }

        private string _hint = string.Empty;
        public string Hint { get => _hint; private set => SetProperty(ref _hint, value); }

        private DateTime _rangeStart = DateTime.UtcNow.AddDays(-1);
        public DateTime RangeStart { get => _rangeStart; private set => SetProperty(ref _rangeStart, value); }

        private DateTime _rangeEnd = DateTime.UtcNow;
        public DateTime RangeEnd { get => _rangeEnd; private set => SetProperty(ref _rangeEnd, value); }

        private IReadOnlyList<ChartSeries> _onlineSeries = Array.Empty<ChartSeries>();
        public IReadOnlyList<ChartSeries> OnlineSeries { get => _onlineSeries; private set => SetProperty(ref _onlineSeries, value); }

        private IReadOnlyList<ChartSeries> _speedSeries = Array.Empty<ChartSeries>();
        public IReadOnlyList<ChartSeries> SpeedSeries { get => _speedSeries; private set => SetProperty(ref _speedSeries, value); }

        private string _speedTitle = string.Empty;
        public string SpeedTitle { get => _speedTitle; private set => SetProperty(ref _speedTitle, value); }

        private IReadOnlyList<ChartBar> _trafficBars = Array.Empty<ChartBar>();
        public IReadOnlyList<ChartBar> TrafficBars { get => _trafficBars; private set => SetProperty(ref _trafficBars, value); }

        private string _trafficTotal = string.Empty;
        public string TrafficTotal { get => _trafficTotal; private set => SetProperty(ref _trafficTotal, value); }

        public ServerOverviewViewModel(IDbContextFactory<DataContext> dbFactory, ClientStatusService clientStatus, InfoBarService infoBarService)
            : base(infoBarService)
        {
            _dbFactory = dbFactory;
            _clientStatus = clientStatus;
        }

        public Task Refresh()
        {
            return Run(LoadData);
        }

        private async Task LoadData(CancellationToken ct)
        {
            var nowLocal = DateTime.Now;
            var nowUtc = DateTime.UtcNow;
            var fromLocal = SelectedPeriod.FromLocal(nowLocal)!.Value;
            var fromUtc = Formats.ToUtc(fromLocal);

            RangeStart = fromUtc;
            RangeEnd = nowUtc;

            var clients = _clientStatus.Clients;
            OnlineNow = $"{clients.Count(x => x.IsOnline)} из {clients.Count}";

            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var hints = new List<string>();

            var heartbeat = await Optional(() => db.MonitorHeartbeat.AsNoTracking().FirstOrDefaultAsync(ct));
            ct.ThrowIfCancellationRequested();
            if (heartbeat == null)
            {
                ServiceState = "Нет данных от сервиса";
                ServiceStateColor = "Gray";
                LastPoll = "—";
                ServerTitle = "—";
                HasLastError = false;
            }
            else
            {
                var isStale = nowUtc - heartbeat.LastPoll > ClientStatusService.StaleAfter;
                ServiceState = isStale ? "Не отвечает" : "Работает";
                ServiceStateColor = isStale ? "OrangeRed" : "Green";
                LastPoll = $"{Formats.ToLocal(heartbeat.LastPoll):dd.MM.yyyy HH:mm:ss}";
                ServerTitle = heartbeat.ServerTitle ?? "появится после обновления сервиса";

                // After the last successful poll the error is current: highlighted
                var errorAt = heartbeat.LastErrorAt;
                var isCurrent = errorAt > heartbeat.LastPoll;
                HasLastError = errorAt != null && (isCurrent || nowUtc - errorAt < ShowErrorFor);
                LastError = errorAt != null ? $"{Formats.ToLocal(errorAt.Value):dd.MM.yyyy HH:mm:ss} — {heartbeat.LastError}" : string.Empty;
                LastErrorColor = isCurrent ? "OrangeRed" : "Gray";
            }

            var polls = await Optional(() => db.MonitorPoll
                .AsNoTracking()
                .Where(x => x.PolledAt >= fromUtc)
                .OrderBy(x => x.PolledAt)
                .ToListAsync(ct));
            ct.ThrowIfCancellationRequested();

            if (polls == null)
                hints.Add("История подключений и трафика появится после обновления сервиса.");

            OnlineSeries = new[]
            {
                new ChartSeries("Клиентов онлайн", ChartColors.Online, OnlinePoints(polls ?? new List<MonitorPoll>())),
            };

            var fromHour = TrafficCalculator.HourOf(fromUtc);
            var hourly = await Optional(() => db.ClientTrafficHourly
                .AsNoTracking()
                .Where(x => x.Hour >= fromHour)
                .ToListAsync(ct)) ?? new List<ClientTrafficHourly>();
            ct.ThrowIfCancellationRequested();

            // A day of snapshots gives the speed at every poll; for longer periods hourly averages are lighter and readable
            List<SpeedPoint> speed;
            if (SelectedPeriod.Granularity == Granularity.Hour)
            {
                SpeedTitle = "Суммарная скорость";
                var samples = await db.VpnClient
                    .Where(x => x.Inserted >= fromUtc)
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

                speed = ClientSpeedCalculator.Total(samples);
            }
            else
            {
                SpeedTitle = "Суммарная скорость, среднее за час";
                speed = TrafficBuckets.Group(hourly, Granularity.Hour, fromLocal, nowLocal, TimeZoneInfo.Local)
                    .Select((x, i) => new SpeedPoint(Formats.ToUtc(x.Start), x.BytesSent * 8 / 3600.0, x.BytesReceived * 8 / 3600.0, i == 0))
                    .ToList();
            }

            SpeedSeries = new[]
            {
                new ChartSeries("Скачивание", ChartColors.Download, speed.Select(x => new ChartPoint(x.Time, x.SentBps, x.StartsSegment)).ToList()),
                new ChartSeries("Отдача", ChartColors.Upload, speed.Select(x => new ChartPoint(x.Time, x.ReceivedBps, x.StartsSegment)).ToList()),
            };

            var buckets = TrafficBuckets.Group(hourly, SelectedPeriod.Granularity, fromLocal, nowLocal, TimeZoneInfo.Local);
            TrafficBars = Formats.TrafficBars(buckets, SelectedPeriod.Granularity);
            TrafficTotal = Formats.Traffic(buckets.Sum(x => x.BytesSent), buckets.Sum(x => x.BytesReceived));

            Hint = string.Join("\n", hints);
        }

        private static List<ChartPoint> OnlinePoints(List<MonitorPoll> polls)
        {
            var points = new List<ChartPoint>();
            DateTime? prev = null;

            foreach (var poll in polls)
            {
                points.Add(new ChartPoint(poll.PolledAt, poll.ActiveClients, prev == null || poll.PolledAt - prev > ClientSpeedCalculator.MaxGap));
                prev = poll.PolledAt;
            }

            return points;
        }

        /// <summary>Tables created by the latest migrations may be missing until the service is updated.</summary>
        private static async Task<T?> Optional<T>(Func<Task<T>> query) where T : class?
        {
            try
            {
                return await query();
            }
            catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.UndefinedColumn)
            {
                return null;
            }
        }
    }
}
