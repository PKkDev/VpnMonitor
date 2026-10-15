using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace VpnMonitor.Data.Entities;

[Table("vpn-client", Schema = "public")]
[Index(nameof(CommonName), nameof(Inserted))]
[Index(nameof(Inserted))]
public class VpnClient
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    [Column("common_name")]
    public string CommonName { get; set; }

    [Column("real_address")]
    public string RealAddress { get; set; }

    [Column("virtual_address")]
    public string VirtualAddress { get; set; }

    [Column("virtual_ip_v6_address")]
    public string VirtualIPv6Address { get; set; }

    [Column("bytes_received")]
    public long BytesReceived { get; set; }

    [Column("bytes_sent")]
    public long BytesSent { get; set; }

    [Column("connected_since")]
    public DateTime ConnectedSince { get; set; }

    [Column("connected_since_time_t")]
    public string ConnectedSinceTimeT { get; set; }

    [Column("user_name")]
    public string UserName { get; set; }

    [Column("client_id")]
    public string ClientId { get; set; }

    [Column("peer_id")]
    public string PeerId { get; set; }

    [Column("data_channel_cipher")]
    public string DataChannelCipher { get; set; }

    [Column("inserted")]
    public DateTime Inserted { get; set; }
}
