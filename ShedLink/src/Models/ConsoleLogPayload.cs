namespace ShedLink.Models;

/// <summary>
/// A single log line forwarded to connected dashboards in real time.
/// </summary>
public class ConsoleLogPayload
{
    public string Message { get; set; } = string.Empty;
}
