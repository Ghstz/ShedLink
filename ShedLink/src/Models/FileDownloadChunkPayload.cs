namespace ShedLink.Models;

/// <summary>
/// One chunk of a backup file being streamed to the dashboard.
/// We split large files into 1 MB pieces so we don’t blow up the WebSocket frame.
/// </summary>
public class FileDownloadChunkPayload
{
    public string FileName { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public int TotalChunks { get; set; }
    public string Base64Data { get; set; } = string.Empty;
}
