using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Dispatching;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using VpnMonitor.Data.Context;
using VpnMonitor.Data.Entities;
using VpnMonitor.Data.Statistics;
using VpnMonitor.WUI.MVVM.Model;

namespace VpnMonitor.WUI.Services
{
    /// <summary>
    /// Current state of the clients, refreshed by a timer while the app runs, also in the tray.
    /// Raises notifications when clients connect or disconnect and when the monitoring service stops sending data.
    /// </summary>
    public class ClientStatusService : ObservableObject
    {
        public static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(60);

        /// <summary>Without a poll for this long the service is considered not working.</summary>
        public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(10);

        private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(200);

        private readonly IDbContextFactory<DataContext> _dbFactory;
        private readonly NotificationService _notifications;
        private readonly InfoBarService _infoBarService;

        private DispatcherQueueTimer? _timer;
        private DateTime _nextRefresh;
        private string? _lastError;

        // Online clients from the last refresh with fresh data, null before the first one
        private HashSet<string>? _knownOnline;
        private bool _staleNotified;

        private ObservableCollection<VpnClientActualDataResult> _clients = new();
        public ObservableCollection<VpnClientActualDataResult> Clients { get => _clients; private set => SetProperty(ref _clients, value); }

        private DateTime? _lastInserted;
        public DateTime? LastInserted
        {
            get => _lastInserted;
            private set
            {
                if (SetProperty(ref _lastInserted, value))
                    OnPropertyChanged(nameof(LastInsertedText));
            }
        }

        public string LastInsertedText => LastInserted?.ToString("dd.MM.yyyy HH:mm:ss") ?? "—";

        private string _serviceWarning = string.Empty;
        public string ServiceWarning { get => _serviceWarning; private set => SetProperty(ref _serviceWarning, value); }

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }

        /// <summary>Time until the next refresh, 0..100.</summary>
        private double _progress;
        public double Progress { get => _progress; private set => SetProperty(ref _progress, value); }

        /// <summary>Short status for the tray icon tooltip.</summary>
        public event Action<string>? StatusChanged;

        public ClientStatusService(IDbContextFactory<DataContext> dbFactory, NotificationService notifications, InfoBarService infoBarService)
        {
            _dbFactory = dbFactory;
            _notifications = notifications;
            _infoBarService = infoBarService;
        }

        /// <summary>Must be called on the UI thread.</summary>
        public void Start()
        {
            _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            _timer.Interval = TickInterval;
            _timer.Tick += (_, _) => OnTick();
            _timer.Start();

            _ = RefreshAsync();
        }

        private void OnTick()
        {
            if (IsLoading)
                return;

            var left = _nextRefresh - DateTime.UtcNow;
            if (left <= TimeSpan.Zero)
            {
                _ = RefreshAsync();
                return;
            }

            Progress = 100 * (1 - left / RefreshInterval);
        }

        public async Task RefreshAsync()
        {
            if (IsLoading)
                return;

            IsLoading = true;

            try
            {
                await Load();
                _lastError = null;
            }
            catch (Exception ex)
            {
                var message = $"Не удалось загрузить данные: {DbErrors.Describe(ex)}";

                // Refreshes go on in the background: the same error is shown once
                if (message != _lastError)
                    _infoBarService.Show(message);

                _lastError = message;
                StatusChanged?.Invoke("Нет связи с базой данных");
            }
            finally
            {
                IsLoading = false;
                Progress = 0;
                _nextRefresh = DateTime.UtcNow + RefreshInterval;
            }
        }

        private async Task Load()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var lastByClient = await db.VpnClient
                .GroupBy(x => x.CommonName)
                .Select(x => new
                {
                    CommonName = x.Key,
                    LastConnected = x.Max(y => y.ConnectedSince),
                    LastSeen = x.Max(y => y.Inserted),
                })
                .ToListAsync();

            var start = DateTime.UtcNow - StaleAfter;
            var recent = await db.VpnClient
                .AsNoTracking()
                .Where(x => x.Inserted >= start)
                .ToListAsync();

            var recentByClient = recent.ToLookup(x => x.CommonName);

