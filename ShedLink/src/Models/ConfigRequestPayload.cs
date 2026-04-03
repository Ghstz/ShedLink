namespace ShedLink.Models;

/// <summary>
/// Dashboard wants to read a specific config file by name.
/// </summary>
public class ConfigRequestPayload
{
    public string FileName { get; set; } = string.Empty;
}
