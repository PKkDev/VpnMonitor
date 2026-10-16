using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VpnMonitor.Data.Context;
using VpnMonitor.Data.Entities;
using VpnMonitor.Data.Statistics;
using VpnMonitor.Service.Options;
using VpnMonitor.Service.Services;

namespace VpnMonitor.Service.Worker
{
    public class MonitorWorker : BackgroundService
    {
        private readonly IDbContextFactory<DataContext> _dbFactory;
        private readonly MonitorOption _option;
        private readonly ILogger<MonitorWorker> _logger;

        private readonly VpnClientParser _vpnClientParser = new();
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(24);
        private readonly TimeSpan _cleanupRetryDelay = TimeSpan.FromHours(1);

        // A large backlog (e.g. the first cleanup of a long history) is removed in portions over several polls,
        // so one statement does not hit the command timeout and polls are not delayed for long
        private const int CleanupBatchSize = 10_000;
        private const int CleanupBatchesPerPoll = 20;

        private TelnetClient? _client;
        private bool _migrated;
        private DateTime _lastCleanup = DateTime.MinValue;

        public MonitorWorker(
            IDbContextFactory<DataContext> dbFactory,
            IOptions<MonitorOption> option,
            ILogger<MonitorWorker> logger)
        {
            _dbFactory = dbFactory;
            _option = option.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Monitoring {Host}:{Port} every {Interval} s",
                _option.Host, _option.Port, _option.IntervalSeconds);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var delay = TimeSpan.FromSeconds(_option.IntervalSeconds);

                    try
                    {
                        await MigrateDatabase(stoppingToken);
                        await Poll(stoppingToken);
                        await CleanupOldRecords(stoppingToken);
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        // The connection is opened again on the next attempt
                        Disconnect();
                        delay = TimeSpan.FromSeconds(_option.RetryDelaySeconds);
                        _logger.LogError("Poll failed, retry in {Delay} s: {Message}", delay.TotalSeconds, ex.Message);
                        await RecordError("Опрос", ex, stoppingToken);
                    }

