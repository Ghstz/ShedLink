using System.IO;
using System.Text.Json;
using Shed_Security_AP.Models.Local;

namespace Shed_Security_AP.Services;

/// <summary>
/// Writes admin actions (kicks, bans, config edits, etc.) to daily JSON log files
/// under <c>%AppData%/ShedLink/Logs/</c>. This gives server owners a local paper trail
/// of everything they did through the dashboard, even if the server is gone.
/// </summary>
public class LocalAuditService : IAuditService
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ShedLink", "Logs");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string GetTodayPath()
        => Path.Combine(LogDir, $"audit-{DateTime.Now:yyyy-MM-dd}.json");

    public void LogAction(string action, string target, string details)
    {
        var entries = LoadEntriesFromFile(GetTodayPath());
        entries.Add(new AuditEntry
        {
            Timestamp = DateTime.Now,
            Action = action,
            Target = target,
            Details = details
        });
        SaveEntries(entries, GetTodayPath());
    }

    public List<AuditEntry> LoadEntries()
    {
        if (!Directory.Exists(LogDir))
            return [];

        var allEntries = new List<AuditEntry>();

        foreach (var file in Directory.GetFiles(LogDir, "audit-*.json"))
        {
            allEntries.AddRange(LoadEntriesFromFile(file));
        }

        allEntries.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
        return allEntries;
    }

    public void ClearAll()
    {
        if (!Directory.Exists(LogDir))
            return;

        foreach (var file in Directory.GetFiles(LogDir, "audit-*.json"))
        {
            try { File.Delete(file); } catch { }
        }
    }

    private static List<AuditEntry> LoadEntriesFromFile(string path)
    {
        if (!File.Exists(path))
            return [];

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<AuditEntry>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static void SaveEntries(List<AuditEntry> entries, string path)
    {
        Directory.CreateDirectory(LogDir);
        File.WriteAllText(path, JsonSerializer.Serialize(entries, JsonOptions));
    }
}
