using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Shed_Security_AP.Models.Local;

namespace Shed_Security_AP.Core;

/// <summary>
/// Persists the user's saved teleport waypoints to <c>%AppData%/ShedLink/waypoints.json</c>.
/// Same pattern as <see cref="ProfileManager"/> — simple JSON file, loaded on startup,
/// saved whenever the list changes.
/// </summary>
public static class WaypointManager
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ShedLink");

    private static readonly string FilePath = Path.Combine(Dir, "waypoints.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static List<Waypoint> Load()
    {
        if (!File.Exists(FilePath))
            return [];

        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<Waypoint>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void Save(List<Waypoint> waypoints)
    {
        Directory.CreateDirectory(Dir);
        var json = JsonSerializer.Serialize(waypoints, JsonOptions);
        File.WriteAllText(FilePath, json);
    }
}
