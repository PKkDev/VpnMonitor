using Microsoft.EntityFrameworkCore;
using VpnMonitor.Data.Entities;

namespace VpnMonitor.Data.Context;

/// <summary>
/// Применение миграций
/// dotnet ef database update --project VpnMonitor.Data --startup-project VpnMonitor.Service
/// </summary>
public class DataContext : DbContext
{
    public DbSet<VpnClient> VpnClient => Set<VpnClient>();

    public DbSet<MonitorHeartbeat> MonitorHeartbeat => Set<MonitorHeartbeat>();

    public DbSet<MonitorPoll> MonitorPoll => Set<MonitorPoll>();

    public DbSet<ClientTrafficHourly> ClientTrafficHourly => Set<ClientTrafficHourly>();

    public DataContext(DbContextOptions<DataContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
    }
}
