using System.Windows;
using WinForms = System.Windows.Forms;

namespace Shed_Security_AP.Services;

/// <summary>
/// Shows Windows system tray balloon notifications for security alerts.
/// Only fires when the app isn't focused — so you'll see a toast pop up
/// if a player triggers anti-cheat while you're tabbed out.
/// </summary>
public class WindowsNotificationService : INotificationService
{
    private readonly WinForms.NotifyIcon _notifyIcon;
    private bool _disposed;

    public WindowsNotificationService()
    {
        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Shield,
            Text = "Shed Link Dashboard",
            Visible = true
        };
    }

    public void ShowAlert(string title, string message)
    {
        if (_disposed) return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            _notifyIcon.ShowBalloonTip(
                3000,
                title,
                message,
                WinForms.ToolTipIcon.Warning);
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
