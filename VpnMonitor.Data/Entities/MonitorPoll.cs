using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace VpnMonitor.Data.Entities;

/// <summary>
/// One row per successful poll, including polls without clients. Removed by the retention cleanup.
/// </summary>
[Table("monitor-poll", Schema = "public")]
[Index(nameof(PolledAt))]
public class MonitorPoll
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    [Column("polled_at")]
    public DateTime PolledAt { get; set; }

    [Column("active_clients")]
    public int ActiveClients { get; set; }
}
