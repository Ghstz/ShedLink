namespace ShedLink.Models;

/// <summary>
/// Dashboard tells us to flip a mod on or off. We handle this by renaming the file —
/// appending ".disabled" to turn it off, or stripping that suffix to turn it back on.
/// The server needs a restart for changes to actually take effect.
/// </summary>
public class ModToggleRequestPayload
{
    public string FileName { get; set; } = string.Empty;
    public bool Enable { get; set; }
}
