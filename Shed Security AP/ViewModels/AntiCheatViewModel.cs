using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Shed_Security_AP.Core;
using Shed_Security_AP.Models.Local;
using Shed_Security_AP.Models.Network;
using Shed_Security_AP.Services;

namespace Shed_Security_AP.ViewModels;

/// <summary>
/// Listens for real-time security alerts from the server's anti-cheat system
/// and displays them in a scrolling feed. Also shows who's currently shadowbanned
/// with the option to pardon them. Fires desktop notifications when the app
/// isn't focused so you don't miss anything sketchy.
/// </summary>
public class AntiCheatViewModel : ViewModelBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IWebSocketService _webSocketService;
    private readonly INotificationService _notificationService;
    private readonly Func<bool> _isAppFocused;

    public ObservableCollection<SecurityAlert> LiveAlerts { get; } = [];
    public ObservableCollection<string> ShadowbannedPlayers { get; } = [];

    public ICommand PardonPlayerCommand { get; }

    private const int MaxAlerts = 200;

    public AntiCheatViewModel(IWebSocketService webSocketService, INotificationService notificationService, Func<bool> isAppFocused)
    {
        _webSocketService = webSocketService;
        _notificationService = notificationService;
        _isAppFocused = isAppFocused;
        _webSocketService.MessageReceived += OnMessageReceived;

        PardonPlayerCommand = new AsyncRelayCommand(async param =>
        {
            if (param is not string playerName)
                return;

            var message = new NetworkMessage
            {
                Type = "PlayerAction",
                Payload = JsonSerializer.SerializeToElement(
                    new PlayerActionPayload
                    {
                        ActionType = "Pardon",
                        TargetPlayer = playerName,
                        Reason = string.Empty
                    }, JsonOptions)
            };
            await _webSocketService.SendMessageAsync(message);

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                ShadowbannedPlayers.Remove(playerName);
            });
        });
    }

    private void OnMessageReceived(object? sender, NetworkMessage message)
    {
        switch (message.Type)
        {
            case "SecurityAlert":
                HandleSecurityAlert(message.Payload);
                break;
            case "ShadowbanListUpdate":
                HandleShadowbanListUpdate(message.Payload);
                break;
        }
    }

    private void HandleSecurityAlert(JsonElement payload)
    {
        var alert = payload.Deserialize<SecurityAlertPayload>(JsonOptions);
        if (alert is null) return;

        Application.Current?.Dispatcher?.Invoke(() =>
        {
            LiveAlerts.Insert(0, new SecurityAlert(
                alert.Timestamp, alert.PlayerName, alert.AlertType, alert.Details, alert.StrikeCount));

            while (LiveAlerts.Count > MaxAlerts)
                LiveAlerts.RemoveAt(LiveAlerts.Count - 1);

            if (LocalPreferences.EnableDesktopNotifications && !_isAppFocused())
            {
                var notificationText = alert.PlayerName == "System"
                    ? $"System Audit: {alert.Details}"
                    : $"{alert.PlayerName} ({alert.AlertType}): {alert.Details}";
                _notificationService.ShowAlert("Shed Security Alert", notificationText);
            }
        });
    }

    private void HandleShadowbanListUpdate(JsonElement payload)
    {
        var list = payload.Deserialize<ShadowbanListPayload>(JsonOptions);
        if (list is null) return;

        Application.Current?.Dispatcher?.Invoke(() =>
        {
            ShadowbannedPlayers.Clear();
            foreach (var player in list.Players)
                ShadowbannedPlayers.Add(player);
        });
    }

    public void ResetState()
    {
        _webSocketService.MessageReceived -= OnMessageReceived;
        LiveAlerts.Clear();
        ShadowbannedPlayers.Clear();
    }

    public void Resubscribe()
    {
        _webSocketService.MessageReceived -= OnMessageReceived;
        _webSocketService.MessageReceived += OnMessageReceived;
    }
}
