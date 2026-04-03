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
/// Manages the server's whitelist and ban list. Fetches the current lists on demand,
/// lets you add/remove players, and pushes changes to the server immediately.
/// The server reads from the actual VS whitelist and ban files, so changes
/// take effect right away without a restart.
/// </summary>
public class AccessControlViewModel : ViewModelBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IWebSocketService _webSocketService;
    private readonly IAuditService _auditService;

    public ObservableCollection<string> Whitelist { get; } = [];
    public ObservableCollection<string> Banlist { get; } = [];

    private string _newWhitelistName = string.Empty;
    public string NewWhitelistName
    {
        get => _newWhitelistName;
        set => SetProperty(ref _newWhitelistName, value);
    }

    private string _newBanName = string.Empty;
    public string NewBanName
    {
        get => _newBanName;
        set => SetProperty(ref _newBanName, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand AddWhitelistCommand { get; }
    public ICommand RemoveWhitelistCommand { get; }
    public ICommand AddBanCommand { get; }
    public ICommand RemoveBanCommand { get; }

    public AccessControlViewModel(IWebSocketService webSocketService, IAuditService auditService)
    {
        _webSocketService = webSocketService;
        _auditService = auditService;
        _webSocketService.MessageReceived += OnMessageReceived;

        RefreshCommand = new AsyncRelayCommand(
            async _ =>
            {
                await _webSocketService.SendMessageAsync(new NetworkMessage
                {
                    Type = "AccessControlRequest",
                    Payload = JsonSerializer.SerializeToElement(new { })
                });
            });

        AddWhitelistCommand = new AsyncRelayCommand(
            async _ =>
            {
                await SendActionAsync("AddWhitelist", NewWhitelistName.Trim());
                NewWhitelistName = string.Empty;
            },
            _ => !string.IsNullOrWhiteSpace(NewWhitelistName));

        RemoveWhitelistCommand = new AsyncRelayCommand(
            async param =>
            {
                if (param is string name)
                    await SendActionAsync("RemoveWhitelist", name);
            });

        AddBanCommand = new AsyncRelayCommand(
            async _ =>
            {
                await SendActionAsync("AddBan", NewBanName.Trim());
                NewBanName = string.Empty;
            },
            _ => !string.IsNullOrWhiteSpace(NewBanName));

        RemoveBanCommand = new AsyncRelayCommand(
            async param =>
            {
                if (param is string name)
                    await SendActionAsync("RemoveBan", name);
            });
    }

    private async Task SendActionAsync(string actionType, string playerName)
    {
        var message = new NetworkMessage
        {
            Type = "AccessControlAction",
            Payload = JsonSerializer.SerializeToElement(
                new AccessControlActionPayload
                {
                    ActionType = actionType,
                    PlayerName = playerName
                }, JsonOptions)
        };
        await _webSocketService.SendMessageAsync(message);
        _auditService.LogAction(actionType, playerName, "");
        StatusMessage = $"{actionType}: {playerName}";
    }

    private void OnMessageReceived(object? sender, NetworkMessage message)
    {
        if (message.Type == "AccessControlList")
            HandleAccessControlList(message.Payload);
    }

    private void HandleAccessControlList(JsonElement payload)
    {
        var data = payload.Deserialize<AccessControlListPayload>(JsonOptions);
        if (data is null) return;

        Application.Current?.Dispatcher?.Invoke(() =>
        {
            Whitelist.Clear();
            foreach (var name in data.Whitelist)
                Whitelist.Add(name);

            Banlist.Clear();
            foreach (var name in data.Banlist)
                Banlist.Add(name);
        });
    }

    public void ResetState()
    {
        _webSocketService.MessageReceived -= OnMessageReceived;

        Whitelist.Clear();
        Banlist.Clear();
        NewWhitelistName = string.Empty;
        NewBanName = string.Empty;
        StatusMessage = string.Empty;
    }

    public void Resubscribe()
    {
        _webSocketService.MessageReceived -= OnMessageReceived;
        _webSocketService.MessageReceived += OnMessageReceived;
    }
}
