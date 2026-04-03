namespace ShedLink.Models;

/// <summary>
/// The raw JSON content of a config file, shipped to the dashboard for editing.
/// </summary>
public class ConfigResponsePayload
{
    public string JsonContent { get; set; } = string.Empty;
}
