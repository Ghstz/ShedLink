using System.Collections.Generic;

namespace ShedLink.Models;

/// <summary>
/// The current shadowban roster, pushed to dashboards so the access control view
/// stays up to date. Shadowbanned players can still "play" but their actions are
/// silently neutered — this list lets admins see who's in that state.
/// </summary>
public class ShadowbanListPayload
{
    public List<string> Players { get; set; } = [];
}
