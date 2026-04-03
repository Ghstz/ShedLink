using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Shed_Security_AP.Core;
using Shed_Security_AP.Models.Local;
using Shed_Security_AP.Models.Network;
using Shed_Security_AP.Services;

namespace Shed_Security_AP.ViewModels;

/// <summary>
/// Teleportation hub. Lets admins save named waypoints (persisted to disk),
/// teleport players to waypoints or to other players. Coordinates are relative
/// to world spawn — the server converts to absolute engine coords on its end.
/// </summary>
public class NavigationViewModel : ViewModelBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IWebSocketService _webSocketService;
    private readonly IAuditService _auditService;

    public ObservableCollection<Waypoint> SavedWaypoints { get; } = [];
    public ObservableCollection<string> OnlinePlayers { get; } = [];

    // ── Fields for the "Add Waypoint" form ──
    private string _newWaypointName = string.Empty;
    public string NewWaypointName
    {
        get => _newWaypointName;
        set => SetProperty(ref _newWaypointName, value);
    }

    private string _newWaypointX = "0";
    public string NewWaypointX
    {
        get => _newWaypointX;
        set => SetProperty(ref _newWaypointX, value);
    }

    private string _newWaypointY = "100";
    public string NewWaypointY
    {
        get => _newWaypointY;
        set => SetProperty(ref _newWaypointY, value);
    }

    private string _newWaypointZ = "0";
    public string NewWaypointZ
    {
        get => _newWaypointZ;
        set => SetProperty(ref _newWaypointZ, value);
    }

    // ── Teleport panel — who goes where ──
    private string? _selectedPlayer;
    public string? SelectedPlayer
    {
        get => _selectedPlayer;
        set => SetProperty(ref _selectedPlayer, value);
    }

    private string? _destinationPlayer;
    public string? DestinationPlayer
    {
        get => _destinationPlayer;
        set => SetProperty(ref _destinationPlayer, value);
    }

    private Waypoint? _selectedWaypoint;
    public Waypoint? SelectedWaypoint
    {
        get => _selectedWaypoint;
        set => SetProperty(ref _selectedWaypoint, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand AddWaypointCommand { get; }
    public ICommand RemoveWaypointCommand { get; }
    public ICommand TeleportToWaypointCommand { get; }
    public ICommand TeleportToPlayerCommand { get; }

    public NavigationViewModel(IWebSocketService webSocketService, IAuditService auditService)
    {
        _webSocketService = webSocketService;
        _auditService = auditService;
        _webSocketService.MessageReceived += OnMessageReceived;

        // Pull saved waypoints from disk so they survive app restarts
        foreach (var wp in WaypointManager.Load())
            SavedWaypoints.Add(wp);

        AddWaypointCommand = new RelayCommand(_ =>
        {
            if (string.IsNullOrWhiteSpace(NewWaypointName))
                return;

            if (!double.TryParse(NewWaypointX, out var x) ||
                !double.TryParse(NewWaypointY, out var y) ||
                !double.TryParse(NewWaypointZ, out var z))
            {
                StatusMessage = "Invalid coordinates.";
                return;
            }

            var wp = new Waypoint { Name = NewWaypointName.Trim(), X = x, Y = y, Z = z };
            SavedWaypoints.Add(wp);
            PersistWaypoints();

            NewWaypointName = string.Empty;
            NewWaypointX = "0";
            NewWaypointY = "100";
            NewWaypointZ = "0";
            StatusMessage = $"Waypoint \"{wp.Name}\" saved.";
        });

        RemoveWaypointCommand = new RelayCommand(param =>
        {
            if (param is Waypoint wp)
            {
                SavedWaypoints.Remove(wp);
                PersistWaypoints();
                StatusMessage = $"Waypoint \"{wp.Name}\" removed.";
            }
        });

        TeleportToWaypointCommand = new AsyncRelayCommand(
            async _ =>
            {
                await SendTeleportAsync(SelectedPlayer!, coords: SelectedWaypoint!);
                _auditService.LogAction("Teleport", SelectedPlayer!, $"→ {SelectedWaypoint!.Name}");
                StatusMessage = $"Teleported {SelectedPlayer} → {SelectedWaypoint!.Name}";
            },
            _ => !string.IsNullOrEmpty(SelectedPlayer) && SelectedWaypoint is not null);

        TeleportToPlayerCommand = new AsyncRelayCommand(
            async _ =>
            {
                await SendTeleportAsync(SelectedPlayer!, destinationPlayer: DestinationPlayer!);
                _auditService.LogAction("Teleport", SelectedPlayer!, $"→ {DestinationPlayer}");
                StatusMessage = $"Teleported {SelectedPlayer} → {DestinationPlayer}";
            },
            _ => !string.IsNullOrEmpty(SelectedPlayer) && !string.IsNullOrEmpty(DestinationPlayer)
                 && SelectedPlayer != DestinationPlayer);
    }

    private async Task SendTeleportAsync(string playerName, Waypoint? coords = null, string? destinationPlayer = null)
    {
        var payload = new TeleportRequestPayload
        {
            PlayerName = playerName
        };

        if (destinationPlayer is not null)
        {
            payload.DestinationPlayer = destinationPlayer;
        }
        else if (coords is not null)
        {
            payload.X = coords.X;
            payload.Y = coords.Y;
            payload.Z = coords.Z;
        }

        var message = new NetworkMessage
        {
            Type = "TeleportRequest",
            Payload = JsonSerializer.SerializeToElement(payload, JsonOptions)
        };
        await _webSocketService.SendMessageAsync(message);
    }

    private void PersistWaypoints()
    {
        WaypointManager.Save(SavedWaypoints.ToList());
    }

    private void OnMessageReceived(object? sender, NetworkMessage message)
    {
        if (message.Type == "PlayerListUpdate")
            HandlePlayerListUpdate(message.Payload);
    }

    private void HandlePlayerListUpdate(JsonElement payload)
    {
        var playerList = payload.Deserialize<PlayerListPayload>(JsonOptions);
        if (playerList is null) return;

        Application.Current?.Dispatcher?.Invoke(() =>
        {
            var prevSelected = SelectedPlayer;
            var prevDest = DestinationPlayer;

            OnlinePlayers.Clear();
            foreach (var p in playerList.Players)
                OnlinePlayers.Add(p.Name);

            if (prevSelected is not null && OnlinePlayers.Contains(prevSelected))
                SelectedPlayer = prevSelected;
            else
                SelectedPlayer = OnlinePlayers.FirstOrDefault();

            if (prevDest is not null && OnlinePlayers.Contains(prevDest))
                DestinationPlayer = prevDest;
            else
                DestinationPlayer = OnlinePlayers.Count > 1 ? OnlinePlayers[1] : OnlinePlayers.FirstOrDefault();
        });
    }

    public void ResetState()
    {
        _webSocketService.MessageReceived -= OnMessageReceived;
        OnlinePlayers.Clear();
        SelectedPlayer = null;
        DestinationPlayer = null;
        SelectedWaypoint = null;
        StatusMessage = string.Empty;
    }

    public void Resubscribe()
    {
        _webSocketService.MessageReceived -= OnMessageReceived;
        _webSocketService.MessageReceived += OnMessageReceived;
    }
}
