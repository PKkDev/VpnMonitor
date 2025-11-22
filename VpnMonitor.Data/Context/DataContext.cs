using Microsoft.EntityFrameworkCore;
using VpnMonitor.Data.Entities;

namespace VpnMonitor.Data.Context;

public class DataContext : DbContext
{
    public DbSet<VpnClient> VpnClient => Set<VpnClient>();


    private readonly string? _customConnectString;

    public DataContext() : base()
    {
        Database.EnsureCreated();
    }

    public DataContext(string connectionString) : base()
    {
        _customConnectString = connectionString;
    }

    public DataContext(DbContextOptions<DataContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!string.IsNullOrEmpty(_customConnectString))
            optionsBuilder.UseNpgsql(_customConnectString);
    }
}
