using System.Collections.Generic;

namespace ShedLink.Models;

/// <summary>
/// List of .json config file names found in the server's ModConfig folder.
/// </summary>
public class ConfigListResponsePayload
{
    public List<string> Files { get; set; } = [];
}
