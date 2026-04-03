namespace ShedLink.Models;

/// <summary>
/// One row in the mod manager list — the file name on disk, its human-readable size,
/// and whether it's currently active. Enabled/disabled is determined by the file extension
/// (.cs/.zip = enabled, .disabled = disabled).
/// </summary>
public class ModInfo
{
    public string Name { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}
