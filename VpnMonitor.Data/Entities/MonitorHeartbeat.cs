using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VpnMonitor.Data.Entities;

/// <summary>
/// Single row updated by the service after every successful poll of OpenVPN, even when no clients are connected.
/// </summary>
[Table("monitor-heartbeat", Schema = "public")]
public class MonitorHeartbeat
{
    public const int SingleId = 1;

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Column("id")]
    public int Id { get; set; }

    [Column("last_poll")]
    public DateTime LastPoll { get; set; }

    [Column("active_clients")]
    public int ActiveClients { get; set; }

    /// <summary>TITLE line of the OpenVPN status, contains the server version.</summary>
    [Column("server_title")]
    public string? ServerTitle { get; set; }

    /// <summary>Last failure of a poll or of the cleanup, kept after recovery: compare with <see cref="LastPoll"/>.</summary>
    [Column("last_error")]
    public string? LastError { get; set; }

    [Column("last_error_at")]
    public DateTime? LastErrorAt { get; set; }
}
