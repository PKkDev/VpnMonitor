using System.Text;
using VpnMonitor.Service.Services;

namespace VpnMonitor.Tests;

public class VpnClientParserTests
{
    private static readonly DateTime Now = new(2025, 2, 9, 17, 0, 2, DateTimeKind.Utc);

    private static StringBuilder Status(params string[] clientLines)
    {
        var sb = new StringBuilder()
            .AppendLine("TITLE,OpenVPN 2.6.12 x86_64-pc-linux-gnu")
            .AppendLine("TIME,2025-02-09 17:00:02,1739116802")
            .AppendLine("HEADER,CLIENT_LIST,Common Name,Real Address,Virtual Address,Virtual IPv6 Address,Bytes Received,Bytes Sent,Connected Since,Connected Since (time_t),Username,Client ID,Peer ID,Data Channel Cipher");

        foreach (var line in clientLines)
            sb.AppendLine(line);

        return sb
            .AppendLine("HEADER,ROUTING_TABLE,Virtual Address,Common Name,Real Address,Last Ref,Last Ref (time_t)")
            .AppendLine("ROUTING_TABLE,10.8.0.5,kirill-all-client,192.0.2.10:54305,2025-02-09 17:00:01,1739116801")
            .AppendLine("GLOBAL_STATS,Max bcast/mcast queue length,2")
            .AppendLine("END");
    }

    [Fact]
    public void ParseClients_MapsAllFields()
    {
        var sb = Status("CLIENT_LIST,bav-all-client,192.0.2.20:58267,10.8.0.6,fddd:1194:1194:1194::1001,3253025,62832311,2025-02-09 17:13:11,1739117591,UNDEF,45,1,AES-256-GCM");

        var client = Assert.Single(new VpnClientParser().ParseClients(sb, Now));

        Assert.Equal("bav-all-client", client.CommonName);
        Assert.Equal("192.0.2.20:58267", client.RealAddress);
        Assert.Equal("10.8.0.6", client.VirtualAddress);
        Assert.Equal("fddd:1194:1194:1194::1001", client.VirtualIPv6Address);
        Assert.Equal("3253025", client.BytesReceived);
        Assert.Equal("62832311", client.BytesSent);
        Assert.Equal("1739117591", client.ConnectedSinceTimeT);
        Assert.Equal("UNDEF", client.UserName);
        Assert.Equal("45", client.ClientId);
        Assert.Equal("1", client.PeerId);
        Assert.Equal("AES-256-GCM", client.DataChannelCipher);
        Assert.Equal(Now, client.Inserted);
    }

    [Fact]
    public void ParseClients_TakesConnectedSinceFromTimeT_AsUtc()
    {
        var sb = Status("CLIENT_LIST,bav-all-client,192.0.2.20:58267,10.8.0.6,,1,2,2025-02-09 17:13:11,1739117591,UNDEF,45,1,AES-256-GCM");

        var client = Assert.Single(new VpnClientParser().ParseClients(sb, Now));

        Assert.Equal(new DateTime(2025, 2, 9, 16, 13, 11, DateTimeKind.Utc), client.ConnectedSince);
        Assert.Equal(DateTimeKind.Utc, client.ConnectedSince.Kind);
    }

    [Fact]
    public void ParseClients_IgnoresNonClientLines()
    {
        var clients = new VpnClientParser().ParseClients(Status(), Now);

        Assert.Empty(clients);
    }

    [Fact]
    public void ParseClients_ReturnsEveryDistinctClient()
    {
        var sb = Status(
            "CLIENT_LIST,bav-all-client,192.0.2.20:58267,10.8.0.6,,1,2,2025-02-09 17:13:11,1739117591,UNDEF,45,1,AES-256-GCM",
            "CLIENT_LIST,kirill-all-client,192.0.2.10:54305,10.8.0.5,,100,100,2025-02-09 16:58:04,1739116684,UNDEF,40,0,AES-256-GCM");

        var clients = new VpnClientParser().ParseClients(sb, Now);

        Assert.Equal(new[] { "bav-all-client", "kirill-all-client" }, clients.Select(x => x.CommonName).Order());
    }

    [Fact]
    public void ParseClients_ForDuplicatedCommonName_KeepsRecordWithMostBytesSent()
    {
        // 9000 < 100000 as numbers, but "9000" > "100000" as strings
        var sb = Status(
            "CLIENT_LIST,kirill-all-client,192.0.2.10:54305,10.8.0.5,,100,9000,2025-02-09 16:58:04,1739116684,UNDEF,40,0,AES-256-GCM",
            "CLIENT_LIST,kirill-all-client,192.0.2.11:54306,10.8.0.7,,200,100000,2025-02-09 16:59:04,1739116744,UNDEF,41,1,AES-256-GCM");

        var client = Assert.Single(new VpnClientParser().ParseClients(sb, Now));

        Assert.Equal("100000", client.BytesSent);
        Assert.Equal("10.8.0.7", client.VirtualAddress);
    }
}
