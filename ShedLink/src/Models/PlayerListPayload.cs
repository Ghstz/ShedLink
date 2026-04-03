using System.Collections.Generic;

namespace ShedLink.Models;

/// <summary>
/// The complete roster of everyone currently online, pushed to dashboards
/// on every telemetry tick. Each entry includes position, health, and ping
/// so the radar and player list views stay in sync without extra requests.
/// </summary>
public class PlayerListPayload
{
    public List<PlayerInfoPayload> Players { get; set; } = new();
}
