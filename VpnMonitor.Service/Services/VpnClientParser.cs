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

            // time_t is used instead of the text date: it does not depend on the server time zone or culture
            var connectedSince = DateTimeOffset.FromUnixTimeSeconds(long.Parse(props[7])).UtcDateTime;

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
                .Select(x => x.OrderByDescending(x => long.Parse(x.BytesSent)).First());
            return distinct.ToList();
        }

        return result;
    }
}
