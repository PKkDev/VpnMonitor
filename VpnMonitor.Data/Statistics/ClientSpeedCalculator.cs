using VpnMonitor.Data.Entities;

namespace VpnMonitor.Data.Statistics;

/// <param name="SentBps">Server to client, i.e. download of the client, bit/s.</param>
/// <param name="ReceivedBps">Client to server, i.e. upload of the client, bit/s.</param>
public record ClientSpeed(double SentBps, double ReceivedBps, DateTime ConnectedSince);

/// <param name="StartsSegment">The point is not connected to the previous one: a new session or a gap in the data.</param>
public record SpeedPoint(DateTime Time, double SentBps, double ReceivedBps, bool StartsSegment);

public static class ClientSpeedCalculator
{
    /// <summary>Snapshots further apart than this are not joined into one line on a chart.</summary>
    public static readonly TimeSpan MaxGap = TimeSpan.FromMinutes(10);

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

        var speed = Between(ordered[1], ordered[0]);
        if (speed == null)
            return null;

        return new ClientSpeed(speed.Value.Sent, speed.Value.Received, session.Key);
    }

    /// <summary>
    /// Speed of one client between every two consecutive samples of the same session, for a chart.
    /// </summary>
    public static List<SpeedPoint> Series(IEnumerable<VpnClient> samples)
    {
        var result = new List<SpeedPoint>();
        VpnClient? prev = null;
        var connected = false;

        foreach (var cur in samples.OrderBy(x => x.Inserted))
        {
            var speed = prev != null && prev.ConnectedSince == cur.ConnectedSince && cur.Inserted - prev.Inserted <= MaxGap
                ? Between(prev, cur)
                : null;

            if (speed != null)
            {
                result.Add(new SpeedPoint(cur.Inserted, speed.Value.Sent, speed.Value.Received, !connected));
                connected = true;
            }
            else
            {
                connected = false;
            }

            prev = cur;
        }

        return result;
    }

    /// <summary>
    /// Summed speed of all clients at every poll, for a chart of the whole server.
    /// </summary>
    public static List<SpeedPoint> Total(IEnumerable<VpnClient> samples)
    {
        var sums = samples
            .GroupBy(x => x.CommonName)
            .SelectMany(x => Series(x))
            .GroupBy(x => x.Time)
            .Select(x => new { Time = x.Key, Sent = x.Sum(y => y.SentBps), Received = x.Sum(y => y.ReceivedBps) })
            .OrderBy(x => x.Time);

        var result = new List<SpeedPoint>();
        DateTime? prevTime = null;

        foreach (var sum in sums)
        {
            var startsSegment = prevTime == null || sum.Time - prevTime > MaxGap;
            result.Add(new SpeedPoint(sum.Time, sum.Sent, sum.Received, startsSegment));
            prevTime = sum.Time;
        }

        return result;
    }

    private static (double Sent, double Received)? Between(VpnClient prev, VpnClient cur)
    {
        var seconds = (cur.Inserted - prev.Inserted).TotalSeconds;
        if (seconds <= 0)
            return null;

        var sent = TrafficCalculator.Delta(cur.BytesSent, prev.BytesSent);
        var received = TrafficCalculator.Delta(cur.BytesReceived, prev.BytesReceived);

        return (sent * 8 / seconds, received * 8 / seconds);
    }
}
