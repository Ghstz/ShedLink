using System;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Shed_Security_AP.Core;
using Shed_Security_AP.Models.Network;
using Shed_Security_AP.Services;

namespace Shed_Security_AP.ViewModels;

/// <summary>
/// World control panel — set the time of day with a slider, skip to morning,
/// force a temporal storm for fun (or testing), or cancel one that's wrecking
/// your players. Live world status (hour, season, storm countdown) updates
/// automatically from the server's telemetry feed.
/// </summary>
public class WorldControlViewModel : ViewModelBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IWebSocketService _webSocketService;
    private readonly IAuditService _auditService;

    private double _hourOfDay;
    public double HourOfDay
    {
        get => _hourOfDay;
        set => SetProperty(ref _hourOfDay, value);
    }

    private string _season = "—";
    public string Season
    {
        get => _season;
        set => SetProperty(ref _season, value);
    }

    private double _daysUntilStorm;
    public double DaysUntilStorm
    {
        get => _daysUntilStorm;
        set => SetProperty(ref _daysUntilStorm, value);
    }

    private string _stormStatus = "—";
    public string StormStatus
    {
        get => _stormStatus;
        set => SetProperty(ref _stormStatus, value);
    }

    private string _timeDisplay = "0:00";
    public string TimeDisplay
    {
        get => _timeDisplay;
        set => SetProperty(ref _timeDisplay, value);
    }

    private double _sliderHour;
    public double SliderHour
    {
        get => _sliderHour;
        set
        {
            if (SetProperty(ref _sliderHour, value))
            {
                var h = (int)value;
                var m = (int)((value - h) * 60);
                SliderTimeLabel = $"{h:D2}:{m:D2}";
            }
        }
    }

    private string _sliderTimeLabel = "00:00";
    public string SliderTimeLabel
    {
        get => _sliderTimeLabel;
        set => SetProperty(ref _sliderTimeLabel, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand SetTimeCommand { get; }
    public ICommand SkipNightCommand { get; }
    public ICommand ForceStormCommand { get; }
    public ICommand StopStormCommand { get; }

    public WorldControlViewModel(IWebSocketService webSocketService, IAuditService auditService)
    {
        _webSocketService = webSocketService;
        _auditService = auditService;
        _webSocketService.MessageReceived += OnMessageReceived;

        SetTimeCommand = new AsyncRelayCommand(
            async _ =>
            {
                await SendControlAsync("SetTime", SliderHour);
                _auditService.LogAction("Set Time", "", SliderTimeLabel);
                StatusMessage = $"Time set to {SliderTimeLabel}";
            });

        SkipNightCommand = new AsyncRelayCommand(
            async _ =>
            {
                await SendControlAsync("SetTime", 6.0);
                _auditService.LogAction("Skip Night", "", "06:00");
                StatusMessage = "Skipped to morning (06:00)";
            });

        ForceStormCommand = new AsyncRelayCommand(
            async _ =>
            {
                await SendControlAsync("ForceStorm", null);
                _auditService.LogAction("Force Storm", "", "");
                StatusMessage = "Temporal storm triggered!";
            });

        StopStormCommand = new AsyncRelayCommand(
            async _ =>
            {
                await SendControlAsync("StopStorm", null);
                _auditService.LogAction("Stop Storm", "", "");
                StatusMessage = "Temporal storm cancelled.";
            });
    }

    private async Task SendControlAsync(string action, double? value)
    {
        var message = new NetworkMessage
        {
            Type = "WorldControlRequest",
            Payload = JsonSerializer.SerializeToElement(
                new WorldControlRequestPayload
                {
                    Action = action,
                    Value = value
                }, JsonOptions)
        };
        await _webSocketService.SendMessageAsync(message);
    }

    private void OnMessageReceived(object? sender, NetworkMessage message)
    {
        if (message.Type == "WorldStatusUpdate")
            HandleWorldStatusUpdate(message.Payload);
    }

    private void HandleWorldStatusUpdate(JsonElement payload)
    {
        var data = payload.Deserialize<WorldStatusPayload>(JsonOptions);
        if (data is null) return;

        Application.Current?.Dispatcher?.Invoke(() =>
        {
            HourOfDay = data.HourOfDay;
            Season = string.IsNullOrEmpty(data.Season) ? "—" : data.Season;
            DaysUntilStorm = data.DaysUntilStorm;

            var h = (int)data.HourOfDay;
            var m = (int)((data.HourOfDay - h) * 60);
            TimeDisplay = $"{h:D2}:{m:D2}";

            StormStatus = data.IsStormActive
                ? "ACTIVE"
                : data.DaysUntilStorm < 1
                    ? "Imminent"
                    : $"In {data.DaysUntilStorm:F1} days";
        });
    }

    public void ResetState()
    {
        _webSocketService.MessageReceived -= OnMessageReceived;
        HourOfDay = 0;
        Season = "—";
        DaysUntilStorm = 0;
        StormStatus = "—";
        TimeDisplay = "0:00";
        SliderHour = 0;
        StatusMessage = string.Empty;
    }

    public void Resubscribe()
    {
        _webSocketService.MessageReceived -= OnMessageReceived;
        _webSocketService.MessageReceived += OnMessageReceived;
    }
}
