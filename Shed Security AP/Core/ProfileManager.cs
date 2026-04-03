using System.IO;
using System.Text.Json;
using Shed_Security_AP.Models.Local;

namespace Shed_Security_AP.Core;

/// <summary>
/// Handles saving and loading server connection profiles to/from
/// <c>%AppData%/ShedLink/profiles.json</c>. This way users don't have to
/// re-enter IP/port/token every time they open the dashboard.
/// </summary>
public static class ProfileManager
{
    private static readonly string ProfileDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ShedLink");

    private static readonly string ProfilePath = Path.Combine(ProfileDir, "profiles.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static List<ServerProfile> Load()
    {
        if (!File.Exists(ProfilePath))
            return [];

        try
        {
            var json = File.ReadAllText(ProfilePath);
            return JsonSerializer.Deserialize<List<ServerProfile>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void Save(List<ServerProfile> profiles)
    {
        Directory.CreateDirectory(ProfileDir);
        var json = JsonSerializer.Serialize(profiles, JsonOptions);
        File.WriteAllText(ProfilePath, json);
    }
}
