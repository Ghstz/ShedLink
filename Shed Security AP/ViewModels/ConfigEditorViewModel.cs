using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Shed_Security_AP.Core;
using Shed_Security_AP.Models.Network;
using Shed_Security_AP.Services;

namespace Shed_Security_AP.ViewModels;

/// <summary>
/// A remote JSON config editor. Fetches the list of server config files,
/// loads whichever one you pick into a text editor, and saves changes back
/// to the server. Heads up — most config changes need a server restart to take effect.
/// </summary>
public class ConfigEditorViewModel : ViewModelBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IWebSocketService _webSocketService;
    private readonly IAuditService _auditService;

    private string _configJson = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private string? _selectedConfig;

    public ObservableCollection<string> AvailableConfigs { get; } = [];

    public string? SelectedConfig
    {
        get => _selectedConfig;
        set
        {
            if (SetProperty(ref _selectedConfig, value) && !string.IsNullOrEmpty(value))
                _ = FetchSelectedConfigAsync();
        }
    }

    public string ConfigJson
    {
        get => _configJson;
        set => SetProperty(ref _configJson, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public ICommand SaveConfigCommand { get; }

    public ConfigEditorViewModel(IWebSocketService webSocketService, IAuditService auditService)
    {
        _webSocketService = webSocketService;
        _auditService = auditService;
        _webSocketService.MessageReceived += OnMessageReceived;

        SaveConfigCommand = new AsyncRelayCommand(async _ =>
        {
            if (string.IsNullOrWhiteSpace(ConfigJson) || string.IsNullOrEmpty(SelectedConfig))
                return;

            IsBusy = true;
            StatusMessage = "Saving config...";

            var message = new NetworkMessage
            {
                Type = "ConfigSave",
                Payload = JsonSerializer.SerializeToElement(
                    new ConfigSavePayload { FileName = SelectedConfig, JsonContent = ConfigJson }, JsonOptions)
            };
            await _webSocketService.SendMessageAsync(message);

            _auditService.LogAction("Config Save", SelectedConfig, "");
            StatusMessage = "Config saved.";
            IsBusy = false;
        }, _ => !string.IsNullOrWhiteSpace(ConfigJson) && !string.IsNullOrEmpty(SelectedConfig));
    }

    private async Task FetchSelectedConfigAsync()
    {
        IsBusy = true;
        StatusMessage = $"Fetching {SelectedConfig}...";

        var message = new NetworkMessage
        {
            Type = "ConfigRequest",
            Payload = JsonSerializer.SerializeToElement(
                new ConfigRequestPayload { FileName = SelectedConfig! }, JsonOptions)
        };
        await _webSocketService.SendMessageAsync(message);
    }

    private void OnMessageReceived(object? sender, NetworkMessage message)
    {
        switch (message.Type)
        {
            case "ConfigResponse":
                HandleConfigResponse(message.Payload);
                break;
            case "ConfigListResponse":
                HandleConfigListResponse(message.Payload);
                break;
        }
    }

    private void HandleConfigResponse(JsonElement payload)
    {
        var response = payload.Deserialize<ConfigResponsePayload>(JsonOptions);
        if (response is null) return;

        Application.Current?.Dispatcher?.Invoke(() =>
        {
            ConfigJson = response.JsonContent;
            StatusMessage = "Config loaded.";
            IsBusy = false;
        });
    }

    private void HandleConfigListResponse(JsonElement payload)
    {
        var response = payload.Deserialize<ConfigListResponsePayload>(JsonOptions);
        if (response is null) return;

        Application.Current?.Dispatcher?.Invoke(() =>
        {
            AvailableConfigs.Clear();
            foreach (var file in response.Files)
                AvailableConfigs.Add(file);

            if (AvailableConfigs.Count > 0)
                SelectedConfig = AvailableConfigs[0];

            StatusMessage = $"{AvailableConfigs.Count} config(s) found.";
        });
    }

    public void ResetState()
    {
        _webSocketService.MessageReceived -= OnMessageReceived;
        AvailableConfigs.Clear();
        _selectedConfig = null;
        ConfigJson = string.Empty;
        StatusMessage = string.Empty;
        IsBusy = false;
    }

    public async void Resubscribe()
    {
        _webSocketService.MessageReceived -= OnMessageReceived;
        _webSocketService.MessageReceived += OnMessageReceived;

        var message = new NetworkMessage
        {
            Type = "ConfigListRequest",
            Payload = JsonSerializer.SerializeToElement(new ConfigListRequestPayload(), JsonOptions)
        };
        await _webSocketService.SendMessageAsync(message);
    }
}
