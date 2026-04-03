using System.Collections.Generic;

namespace Shed_Security_AP.Models.Network;

public class BackupListResponsePayload
{
    public List<BackupInfo> Backups { get; set; } = [];
}
