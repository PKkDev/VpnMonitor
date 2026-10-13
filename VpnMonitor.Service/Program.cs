using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VpnMonitor.Data.Context;
using VpnMonitor.Service.Worker;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // "dotnet ef" runs Main up to Build() to get DataContext, so nothing before it may touch the database or OpenVPN
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Configuration.AddJsonFile("appsettings.Private.json", optional: false);

        var connectionString = builder.Configuration.GetSection("NpgSql").GetValue<string>("ConnectionString");
        if (string.IsNullOrEmpty(connectionString))
            throw new Exception("Connection string is not found");

        var retentionDays = builder.Configuration.GetSection("Monitor").GetValue("RetentionDays", 30);

        builder.Services.AddDbContextFactory<DataContext>(options => options.UseNpgsql(connectionString));
        builder.Services.AddSingleton(services => new MonitorWorker(
            services.GetRequiredService<IDbContextFactory<DataContext>>(),
            retentionDays));

        using var host = builder.Build();

        try
        {
            using (DataContext db = host.Services.GetRequiredService<IDbContextFactory<DataContext>>().CreateDbContext())
            {
                db.Database.Migrate();
            }

            await host.Services.GetRequiredService<MonitorWorker>().Start();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }
}
