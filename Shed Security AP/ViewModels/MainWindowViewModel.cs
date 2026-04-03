using System.Windows;
using System.Windows.Input;
using Shed_Security_AP.Core;
using Shed_Security_AP.Services;

namespace Shed_Security_AP.ViewModels;

/// <summary>
/// The root ViewModel that owns every other ViewModel and the WebSocket service.
/// Handles view navigation, wires up connect/disconnect lifecycle, and coordinates
/// the subscribe/reset dance when the connection state changes.
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    private string _title = "Shed Link Dashboard";
    private ViewModelBase _currentView = null!;
    private string _statusText = "Status: Disconnected";

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public ViewModelBase CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public ICommand MinimizeCommand { get; }
    public ICommand MaximizeRestoreCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand NavigateCommand { get; }

    public IWebSocketService WebSocketService { get; }
    public INotificationService NotificationService { get; }
    public ConnectionViewModel ConnectionVm { get; }
    public DashboardViewModel DashboardVm { get; }
    public AntiCheatViewModel AntiCheatVm { get; }
    public ConfigEditorViewModel ConfigEditorVm { get; }
    public ModManagerViewModel ModManagerVm { get; }
    public BackupManagerViewModel BackupManagerVm { get; }
    public DiagnosticsViewModel DiagnosticsVm { get; }
    public SpawnerViewModel SpawnerVm { get; }
    public AccessControlViewModel AccessControlVm { get; }
    public RadarViewModel RadarVm { get; }
    public WorldControlViewModel WorldControlVm { get; }
    public NavigationViewModel NavigationVm { get; }
    public HistoryViewModel HistoryVm { get; }
    public AppSettingsViewModel AppSettingsVm { get; }

    public MainWindowViewModel()
    {
        WebSocketService = new ShedWebSocketService();
        NotificationService = new WindowsNotificationService();
        IAuditService auditService = new LocalAuditService();
        ConnectionVm = new ConnectionViewModel(WebSocketService);
        DashboardVm = new DashboardViewModel(WebSocketService, auditService);
        AntiCheatVm = new AntiCheatViewModel(WebSocketService, NotificationService, () =>
        {
            if (Application.Current?.MainWindow is MainWindow mw)
                return mw.IsAppFocused;
            return true;
        });
        ConfigEditorVm = new ConfigEditorViewModel(WebSocketService, auditService);
        ModManagerVm = new ModManagerViewModel(WebSocketService, auditService);
        BackupManagerVm = new BackupManagerViewModel(WebSocketService, auditService);
        DiagnosticsVm = new DiagnosticsViewModel(WebSocketService, auditService);
        SpawnerVm = new SpawnerViewModel(WebSocketService, auditService);
        AccessControlVm = new AccessControlViewModel(WebSocketService, auditService);
        RadarVm = new RadarViewModel(WebSocketService);
        WorldControlVm = new WorldControlViewModel(WebSocketService, auditService);
        NavigationVm = new NavigationViewModel(WebSocketService, auditService);
        HistoryVm = new HistoryViewModel(auditService);
        AppSettingsVm = new AppSettingsViewModel();

        // Start on the connection screen — nothing works until they auth
        _currentView = ConnectionVm;

        // Once auth succeeds, wire everyone up and drop the user into the dashboard
        ConnectionVm.Connected += (_, _) =>
        {
            DashboardVm.Resubscribe();
            AntiCheatVm.Resubscribe();
            ConfigEditorVm.Resubscribe();
            ModManagerVm.Resubscribe();
            BackupManagerVm.Resubscribe();
            DiagnosticsVm.Resubscribe();
            SpawnerVm.Resubscribe();
            AccessControlVm.Resubscribe();
            RadarVm.Resubscribe();
            WorldControlVm.Resubscribe();
            NavigationVm.Resubscribe();
            StatusText = "Status: Connected";
            CurrentView = DashboardVm;
        };

        // If we lose the server, wipe all state and kick back to the login screen
        WebSocketService.Disconnected += (_, _) =>
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                DashboardVm.ResetState();
                AntiCheatVm.ResetState();
                ConfigEditorVm.ResetState();
                ModManagerVm.ResetState();
                BackupManagerVm.ResetState();
                DiagnosticsVm.ResetState();
                SpawnerVm.ResetState();
                AccessControlVm.ResetState();
                RadarVm.ResetState();
                WorldControlVm.ResetState();
                NavigationVm.ResetState();
                ConnectionVm.ErrorMessage = "Connection lost to server.";
                StatusText = "Status: Disconnected";
                CurrentView = ConnectionVm;
            });
        };

        MinimizeCommand = new RelayCommand(_ =>
        {
            if (Application.Current?.MainWindow is Window w)
                w.WindowState = WindowState.Minimized;
        });

        MaximizeRestoreCommand = new RelayCommand(_ =>
        {
            if (Application.Current?.MainWindow is Window w)
                w.WindowState = w.WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
        });

        CloseCommand = new RelayCommand(_ =>
        {
            if (Application.Current?.MainWindow is Window w)
                w.Close();
        });

        NavigateCommand = new RelayCommand(param =>
        {
            if (param is "History")
                HistoryVm.Refresh();

            CurrentView = param switch
            {
                "Connection" => ConnectionVm,
                "Dashboard" => DashboardVm,
                "AntiCheat" => AntiCheatVm,
                "ConfigEditor" => ConfigEditorVm,
                "ModManager" => ModManagerVm,
                "BackupManager" => BackupManagerVm,
                "Diagnostics" => DiagnosticsVm,
                "Spawner" => SpawnerVm,
                "AccessControl" => AccessControlVm,
                "Radar" => RadarVm,
                "WorldControl" => WorldControlVm,
                "Navigation" => NavigationVm,
                "History" => HistoryVm,
                "AppSettings" => AppSettingsVm,
                _ => CurrentView
            };
        });
    }
}
