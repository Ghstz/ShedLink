using System.Collections.ObjectModel;
using System.Windows.Input;
using Shed_Security_AP.Core;
using Shed_Security_AP.Models.Local;
using Shed_Security_AP.Services;

namespace Shed_Security_AP.ViewModels;

/// <summary>
/// The login screen. Lets the user enter IP/port/token, save connection profiles
/// for quick access later, and fires <see cref="Connected"/> on successful auth
/// so the main window knows to switch to the dashboard.
/// </summary>
public class ConnectionViewModel : ViewModelBase
{
    private readonly IWebSocketService _webSocketService;

    private string _serverIp = "127.0.0.1";
    private string _port = "42420";
    private string _securityToken = string.Empty;
    private bool _isConnecting;
    private string? _errorMessage;
    private ServerProfile? _selectedProfile;

    public event EventHandler? Connected;

    public ObservableCollection<ServerProfile> SavedProfiles { get; } = [];

    public ServerProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value) && value is not null)
            {
                ServerIp = value.Ip;
                Port = value.Port;
                SecurityToken = value.Token;
            }
        }
    }

    public string ServerIp
    {
        get => _serverIp;
        set => SetProperty(ref _serverIp, value);
    }

    public string Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    public string SecurityToken
    {
        get => _securityToken;
        set => SetProperty(ref _securityToken, value);
    }

    public bool IsConnecting
    {
        get => _isConnecting;
        set => SetProperty(ref _isConnecting, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public ICommand ConnectCommand { get; }
    public ICommand SaveProfileCommand { get; }

    public ConnectionViewModel(IWebSocketService webSocketService)
    {
        _webSocketService = webSocketService;

        LoadProfiles();

        ConnectCommand = new AsyncRelayCommand(
            async _ =>
            {
                IsConnecting = true;
                ErrorMessage = null;

                var (success, errorMessage) = await _webSocketService.ConnectAsync(ServerIp, Port, SecurityToken);

                IsConnecting = false;

                if (success)
                {
                    Connected?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    ErrorMessage = errorMessage ?? "Failed to connect.";
                }
            },
            _ => !IsConnecting
                 && !string.IsNullOrWhiteSpace(ServerIp)
                 && !string.IsNullOrWhiteSpace(Port)
                 && !string.IsNullOrWhiteSpace(SecurityToken));

        SaveProfileCommand = new RelayCommand(
            _ =>
            {
                var name = $"{ServerIp}:{Port}";
                var existing = SavedProfiles.FirstOrDefault(p => p.Name == name);

                if (existing is not null)
                {
                    existing.Ip = ServerIp;
                    existing.Port = Port;
                    existing.Token = SecurityToken;
                }
                else
                {
                    SavedProfiles.Add(new ServerProfile
                    {
                        Name = name,
                        Ip = ServerIp,
                        Port = Port,
                        Token = SecurityToken
                    });
                }

                ProfileManager.Save([.. SavedProfiles]);
            },
            _ => !string.IsNullOrWhiteSpace(ServerIp)
                 && !string.IsNullOrWhiteSpace(Port)
                 && !string.IsNullOrWhiteSpace(SecurityToken));
    }

    private void LoadProfiles()
    {
        var profiles = ProfileManager.Load();
        foreach (var profile in profiles)
            SavedProfiles.Add(profile);
    }
}
