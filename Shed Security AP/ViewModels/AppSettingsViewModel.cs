using Shed_Security_AP.Core;

namespace Shed_Security_AP.ViewModels;

/// <summary>
/// Dashboard-level preferences. Right now it's just the desktop notification toggle,
/// but this is where any future client-side settings would live.
/// </summary>
public class AppSettingsViewModel : ViewModelBase
{
    public bool EnableDesktopNotifications
    {
        get => LocalPreferences.EnableDesktopNotifications;
        set
        {
            if (LocalPreferences.EnableDesktopNotifications != value)
            {
                LocalPreferences.EnableDesktopNotifications = value;
                OnPropertyChanged();
            }
        }
    }
}
