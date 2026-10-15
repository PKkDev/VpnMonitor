using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VpnMonitor.Data.Context;
using VpnMonitor.Service.Options;
using VpnMonitor.Service.Worker;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // "dotnet ef" runs Main up to Build() to get DataContext, so nothing before it may touch the database or OpenVPN
        var builder = Host.CreateApplicationBuilder(args);

        builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.Private.json"), optional: false);

        var connectionString = builder.Configuration.GetSection("NpgSql").GetValue<string>("ConnectionString");
        if (string.IsNullOrEmpty(connectionString))
            throw new Exception("Connection string is not found");

        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
        });
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

        builder.Services.Configure<MonitorOption>(builder.Configuration.GetSection("Monitor"));
        builder.Services.AddDbContextFactory<DataContext>(options => options.UseNpgsql(connectionString));
        builder.Services.AddHostedService<MonitorWorker>();

        using var host = builder.Build();

        await host.RunAsync();
    }
}
