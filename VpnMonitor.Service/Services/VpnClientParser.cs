using System.Text;
using VpnMonitor.Data.Entities;


/*
 
HEADER, CLIENT_LIST, Common Name, Real Address, Virtual Address, Virtual IPv6 Address, Bytes Received, Bytes Sent, Connected Since, Connected Since (time_t), Username, Client ID, Peer ID, Data Channel Cipher
CLIENT_LIST, kirill-all-v3,81.23.184.151:4169,10.8.0.3,fddd: 1194:1194:1194::1001,8682159,384698309,2025 - 07 - 16 19:29:54,1752694194,UNDEF,0,0, AES-256-GCM

HEADER, ROUTING_TABLE, Virtual Address, Common Name, Real Address, Last Ref, Last Ref (time_t)
ROUTING_TABLE, fddd:1194:1194:1194::1001,kirill - all - v3,81.23.184.151:4169,2025 - 07 - 16 19:42:01,1752694921
ROUTING_TABLE,10.8.0.3, kirill-all-v3,81.23.184.151:4169,2025 - 07 - 16 19:41:57,1752694917
 
 */

namespace VpnMonitor.Service.Services;

public class VpnClientParser
{
    public VpnClientParser() { }

    public List<VpnClient> ParseClients(StringBuilder sb, DateTime now)
    {
        List<VpnClient> result = new();

        var listTxt = sb.ToString().Split(Environment.NewLine);
        var clientsTxt = listTxt.Where(x => x.StartsWith("CLIENT_LIST,"));
        foreach (var clientTxt in clientsTxt)
        {
            var props = clientTxt.Replace("CLIENT_LIST,", "").Split(',');

            var connectedSinceUnc = DateTime.Parse(props[6]);
            //var connectedSince = DateTime.SpecifyKind(connectedSinceUnc, DateTimeKind.Utc);
            var connectedSince = connectedSinceUnc.ToUniversalTime();

            result.Add(new VpnClient()
            {
                CommonName = props[0],
                RealAddress = props[1],
                VirtualAddress = props[2],
                VirtualIPv6Address = props[3],
                BytesReceived = props[4],
                BytesSent = props[5],
                ConnectedSince = connectedSince,
                ConnectedSinceTimeT = props[7],
                UserName = props[8],
                ClientId = props[9],
                PeerId = props[10],
                DataChannelCipher = props[11],

                Inserted = now
            });

        }

        if (result.Count > 0)
        {
            var distinct = result
                .GroupBy(x => x.CommonName)
                .Select(x => x.OrderByDescending(x => x.BytesSent).First());
            return distinct.ToList();
        }

        return result;
    }

    public List<VpnClient> ParseClientsTest()
    {
        StringBuilder sb = new StringBuilder()
           .AppendLine("TIME,2025 - 02 - 09 17:00:02,1739116802")
           .AppendLine("HEADER, CLIENT_LIST, Common Name, Real Address, Virtual Address, Virtual IPv6 Address, Bytes Received, Bytes Sent, Connected Since, Connected Since (time_t), Username, Client ID, Peer ID, Data Channel Cipher")
           .AppendLine("CLIENT_LIST,bav-all-client,109.229.110.141:58267,10.8.0.6,,3253025,62832311,2025-02-09 17:13:11,1739117591,UNDEF,45,1,AES-256-GCM")
           .AppendLine("CLIENT_LIST, kirill-all-client,94.231.132.193:54305,10.8.0.5,,100,100,2025 - 02 - 09 16:58:04,1739116684,UNDEF,40,0, AES-256-GCM")
           .AppendLine("CLIENT_LIST, kirill-all-client,94.231.132.193:54305,10.8.0.5,,200,200,2025 - 02 - 09 16:58:04,1739116684,UNDEF,40,0, AES-256-GCM")
           .AppendLine("HEADER, ROUTING_TABLE, Virtual Address, Common Name, Real Address, Last Ref, Last Ref (time_t)")
           .AppendLine("ROUTING_TABLE,10.8.0.5, kirill-all-client,94.231.132.193:54305,2025 - 02 - 09 17:00:01,1739116801")
           .AppendLine("GLOBAL_STATS, Max bcast/mcast queue length,2")
           .AppendLine("GLOBAL_STATS, dco_enabled,0")
           .AppendLine("END");

        return ParseClients(sb, DateTime.Now.ToUniversalTime());
    }
}
