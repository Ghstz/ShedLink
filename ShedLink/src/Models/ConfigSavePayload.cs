namespace ShedLink.Models;

/// <summary>
/// Dashboard is saving an edited config file back to disk.
/// After writing, we poke Shed Security to hot-reload it.
/// </summary>
public class ConfigSavePayload
{
    public string FileName { get; set; } = string.Empty;
    public string JsonContent { get; set; } = string.Empty;
}
