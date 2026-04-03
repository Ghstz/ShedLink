namespace ShedLink.Models;

/// <summary>
/// One chunk of a mod .zip being uploaded from the dashboard.
/// Chunks arrive sequentially and get stitched into a .part file until the last one lands.
/// </summary>
public class FileUploadChunkPayload
{
    public string FileName { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public int TotalChunks { get; set; }
    public string Base64Data { get; set; } = string.Empty;
}
