namespace ShedLink.Models;

/// <summary>
/// One row in the backup list — name, human-readable size, and when it was created.
/// </summary>
public class BackupInfo
{
    public string Name { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
}
