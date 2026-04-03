using System.Collections.Generic;

namespace ShedLink.Models;

/// <summary>
/// Carries the current whitelist and ban list to the dashboard.
/// </summary>
public class AccessControlListPayload
{
    public List<string> Whitelist { get; set; } = new();
    public List<string> Banlist { get; set; } = new();
}
