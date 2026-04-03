using System.Collections.Generic;

namespace Shed_Security_AP.Models.Network;

public class ModListResponsePayload
{
    public List<ModInfo> Mods { get; set; } = [];
}
