namespace ShedLink.Models;

/// <summary>
/// The heartbeat stats we push to every connected dashboard on each telemetry tick.
/// TPS, RAM, and uptime are pre-formatted as strings so the UI can display them as-is.
/// </summary>
public class MetricsPayload
{
    public string Tps { get; set; } = string.Empty;

    public string RamUsage { get; set; } = string.Empty;

    public string Uptime { get; set; } = string.Empty;
}
