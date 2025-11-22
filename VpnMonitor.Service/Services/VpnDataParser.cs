using VpnMonitor.Data.Context;

namespace VpnMonitor.Service.Services;

public class VpnClientActualDataResult
{
    public string Client { get; set; }
    public bool IsOnline { get; set; }
    public string LastSend { get; set; }
    public string LasrReceived { get; set; }

    public override string ToString()
    {
        var onlineStr = IsOnline ? "online" : "offline";
        return $"{Client}({onlineStr}) - send: {LastSend} received: {LasrReceived}";
    }
}


public class VpnDataParser
{

    public void View()
    {
        List<VpnClientActualDataResult> actualresult = new();

        var now = DateTime.Now.ToUniversalTime();
        var start = now.AddMinutes(-10);

        using (DataContext db = new())
        {
            var clients = db.VpnClient
                .Where(x => x.Inserted >= start)
                .ToList();

            if (clients.Count != 0)
            {
                var ordered = clients
                    .GroupBy(x => x.CommonName)
                    .Select(x => new
                    {
                        CommonName = x.Key,
                        Data = x.OrderByDescending(x => x.Inserted).ToList(),
                    });

                foreach (var item in ordered)
                {
                    if (item.Data.Count > 1)
                    {
                        var bytesReceivedD = new List<(double, long)>();
                        var bytesSentD = new List<(double, long)>();

                        for (var i = 0; i < item.Data.Count; i++)
                        {
                            if (i + 1 < item.Data.Count)
                            {
                                var cur = item.Data[i];
                                var prev = item.Data[i + 1];
                                var a = Convert.ToInt64(cur.BytesReceived);
                                var b = Convert.ToInt64(prev.BytesReceived);
                                bytesReceivedD.Add(new(
                                    (cur.Inserted - prev.Inserted).TotalSeconds,
                                    a - b));
                            }
                        }

                        for (var i = 0; i < item.Data.Count; i++)
                        {
                            if (i + 1 < item.Data.Count)
                            {
                                var cur = item.Data[i];
                                var prev = item.Data[i + 1];
                                var a = Convert.ToInt64(cur.BytesSent);
                                var b = Convert.ToInt64(prev.BytesSent);
                                bytesSentD.Add(new(
                                    (cur.Inserted - prev.Inserted).TotalSeconds,
                                    a - b));
                            }
                        }

                        var lastReceived = bytesReceivedD.First().Item2 * 8 / bytesReceivedD.First().Item1;
                        var lastReceivedMbit = Math.Round(lastReceived / 1048576, 2);

                        var lastSent = bytesSentD.First().Item2 * 8 / bytesSentD.First().Item1;
                        var lastSentMbit = Math.Round(lastSent / 1048576, 2);

                        actualresult.Add(new()
                        {
                            Client = item.CommonName,
                            IsOnline = true,
                            LastSend = $"{lastSentMbit} Mb/s",
                            LasrReceived = $"{lastReceivedMbit} Mb/s",
                        });
                    }
                    else
                    {
                        actualresult.Add(new()
                        {
                            Client = item.CommonName,
                            IsOnline = true,
                            LastSend = "NONE",
                            LasrReceived = "NONE"
                        });
                    }
                }
            }

            var online = actualresult.Select(x => x.Client).ToList();
            var allClients = db.VpnClient.Select(x => x.CommonName).Distinct().ToList();
            foreach (var item in allClients)
            {
                if (!actualresult.Any(x => x.Client.Equals(item)))
                {
                    actualresult.Add(new VpnClientActualDataResult()
                    {
                        Client = item,
                        IsOnline = false,
                        LastSend = "NONE",
                        LasrReceived = "NONE"
                    });
                }
            }
        }

        foreach (var item in actualresult)
        {
            Console.WriteLine(item.ToString());
        }
    }
}
