using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VpnMonitor.Data.Context;
using VpnMonitor.Service.Options;
using VpnMonitor.Service.Worker;

namespace VpnMonitor.Tests;

public class MonitorWorkerTests
{
    private class ErrorCounter : ILogger<MonitorWorker>
    {
        public int Errors;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
                Interlocked.Increment(ref Errors);
        }
    }

    [Fact]
    public async Task Worker_WhenDatabaseIsUnavailable_KeepsRetryingAndStopsOnRequest()
    {
        // Nothing listens on port 1, so every attempt fails
        var services = new ServiceCollection()
            .AddDbContextFactory<DataContext>(options => options.UseNpgsql("Host=127.0.0.1;Port=1;Database=x;Timeout=1"))
            .BuildServiceProvider();

        var logger = new ErrorCounter();
        var option = Microsoft.Extensions.Options.Options.Create(new MonitorOption { RetryDelaySeconds = 0 });
        using var worker = new MonitorWorker(services.GetRequiredService<IDbContextFactory<DataContext>>(), option, logger);

        await worker.StartAsync(CancellationToken.None);

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (logger.Errors < 2 && DateTime.UtcNow < deadline)
            await Task.Delay(100);

        await worker.StopAsync(CancellationToken.None);

        Assert.True(logger.Errors >= 2, "The worker did not retry after a failure");
        Assert.NotNull(worker.ExecuteTask);
        Assert.False(worker.ExecuteTask.IsFaulted);
    }
}
