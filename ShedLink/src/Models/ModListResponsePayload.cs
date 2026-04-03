using System.Collections.Generic;

namespace ShedLink.Models;

/// <summary>
/// What we send back after scanning the server's Mods directory.
/// Contains every .zip, .cs, and .disabled file we find, along with size and enabled state.
/// </summary>
public class ModListResponsePayload
{
    public List<ModInfo> Mods { get; set; } = [];
}