            var heartbeat = await GetHeartbeat(db);
            var isStale = heartbeat != null && DateTime.UtcNow - heartbeat.LastPoll > StaleAfter;

            var clients = new List<VpnClientActualDataResult>();
            foreach (var last in lastByClient)
            {
                var speed = ClientSpeedCalculator.Calculate(recentByClient[last.CommonName]);

                // With the heartbeat a client is online when it was in the latest poll.
                // Without it (an older service) only by having two recent samples.
                var isOnline = heartbeat != null
                    ? !isStale && last.LastSeen == heartbeat.LastPoll
                    : speed != null;

                var client = new VpnClientActualDataResult(last.CommonName)
                {
                    IsOnline = isOnline,
                    LastConnected = ToLocal(last.LastConnected),
                    LastSeen = ToLocal(last.LastSeen),
                };

                if (isOnline)
                {
                    client.ConnectedTime = Math.Round((DateTime.UtcNow - last.LastConnected).TotalMinutes, 2);

                    if (speed != null)
                    {
                        client.DownloadSpeed = Units.Speed(speed.SentBps);
                        client.UploadSpeed = Units.Speed(speed.ReceivedBps);
                    }
                }

                clients.Add(client);
            }

            if (heartbeat != null)
            {
                LastInserted = ToLocal(heartbeat.LastPoll);
                ServiceWarning = isStale ? $"Сервис не обновлял данные больше {StaleAfter.TotalMinutes} минут" : string.Empty;
            }
            else if (lastByClient.Count > 0)
            {
                LastInserted = ToLocal(lastByClient.Max(x => x.LastSeen));
            }

            Clients = new(clients
                .OrderByDescending(x => x.IsOnline)
                .ThenByDescending(x => x.LastSeen));

            Notify(clients, heartbeat, isStale);

            var onlineCount = clients.Count(x => x.IsOnline);
            StatusChanged?.Invoke(isStale ? "Сервис не отвечает" : $"Онлайн: {onlineCount} из {clients.Count}");
        }

        private void Notify(List<VpnClientActualDataResult> clients, MonitorHeartbeat? heartbeat, bool isStale)
        {
            if (isStale)
            {
                // Data is old: per-client changes are unknown, everyone would look disconnected
                if (!_staleNotified)
                    _notifications.Show("Сервис мониторинга не отвечает", $"Последние данные: {ToLocal(heartbeat!.LastPoll):dd.MM.yyyy HH:mm}");

                _staleNotified = true;
                return;
            }

            if (_staleNotified)
            {
                _notifications.Show("Сервис мониторинга снова работает", "Данные о клиентах обновляются");
                _staleNotified = false;
            }

            var online = clients.Where(x => x.IsOnline).Select(x => x.Client).ToHashSet();

            // No notifications for the state found on start
            if (_knownOnline != null)
            {
                var connected = online.Except(_knownOnline).Order().ToList();
                var disconnected = _knownOnline.Except(online).Order().ToList();

                if (connected.Count > 0 || disconnected.Count > 0)
                {
                    var lines = new List<string>();
                    if (connected.Count > 0)
                        lines.Add($"Подключились: {string.Join(", ", connected)}");
                    if (disconnected.Count > 0)
                        lines.Add($"Отключились: {string.Join(", ", disconnected)}");

                    var title = connected.Count + disconnected.Count == 1
                        ? connected.Count == 1 ? $"{connected[0]} подключился" : $"{disconnected[0]} отключился"
                        : "Изменения подключений";

                    _notifications.Show(title, string.Join("\n", lines));
                }
            }

            _knownOnline = online;
        }

        private static async Task<MonitorHeartbeat?> GetHeartbeat(DataContext db)
        {
            try
            {
                return await db.MonitorHeartbeat.AsNoTracking().FirstOrDefaultAsync();
            }
            catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.UndefinedColumn)
            {
                // The database is not migrated yet: the service version that writes the heartbeat has not been started
                return null;
            }
        }

        private static DateTime ToLocal(DateTime utc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.Local);
        }
    }
}
