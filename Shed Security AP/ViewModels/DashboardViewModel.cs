using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Shed_Security_AP.Core;
using Shed_Security_AP.Models.Network;
using Shed_Security_AP.Services;
using SkiaSharp;

namespace Shed_Security_AP.ViewModels;

/// <summary>
/// The main control panel. Shows live TPS/RAM/uptime sparklines, the online player
/// roster with kick/ban/inspect actions, a server console with command input, and
/// quick-action buttons for common admin tasks. Basically the home screen.
/// </summary>
public class DashboardViewModel : ViewModelBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IWebSocketService _webSocketService;
    private readonly IAuditService _auditService;

    private const int MaxHistoryPoints = 60;

    private static readonly SKColor AccentSkColor = SKColor.Parse("#10B981");

    private readonly ObservableCollection<ObservableValue> _tpsValues = [];
    private readonly ObservableCollection<ObservableValue> _ramValues = [];

    private string _tps = "--";
    private string _ramUsage = "--";
    private string _uptime = "--";
    private string _commandText = string.Empty;

    public string Tps
    {
        get => _tps;
        set => SetProperty(ref _tps, value);
    }

    public string RamUsage
    {
        get => _ramUsage;
        set => SetProperty(ref _ramUsage, value);
    }

    public string Uptime
    {
        get => _uptime;
        set => SetProperty(ref _uptime, value);
    }

    public string CommandText
    {
        get => _commandText;
        set => SetProperty(ref _commandText, value);
    }

    public ISeries[] TpsSeries { get; set; }
    public ISeries[] RamSeries { get; set; }

    public Axis[] HiddenXAxes { get; } =
    [
        new Axis
        {
            IsVisible = false,
            ShowSeparatorLines = false
        }
    ];

    public Axis[] HiddenYAxes { get; } =
    [
        new Axis
        {
            IsVisible = false,
            ShowSeparatorLines = false
        }
    ];

    private const int MaxConsoleLines = 100;

    public ObservableCollection<string> ConsoleLines { get; } = [];

    public ObservableCollection<PlayerInfo> Players { get; } = [];

    public ICommand SendCommand { get; }
    public ICommand KickPlayerCommand { get; }
    public ICommand BanPlayerCommand { get; }
    public ICommand MutePlayerCommand { get; }
    public ICommand RestartServerCommand { get; }
    public ICommand StopServerCommand { get; }
    public ICommand TimeMorningCommand { get; }
    public ICommand ClearWeatherCommand { get; }
    public ICommand SaveWorldCommand { get; }
    public ICommand ClearEntitiesCommand { get; }
    public ICommand InspectPlayerCommand { get; }
    public ICommand CloseModalCommand { get; }
    public ICommand InspectInventoryCommand { get; }

    private bool _isInspectModalOpen;
    public bool IsInspectModalOpen
    {
        get => _isInspectModalOpen;
        set => SetProperty(ref _isInspectModalOpen, value);
    }

    private PlayerDossier? _selectedPlayerDossier;
    public PlayerDossier? SelectedPlayerDossier
    {
        get => _selectedPlayerDossier;
        set => SetProperty(ref _selectedPlayerDossier, value);
    }

    public ObservableCollection<InventorySlot> PlayerInventory { get; } = [];

    private bool _hasInventoryItems;
    public bool HasInventoryItems
    {
        get => _hasInventoryItems;
        set => SetProperty(ref _hasInventoryItems, value);
    }

    public DashboardViewModel(IWebSocketService webSocketService, IAuditService auditService)
    {
        _webSocketService = webSocketService;
        _auditService = auditService;
        _webSocketService.MessageReceived += OnMessageReceived;

        TpsSeries = BuildSparklineSeries(_tpsValues);
        RamSeries = BuildSparklineSeries(_ramValues);

        KickPlayerCommand = new AsyncRelayCommand(
            async param =>
            {
                if (param is PlayerInfo player)
                {
                    await SendPlayerActionAsync("Kick", player.Name);
                    _auditService.LogAction("Kick", player.Name, "");
                }
            });

        BanPlayerCommand = new AsyncRelayCommand(
            async param =>
            {
                if (param is PlayerInfo player)
                {
                    await SendPlayerActionAsync("Ban", player.Name);
                    _auditService.LogAction("Ban", player.Name, "");
                }
            });

        MutePlayerCommand = new AsyncRelayCommand(
            async param =>
            {
                if (param is PlayerInfo player)
                {
                    await SendPlayerActionAsync("Mute", player.Name);
                    _auditService.LogAction("Mute", player.Name, "");
                }
            });

        SendCommand = new AsyncRelayCommand(
            async _ =>
            {
                if (string.IsNullOrWhiteSpace(CommandText))
                    return;

                var text = CommandText;
                CommandText = string.Empty;

                AppendConsoleLine($"[Command] > {text}");
                _auditService.LogAction("Console", "", text);

                var message = new NetworkMessage
                {
                    Type = "ConsoleCommand",
                    Payload = JsonSerializer.SerializeToElement(
                        new CommandPayload { Command = text }, JsonOptions)
                };
                await _webSocketService.SendMessageAsync(message);
            },
            _ => !string.IsNullOrWhiteSpace(CommandText));

        RestartServerCommand = new AsyncRelayCommand(async _ =>
        {
            _auditService.LogAction("Restart", "Server", "");
            AppendConsoleLine("[Power] Restarting server...");
            var message = new NetworkMessage
            {
                Type = "ConsoleCommand",
                Payload = JsonSerializer.SerializeToElement(
                    new CommandPayload { Command = "/shed restart" }, JsonOptions)
            };
            await _webSocketService.SendMessageAsync(message);
        });

        StopServerCommand = new AsyncRelayCommand(async _ =>
        {
            _auditService.LogAction("Stop", "Server", "");
            AppendConsoleLine("[Power] Stopping server...");
            var message = new NetworkMessage
            {
                Type = "ConsoleCommand",
                Payload = JsonSerializer.SerializeToElement(
                    new CommandPayload { Command = "/stop" }, JsonOptions)
            };
            await _webSocketService.SendMessageAsync(message);
        });

        TimeMorningCommand = CreateQuickAction("/time morning", "Set time to morning");
        ClearWeatherCommand = CreateQuickAction("/weather clear", "Clear weather");
        SaveWorldCommand = CreateQuickAction("/save", "Save world");
        ClearEntitiesCommand = CreateQuickAction("/entity removeitem drop", "Clear item drops");

        InspectPlayerCommand = new AsyncRelayCommand(async param =>
        {
            if (param is not PlayerInfo player) return;

            AppendConsoleLine($"[Inspect] Requesting dossier for {player.Name}...");
            var message = new NetworkMessage
            {
                Type = "PlayerInspectRequest",
                Payload = JsonSerializer.SerializeToElement(
                    new PlayerInspectRequestPayload { PlayerName = player.Name }, JsonOptions)
            };
            await _webSocketService.SendMessageAsync(message);
        });

        CloseModalCommand = new RelayCommand(_ => IsInspectModalOpen = false);

        InspectInventoryCommand = new AsyncRelayCommand(async _ =>
        {
            if (SelectedPlayerDossier is null) return;

            AppendConsoleLine($"[Inspect] Requesting inventory for {SelectedPlayerDossier.PlayerName}...");
            var message = new NetworkMessage
            {
                Type = "InventoryRequest",
                Payload = JsonSerializer.SerializeToElement(
                    new InventoryRequestPayload { PlayerName = SelectedPlayerDossier.PlayerName }, JsonOptions)
            };
            await _webSocketService.SendMessageAsync(message);
        });
    }

    private AsyncRelayCommand CreateQuickAction(string command, string label)
    {
        return new AsyncRelayCommand(async _ =>
        {
            _auditService.LogAction("Quick Action", "", label);
            AppendConsoleLine($"[Quick Action] {label}");
            var message = new NetworkMessage
            {
                Type = "ConsoleCommand",
                Payload = JsonSerializer.SerializeToElement(
                    new CommandPayload { Command = command }, JsonOptions)
            };
            await _webSocketService.SendMessageAsync(message);
        });
    }

    private void OnMessageReceived(object? sender, NetworkMessage message)
    {
        switch (message.Type)
        {
            case "MetricsUpdate":
                HandleMetricsUpdate(message.Payload);
                break;
            case "PlayerListUpdate":
                HandlePlayerListUpdate(message.Payload);
                break;
            case "ConsoleLog":
                HandleConsoleLog(message.Payload);
                break;
            case "PlayerInspectResponse":
                HandlePlayerInspectResponse(message.Payload);
                break;
            case "InventoryResponse":
                HandleInventoryResponse(message.Payload);
                break;
        }
    }

    private void HandleMetricsUpdate(JsonElement payload)
    {
        var metrics = payload.Deserialize<MetricsPayload>(JsonOptions);
        if (metrics is null) return;

        Application.Current?.Dispatcher?.Invoke(() =>
        {
            Tps = metrics.Tps;
            RamUsage = metrics.RamUsage;
            Uptime = metrics.Uptime;

            if (double.TryParse(metrics.Tps, NumberStyles.Float, CultureInfo.InvariantCulture, out var tpsVal))
                PushValue(_tpsValues, tpsVal);

            var ramVal = ParseRamToMb(metrics.RamUsage);
            if (ramVal > 0)
                PushValue(_ramValues, ramVal);
        });
    }

    private static void PushValue(ObservableCollection<ObservableValue> collection, double value)
    {
        collection.Add(new ObservableValue(value));
        if (collection.Count > MaxHistoryPoints)
            collection.RemoveAt(0);
    }

    private static double ParseRamToMb(string ram)
    {
        if (string.IsNullOrWhiteSpace(ram) || ram == "--")
            return 0;

        var trimmed = ram.Trim();
        if (trimmed.EndsWith("GB", StringComparison.OrdinalIgnoreCase))
        {
            var num = trimmed[..^2].Trim();
            return double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out var gb) ? gb * 1024.0 : 0;
        }

        if (trimmed.EndsWith("MB", StringComparison.OrdinalIgnoreCase))
        {
            var num = trimmed[..^2].Trim();
            return double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out var mb) ? mb : 0;
        }

        return 0;
    }

    private static ISeries[] BuildSparklineSeries(ObservableCollection<ObservableValue> values)
    {
        return
        [
            new LineSeries<ObservableValue>
            {
                Values = values,
                GeometrySize = 0,
                GeometryStroke = null,
                GeometryFill = null,
                Stroke = new SolidColorPaint(AccentSkColor, 2),
                Fill = new SolidColorPaint(AccentSkColor.WithAlpha(30)),
                LineSmoothness = 0.65,
                AnimationsSpeed = TimeSpan.FromMilliseconds(150),
                IsHoverable = false
            }
        ];
    }

    private void HandlePlayerListUpdate(JsonElement payload)
    {
        var playerList = payload.Deserialize<PlayerListPayload>(JsonOptions);
        if (playerList is null) return;

        Application.Current?.Dispatcher?.Invoke(() =>
        {
            Players.Clear();
            foreach (var p in playerList.Players)
            {
                Players.Add(new PlayerInfo(p.Name, p.Ping));
            }
        });
    }

    private void HandleConsoleLog(JsonElement payload)
    {
        var log = payload.Deserialize<ConsoleLogPayload>(JsonOptions);
        if (log is null || string.IsNullOrEmpty(log.Message)) return;

        Application.Current?.Dispatcher?.Invoke(() => AppendConsoleLine(log.Message));
    }

    private void HandlePlayerInspectResponse(JsonElement payload)
    {
        var data = payload.Deserialize<PlayerInspectResponsePayload>(JsonOptions);
        if (data is null) return;

        Application.Current?.Dispatcher?.Invoke(() =>
        {
            SelectedPlayerDossier = new PlayerDossier(
                data.PlayerName, data.IpAddress, data.Playtime,
                data.Position, data.TotalStrikes);
            PlayerInventory.Clear();
            HasInventoryItems = false;
            IsInspectModalOpen = true;
        });
    }

    private void HandleInventoryResponse(JsonElement payload)
    {
        var data = payload.Deserialize<InventoryResponsePayload>(JsonOptions);
        if (data is null) return;

        Application.Current?.Dispatcher?.Invoke(() =>
        {
            PlayerInventory.Clear();
            foreach (var slot in data.Slots)
                PlayerInventory.Add(slot);
            HasInventoryItems = PlayerInventory.Count > 0;
        });
    }

    private void AppendConsoleLine(string line)
    {
        ConsoleLines.Add(line);
        while (ConsoleLines.Count > MaxConsoleLines)
            ConsoleLines.RemoveAt(0);
    }

    public void ResetState()
    {
        _webSocketService.MessageReceived -= OnMessageReceived;

        Tps = "--";
        RamUsage = "--";
        Uptime = "--";
        CommandText = string.Empty;
        ConsoleLines.Clear();
        Players.Clear();
        _tpsValues.Clear();
        _ramValues.Clear();
        IsInspectModalOpen = false;
        SelectedPlayerDossier = null;
        PlayerInventory.Clear();
        HasInventoryItems = false;
    }

    public void Resubscribe()
    {
        _webSocketService.MessageReceived -= OnMessageReceived;
        _webSocketService.MessageReceived += OnMessageReceived;
    }

    private async Task SendPlayerActionAsync(string actionType, string targetPlayer, string reason = "")
    {
        AppendConsoleLine($"[Action] {actionType} → {targetPlayer}");

        var message = new NetworkMessage
        {
            Type = "PlayerAction",
            Payload = JsonSerializer.SerializeToElement(
                new PlayerActionPayload
                {
                    ActionType = actionType,
                    TargetPlayer = targetPlayer,
                    Reason = reason
                }, JsonOptions)
        };
        await _webSocketService.SendMessageAsync(message);
    }
}

/// <summary>
/// Lightweight player entry for the online roster. Just name and ping —
/// enough to populate the player list without pulling heavy data.
/// </summary>
public class PlayerInfo(string name, int ping)
{
    public string Name { get; } = name;
    public int Ping { get; } = ping;
}

/// <summary>
/// The detailed player info card shown in the inspect modal.
/// Immutable because it's a point-in-time snapshot from the server.
/// </summary>
public class PlayerDossier(string playerName, string ipAddress, string playtime, string position, int totalStrikes)
{
    public string PlayerName { get; } = playerName;
    public string IpAddress { get; } = ipAddress;
    public string Playtime { get; } = playtime;
    public string Position { get; } = position;
    public int TotalStrikes { get; } = totalStrikes;
}
