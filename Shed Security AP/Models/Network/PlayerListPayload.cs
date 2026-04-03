using System.Collections.Generic;

namespace Shed_Security_AP.Models.Network;

public class PlayerListPayload
{
    public List<PlayerInfoPayload> Players { get; set; } = new();
}
