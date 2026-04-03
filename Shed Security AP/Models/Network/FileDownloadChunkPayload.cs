namespace Shed_Security_AP.Models.Network;

public class FileDownloadChunkPayload
{
    public string FileName { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public int TotalChunks { get; set; }
    public string Base64Data { get; set; } = string.Empty;
}
