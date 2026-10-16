namespace VpnMonitor.Service.Options;

public class MonitorOption
{
    /// <summary>Address of the OpenVPN management interface.</summary>
    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 7505;

    /// <summary>Pause between polls.</summary>
    public int IntervalSeconds { get; set; } = 120;

    /// <summary>Pause before the next attempt after a failed poll.</summary>
    public int RetryDelaySeconds { get; set; } = 10;

    /// <summary>How long records are kept. 0 disables the cleanup.</summary>
    public int RetentionDays { get; set; } = 60;
}
