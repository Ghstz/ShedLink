namespace ShedLink.Models;

/// <summary>
/// Dashboard wants to download a specific backup file.
/// </summary>
public class FileDownloadRequestPayload
{
    public string FileName { get; set; } = string.Empty;
}
