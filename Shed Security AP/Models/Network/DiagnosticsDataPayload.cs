using System.Collections.Generic;

namespace Shed_Security_AP.Models.Network;

public class DiagnosticsDataPayload
{
    public int LoadedChunks { get; set; }
    public int DroppedItems { get; set; }
    public int HostileMobs { get; set; }
    public Dictionary<string, double> TickProfile { get; set; } = new();
}
