using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Shed_Security_AP.Core;
using Shed_Security_AP.Models.Network;
using Shed_Security_AP.Services;
using SkiaSharp;

namespace Shed_Security_AP.ViewModels;

/// <summary>
/// Shows server entity stats — loaded entities, dropped items, hostile mobs —
/// plus a pie chart breaking down entity types. The "nuke" buttons let you
/// mass-despawn items or hostiles when the server is lagging from entity overload.
/// </summary>
public class DiagnosticsViewModel : ViewModelBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IWebSocketService _webSocketService;
    private readonly IAuditService _auditService;

    private static readonly SKColor[] PieColors =
    [
        SKColor.Parse("#10B981"),  // Emerald (accent)
        SKColor.Parse("#3B82F6"),  // Blue
        SKColor.Parse("#F59E0B"),  // Amber
        SKColor.Parse("#EF4444"),  // Red
        SKColor.Parse("#8B5CF6"),  // Violet
        SKColor.Parse("#6B7280")   // Gray
    ];

    public SolidColorPaint LegendTextPaint { get; } = new(SKColor.Parse("#D1D5DB"));

    private string _loadedChunks = "--";
    public string LoadedChunks
    {
        get => _loadedChunks;
        set => SetProperty(ref _loadedChunks, value);
    }

    private string _droppedItems = "--";
    public string DroppedItems
    {
        get => _droppedItems;
        set => SetProperty(ref _droppedItems, value);
    }

    private string _hostileMobs = "--";
    public string HostileMobs
    {
        get => _hostileMobs;
        set => SetProperty(ref _hostileMobs, value);
    }

    private ISeries[] _tickProfileSeries = [];
    public ISeries[] TickProfileSeries
    {
        get => _tickProfileSeries;
        set => SetProperty(ref _tickProfileSeries, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand NukeItemsCommand { get; }
    public ICommand NukeHostilesCommand { get; }

    public DiagnosticsViewModel(IWebSocketService webSocketService, IAuditService auditService)
    {
        _webSocketService = webSocketService;
        _auditService = auditService;
        _webSocketService.MessageReceived += OnMessageReceived;

        RefreshCommand = new AsyncRelayCommand(async _ =>
        {
            var message = new NetworkMessage
            {
                Type = "DiagnosticsRequest",
                Payload = JsonSerializer.SerializeToElement(new { }, JsonOptions)
            };
            await _webSocketService.SendMessageAsync(message);
        });

        NukeItemsCommand = new AsyncRelayCommand(async _ =>
        {
            _auditService.LogAction("Nuke Items", "", "");
            var message = new NetworkMessage
            {
                Type = "DespawnDroppedItems",
                Payload = JsonSerializer.SerializeToElement(new { })
            };
            await _webSocketService.SendMessageAsync(message);
        });

        NukeHostilesCommand = new AsyncRelayCommand(async _ =>
        {
            _auditService.LogAction("Nuke Hostiles", "", "");
            var message = new NetworkMessage
            {
                Type = "DespawnHostiles",
                Payload = JsonSerializer.SerializeToElement(new { })
            };
            await _webSocketService.SendMessageAsync(message);
        });
    }

    private void OnMessageReceived(object? sender, NetworkMessage message)
    {
        if (message.Type == "DiagnosticsResponse")
            HandleDiagnosticsResponse(message.Payload);
    }

    private void HandleDiagnosticsResponse(JsonElement payload)
    {
        var data = payload.Deserialize<DiagnosticsDataPayload>(JsonOptions);
        if (data is null) return;

        Application.Current?.Dispatcher?.Invoke(() =>
        {
            LoadedChunks = data.LoadedChunks.ToString("N0");
            DroppedItems = data.DroppedItems.ToString("N0");
            HostileMobs = data.HostileMobs.ToString("N0");

            var series = new ISeries[data.TickProfile.Count];
            var i = 0;
            foreach (var kvp in data.TickProfile)
            {
                var color = PieColors[i % PieColors.Length];
                series[i] = new PieSeries<double>
                {
                    Name = kvp.Key,
                    Values = [kvp.Value],
                    Fill = new SolidColorPaint(color),
                    Stroke = null,
                    Pushout = 0,
                    InnerRadius = 40,
                    IsHoverable = true
                };
                i++;
            }
            TickProfileSeries = series;
        });
    }

    public void ResetState()
    {
        _webSocketService.MessageReceived -= OnMessageReceived;

        LoadedChunks = "--";
        DroppedItems = "--";
        HostileMobs = "--";
        TickProfileSeries = [];
    }

    public void Resubscribe()
    {
        _webSocketService.MessageReceived -= OnMessageReceived;
        _webSocketService.MessageReceived += OnMessageReceived;
    }
}
