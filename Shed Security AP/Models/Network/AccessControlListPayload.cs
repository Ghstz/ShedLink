using System.Collections.Generic;

namespace Shed_Security_AP.Models.Network;

public class AccessControlListPayload
{
    public List<string> Whitelist { get; set; } = [];
    public List<string> Banlist { get; set; } = [];
}
