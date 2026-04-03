using Shed_Security_AP.Models.Local;

namespace Shed_Security_AP.Services;

/// <summary>
/// Contract for logging admin actions locally. Lets us swap implementations
/// (e.g., file-based vs. database) without touching any ViewModel code.
/// </summary>
public interface IAuditService
{
    void LogAction(string action, string target, string details);
    List<AuditEntry> LoadEntries();
    void ClearAll();
}
