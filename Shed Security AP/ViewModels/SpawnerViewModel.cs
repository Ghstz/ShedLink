using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Shed_Security_AP.Core;
using Shed_Security_AP.Models.Network;
using Shed_Security_AP.Services;

namespace Shed_Security_AP.ViewModels;

/// <summary>
/// The item spawner. Pulls the full item/block registry from the server on connect,
/// lets you search and filter through it, pick a player, and drop items into their
/// inventory. The item list is dynamic — whatever mods the server has loaded,
/// those items show up here automatically.
/// </summary>
public class SpawnerViewModel : ViewModelBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IWebSocketService _webSocketService;
    private readonly IAuditService _auditService;

    private string[] _allItems = [];

    public ObservableCollection<string> OnlinePlayers { get; } = [];
    public ObservableCollection<string> FilteredItems { get; } = [];

    private string? _selectedPlayer;
    public string? SelectedPlayer
    {
        get => _selectedPlayer;
        set => SetProperty(ref _selectedPlayer, value);
    }

    private string _itemSearchText = string.Empty;
    public string ItemSearchText
    {
        get => _itemSearchText;
        set
        {
            if (SetProperty(ref _itemSearchText, value))
                ApplyItemFilter();
        }
    }

    private string? _selectedItem;
    public string? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    private int _spawnQuantity = 1;
    public int SpawnQuantity
    {
        get => _spawnQuantity;
        set => SetProperty(ref _spawnQuantity, Math.Max(1, value));
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand SpawnCommand { get; }

    public SpawnerViewModel(IWebSocketService webSocketService, IAuditService auditService)
    {
        _webSocketService = webSocketService;
        _auditService = auditService;
        _webSocketService.MessageReceived += OnMessageReceived;

        ApplyItemFilter();
        _ = RequestItemListAsync();

        SpawnCommand = new AsyncRelayCommand(
            async _ =>
            {
                var message = new NetworkMessage
                {
                    Type = "SpawnItem",
                    Payload = JsonSerializer.SerializeToElement(
                        new SpawnItemPayload
                        {
                            TargetPlayer = SelectedPlayer!,
                            ItemCode = SelectedItem!,
                            Quantity = SpawnQuantity
                        }, JsonOptions)
                };
                await _webSocketService.SendMessageAsync(message);

                StatusMessage = $"Spawned {SelectedItem} x{SpawnQuantity} → {SelectedPlayer}";
                _auditService.LogAction("Spawn Item", SelectedPlayer!, $"{SelectedItem} x{SpawnQuantity}");
            },
            _ => !string.IsNullOrEmpty(SelectedPlayer) && !string.IsNullOrEmpty(SelectedItem));
    }

    private void OnMessageReceived(object? sender, NetworkMessage message)
    {
        if (message.Type == "PlayerListUpdate")
            HandlePlayerListUpdate(message.Payload);
        else if (message.Type == "ItemListResponse")
            HandleItemListResponse(message.Payload);
    }

    private void HandleItemListResponse(JsonElement payload)
    {
        var response = payload.Deserialize<ItemListResponsePayload>(JsonOptions);
        if (response?.Items is null || response.Items.Length == 0) return;

        Application.Current?.Dispatcher?.Invoke(() =>
        {
            _allItems = response.Items;
            ApplyItemFilter();
        });
    }

    private void HandlePlayerListUpdate(JsonElement payload)
    {
        var playerList = payload.Deserialize<PlayerListPayload>(JsonOptions);
        if (playerList is null) return;

        Application.Current?.Dispatcher?.Invoke(() =>
        {
            var previousSelection = SelectedPlayer;
            OnlinePlayers.Clear();
            foreach (var p in playerList.Players)
                OnlinePlayers.Add(p.Name);

            if (previousSelection is not null && OnlinePlayers.Contains(previousSelection))
                SelectedPlayer = previousSelection;
            else
                SelectedPlayer = OnlinePlayers.FirstOrDefault();
        });
    }

    private void ApplyItemFilter()
    {
        var previousSelection = SelectedItem;

        FilteredItems.Clear();

        var search = _itemSearchText.Trim();
        foreach (var item in _allItems)
        {
            if (string.IsNullOrEmpty(search) ||
                item.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                FilteredItems.Add(item);
            }
        }

        if (previousSelection is not null && FilteredItems.Contains(previousSelection))
            SelectedItem = previousSelection;
        else
            SelectedItem = FilteredItems.FirstOrDefault();
    }

    private async Task RequestItemListAsync()
    {
        try
        {
            await _webSocketService.SendMessageAsync(new NetworkMessage
            {
                Type = "GetItemList",
                Payload = JsonSerializer.SerializeToElement(new { }, JsonOptions)
            });
        }
        catch
        {
            // Might not be connected yet — no big deal, we'll retry on resubscribe
        }
    }

    public void ResetState()
    {
        _webSocketService.MessageReceived -= OnMessageReceived;

        OnlinePlayers.Clear();
        SelectedPlayer = null;
        ItemSearchText = string.Empty;
        SpawnQuantity = 1;
        StatusMessage = string.Empty;
    }

    public void Resubscribe()
    {
        _webSocketService.MessageReceived -= OnMessageReceived;
        _webSocketService.MessageReceived += OnMessageReceived;
        _ = RequestItemListAsync();
    }
}
