using System.Collections.Generic;

namespace ShedLink.Models;

/// <summary>
/// The full list of backup .zip files the server found in its Backups directory.
/// </summary>
public class BackupListResponsePayload
{
    public List<BackupInfo> Backups { get; set; } = [];
}
