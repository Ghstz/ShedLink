using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Shed_Security_AP.Core;
using Shed_Security_AP.Models.Network;
using Shed_Security_AP.Services;

namespace Shed_Security_AP.ViewModels;

/// <summary>
/// Powers the 2D radar map. Takes player positions from the server (relative to spawn)
/// and maps them onto a fixed-size canvas. Updates every telemetry tick so you get
/// a near-real-time bird's-eye view of where everyone is.
/// </summary>
public class RadarViewModel : ViewModelBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IWebSocketService _webSocketService;

    private const double WorldSize = 10000.0;
    private const double CanvasSize = 500.0;

    public ObservableCollection<MappedPlayer> MappedPlayers { get; } = [];

    private int _playerCount;
    public int PlayerCount
    {
        get => _playerCount;
        set => SetProperty(ref _playerCount, value);
    }

    public RadarViewModel(IWebSocketService webSocketService)
    {
        _webSocketService = webSocketService;
        _webSocketService.MessageReceived += OnMessageReceived;
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
            MappedPlayers.Clear();

            foreach (var p in playerList.Players)
            {
                var mapped = new MappedPlayer
                {
                    Name = p.Name,
                    WorldX = p.X,
                    WorldZ = p.Z,
                    CanvasX = WorldToCanvas(p.X),
                    CanvasY = WorldToCanvas(p.Z)
                };
                MappedPlayers.Add(mapped);
            }

            PlayerCount = MappedPlayers.Count;
        });
    }

    private static double WorldToCanvas(double worldCoord)
    {
        // Player coords are centered on 0,0 — shift them into canvas pixel space
        // so negative coords end up on the left/top side of the radar
        var half = WorldSize / 2.0;
        var clamped = Math.Clamp(worldCoord, -half, half);
        return ((clamped + half) / WorldSize) * CanvasSize;
    }

    public void ResetState()
    {
        _webSocketService.MessageReceived -= OnMessageReceived;
        MappedPlayers.Clear();
        PlayerCount = 0;
    }

    public void Resubscribe()
    {
        _webSocketService.MessageReceived -= OnMessageReceived;
        _webSocketService.MessageReceived += OnMessageReceived;
    }
}
