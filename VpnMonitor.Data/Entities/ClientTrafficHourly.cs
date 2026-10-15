using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace VpnMonitor.Data.Entities;

/// <summary>
/// Traffic of a client per UTC hour. Not removed by the retention cleanup, so long-term statistics survive.
/// Hourly UTC buckets let the UI group by days in any whole-hour time zone.
/// </summary>
[Table("client-traffic-hourly", Schema = "public")]
[PrimaryKey(nameof(CommonName), nameof(Hour))]
public class ClientTrafficHourly
{
    [Column("common_name")]
    public string CommonName { get; set; } = string.Empty;

    /// <summary>Start of the hour, UTC.</summary>
    [Column("hour")]
    public DateTime Hour { get; set; }

    /// <summary>Bytes received by the server, i.e. uploaded by the client.</summary>
    [Column("bytes_received")]
    public long BytesReceived { get; set; }

    /// <summary>Bytes sent by the server, i.e. downloaded by the client.</summary>
    [Column("bytes_sent")]
    public long BytesSent { get; set; }
}