                    await Task.Delay(delay, stoppingToken);
                }
            }
            finally
            {
                Disconnect();
            }
        }

        private async Task MigrateDatabase(CancellationToken ct)
        {
            if (_migrated)
                return;

            await using (DataContext db = await _dbFactory.CreateDbContextAsync(ct))
            {
                await db.Database.MigrateAsync(ct);
            }

            _migrated = true;
        }

        private async Task Poll(CancellationToken ct)
        {
            if (_client == null)
            {
                var client = new TelnetClient(_option.Host, _option.Port);
                try
                {
                    await client.Connect(ct);
                }
                catch
                {
                    client.Dispose();
                    throw;
                }

                _client = client;
                _logger.LogInformation("Connected to OpenVPN");
            }

            var now = DateTime.UtcNow;
            var response = await _client.SendCommand("status", ct);
            var clients = _vpnClientParser.ParseClients(response, now);
            var title = _vpnClientParser.ParseTitle(response);
            _logger.LogInformation(
                "Active clients: {Count} - {Names}",
                clients.Count, string.Join(',', clients.Select(x => x.CommonName)));

            await using (DataContext db = await _dbFactory.CreateDbContextAsync(ct))
            {
                await using var transaction = await db.Database.BeginTransactionAsync(ct);

                // Before the new snapshots are added: the previous ones are needed for the traffic delta
                foreach (var client in clients)
                    await AddHourlyTraffic(db, client, ct);

                db.VpnClient.AddRange(clients);
                db.MonitorPoll.Add(new MonitorPoll { PolledAt = now, ActiveClients = clients.Count });

                // Written even without clients, so "nobody is connected" differs from "the service is down"
                var heartbeat = await db.MonitorHeartbeat.FindAsync(new object[] { MonitorHeartbeat.SingleId }, ct);
                if (heartbeat == null)
                {
                    heartbeat = new MonitorHeartbeat { Id = MonitorHeartbeat.SingleId };
                    db.MonitorHeartbeat.Add(heartbeat);
                }

                heartbeat.LastPoll = now;
                heartbeat.ActiveClients = clients.Count;
                heartbeat.ServerTitle = title;

                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
        }

        private static async Task AddHourlyTraffic(DataContext db, VpnClient client, CancellationToken ct)
        {
            var previous = await db.VpnClient
                .Where(x => x.CommonName == client.CommonName)
                .OrderByDescending(x => x.Inserted)
                .Select(x => new { x.ConnectedSince, x.BytesSent, x.BytesReceived })
                .FirstOrDefaultAsync(ct);

            // Counters of another session are not comparable: they started from zero on reconnect
            if (previous != null && previous.ConnectedSince != client.ConnectedSince)
                previous = null;

            var sent = TrafficCalculator.Delta(client.BytesSent, previous?.BytesSent);
            var received = TrafficCalculator.Delta(client.BytesReceived, previous?.BytesReceived);
            if (sent == 0 && received == 0)
                return;

            var hour = TrafficCalculator.HourOf(client.Inserted);

            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO public."client-traffic-hourly" (common_name, hour, bytes_received, bytes_sent)
                VALUES ({client.CommonName}, {hour}, {received}, {sent})
                ON CONFLICT (common_name, hour) DO UPDATE SET
                    bytes_received = "client-traffic-hourly".bytes_received + EXCLUDED.bytes_received,
                    bytes_sent = "client-traffic-hourly".bytes_sent + EXCLUDED.bytes_sent
                """, ct);
        }

        private async Task CleanupOldRecords(CancellationToken ct)
        {
            if (_option.RetentionDays <= 0 || DateTime.UtcNow - _lastCleanup < _cleanupInterval)
                return;

            var cutoff = DateTime.UtcNow.AddDays(-_option.RetentionDays);

            try
            {
                await using (DataContext db = await _dbFactory.CreateDbContextAsync(ct))
                {
                    var deleted = 0;
                    var finished = false;

                    for (var batch = 0; batch < CleanupBatchesPerPoll && !finished; batch++)
                    {
                        // The latest record of every client is kept, so the client and its last connection stay known
                        var count = await db.Database.ExecuteSqlInterpolatedAsync($"""
                            DELETE FROM public."vpn-client"
                            WHERE id IN (
                                SELECT v.id
                                FROM public."vpn-client" AS v
                                WHERE v.inserted < {cutoff}
                                    AND EXISTS (
                                        SELECT 1
                                        FROM public."vpn-client" AS newer
                                        WHERE newer.common_name = v.common_name AND newer.inserted > v.inserted)
                                LIMIT {CleanupBatchSize})
                            """, ct);

                        deleted += count;
                        finished = count < CleanupBatchSize;
                    }

                    if (!finished)
                    {
                        // Due again on the next poll
                        _logger.LogInformation("Cleanup: {Deleted} old snapshots deleted, continues on the next poll", deleted);
                        return;
                    }

                    // Hourly traffic is kept forever: it is small and holds the long-term statistics
                    deleted += await db.MonitorPoll
                        .Where(x => x.PolledAt < cutoff)
                        .ExecuteDeleteAsync(ct);

                    _lastCleanup = DateTime.UtcNow;
                    _logger.LogInformation(
                        "Cleanup: {Deleted} records older than {Days} days deleted",
                        deleted, _option.RetentionDays);
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // Not on every poll: a failing cleanup would load the database every 2 minutes
                _lastCleanup = DateTime.UtcNow - _cleanupInterval + _cleanupRetryDelay;
                _logger.LogError("Cleanup failed, retry in {Delay} h: {Message}", _cleanupRetryDelay.TotalHours, ex.Message);
                await RecordError("Очистка", ex, ct);
            }
        }

        /// <summary>
        /// Saves the failure to the heartbeat, so the app shows why the service does not work without access to its log.
        /// Fails silently when the database itself is the problem: the log still has the error.
        /// </summary>
        private async Task RecordError(string operation, Exception ex, CancellationToken ct)
        {
            const int maxLength = 1000;

            var message = ex.InnerException == null ? ex.Message : $"{ex.Message} ({ex.InnerException.Message})";
            message = $"{operation}: {message}";
            if (message.Length > maxLength)
                message = message[..maxLength];

            try
            {
                await using (DataContext db = await _dbFactory.CreateDbContextAsync(ct))
                {
                    var now = DateTime.UtcNow;
                    await db.MonitorHeartbeat
                        .Where(x => x.Id == MonitorHeartbeat.SingleId)
                        .ExecuteUpdateAsync(x => x
                            .SetProperty(y => y.LastError, message)
                            .SetProperty(y => y.LastErrorAt, now), ct);
                }
            }
            catch (Exception recordEx) when (!ct.IsCancellationRequested)
            {
                _logger.LogDebug("Error not recorded: {Message}", recordEx.Message);
            }
        }

        private void Disconnect()
        {
            _client?.Dispose();
            _client = null;
        }
    }
}
