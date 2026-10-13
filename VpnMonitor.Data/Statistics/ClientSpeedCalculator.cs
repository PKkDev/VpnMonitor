using VpnMonitor.Data.Entities;

namespace VpnMonitor.Data.Statistics;

public record ClientSpeed(double SentMbit, double ReceivedMbit, DateTime ConnectedSince);

public static class ClientSpeedCalculator
{
    private const double BitsInMbit = 1048576;

    /// <summary>
    /// Speed of one client over its two latest samples.
    /// Only the latest session (same ConnectedSince) is used, because byte counters start from zero after a reconnect.
    /// Returns null when the session has fewer than two samples.
    /// </summary>
    public static ClientSpeed? Calculate(IEnumerable<VpnClient> samples)
    {
        var session = samples
            .GroupBy(x => x.ConnectedSince)
            .OrderByDescending(x => x.Key)
            .FirstOrDefault();

        if (session == null)
            return null;

        var ordered = session.OrderByDescending(x => x.Inserted).ToList();
        if (ordered.Count < 2)
            return null;

        var cur = ordered[0];
        var prev = ordered[1];

        var seconds = (cur.Inserted - prev.Inserted).TotalSeconds;
        if (seconds <= 0)
            return null;

        var sent = Convert.ToInt64(cur.BytesSent) - Convert.ToInt64(prev.BytesSent);
        var received = Convert.ToInt64(cur.BytesReceived) - Convert.ToInt64(prev.BytesReceived);

        return new ClientSpeed(ToMbit(sent, seconds), ToMbit(received, seconds), session.Key);
    }

    private static double ToMbit(long bytes, double seconds)
    {
        return Math.Round(bytes * 8 / seconds / BitsInMbit, 2);
    }
}
