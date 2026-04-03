using System.Collections.Generic;

namespace ShedLink.Models;

/// <summary>
/// Snapshot of entity counts and distribution for the diagnostics panel.
/// TickProfile feeds the pie chart on the dashboard.
/// </summary>
public class DiagnosticsDataPayload
{
    public int LoadedChunks { get; set; }
    public int DroppedItems { get; set; }
    public int HostileMobs { get; set; }
    public Dictionary<string, double> TickProfile { get; set; } = new();
}
