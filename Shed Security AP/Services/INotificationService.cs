namespace Shed_Security_AP.Services;

/// <summary>
/// Abstraction for desktop notifications. The real implementation uses
/// Windows system tray balloons, but this interface keeps the ViewModels
/// testable without popping up actual toast notifications.
/// </summary>
public interface INotificationService : IDisposable
{
    void ShowAlert(string title, string message);
}
