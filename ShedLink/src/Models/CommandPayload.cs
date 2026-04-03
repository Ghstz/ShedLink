namespace ShedLink.Models;

/// <summary>
/// Raw console command from the dashboard. Gets injected straight into the server’s command processor.
/// </summary>
public class CommandPayload
{
    public string Command { get; set; } = string.Empty;
}
