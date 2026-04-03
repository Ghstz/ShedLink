using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Shed_Security_AP.Core;
using Shed_Security_AP.Models.Network;
using Shed_Security_AP.Services;

namespace Shed_Security_AP.ViewModels;

/// <summary>
/// Manages server world backups. You can trigger a new backup, browse existing ones,
/// and download them as ZIP files. Downloads are chunked (the server sends base64
/// segments) and reassembled client-side, with a progress bar so you know it's working.
/// </summary>
public class BackupManagerViewModel : ViewModelBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IWebSocketService _webSocketService;
    private readonly IAuditService _auditService;

    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private bool _isCreating;
    private bool _isDownloading;
    private double _downloadProgress;
    private string? _downloadSavePath;

    public ObservableCollection<BackupInfo> Backups { get; } = [];

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

    public bool IsCreating
    {
        get => _isCreating;
        set => SetProperty(ref _isCreating, value);
    }

    public bool IsDownloading
    {
        get => _isDownloading;
        set => SetProperty(ref _isDownloading, value);
    }

    public double DownloadProgress
    {
        get => _downloadProgress;
        set => SetProperty(ref _downloadProgress, value);
    }

    public ICommand RefreshBackupsCommand { get; }
    public ICommand CreateBackupCommand { get; }
    public ICommand DownloadBackupCommand { get; }

    public BackupManagerViewModel(IWebSocketService webSocketService, IAuditService auditService)
    {
        _webSocketService = webSocketService;
        _auditService = auditService;
        _webSocketService.MessageReceived += OnMessageReceived;

        RefreshBackupsCommand = new AsyncRelayCommand(async _ =>
        {
            await RequestBackupListAsync();
        });

        CreateBackupCommand = new AsyncRelayCommand(async _ =>
        {
            _auditService.LogAction("Backup Create", "", "");
            IsCreating = true;
            StatusMessage = "Creating backup... This may take a moment.";

            var message = new NetworkMessage
            {
                Type = "BackupCreateRequest",
                Payload = JsonSerializer.SerializeToElement(new BackupCreateRequestPayload(), JsonOptions)
            };
            await _webSocketService.SendMessageAsync(message);
        }, _ => !IsCreating);

        DownloadBackupCommand = new AsyncRelayCommand(async param =>
        {
            if (param is not BackupInfo backup) return;

            var dialog = new SaveFileDialog
            {
                FileName = backup.Name,
                DefaultExt = ".zip",
                Filter = "ZIP Archives (*.zip)|*.zip"
            };

            if (dialog.ShowDialog() != true) return;

            _downloadSavePath = dialog.FileName;
            IsDownloading = true;
            DownloadProgress = 0;
            StatusMessage = $"Downloading {backup.Name}...";

            // Wipe any leftover partial download so we start clean
            if (File.Exists(_downloadSavePath))
                File.Delete(_downloadSavePath);

            var message = new NetworkMessage
            {
                Type = "FileDownloadRequest",
                Payload = JsonSerializer.SerializeToElement(new FileDownloadRequestPayload
                {
                    FileName = backup.Name
                }, JsonOptions)
            };
            await _webSocketService.SendMessageAsync(message);
        }, _ => !IsDownloading);
    }

    private async Task RequestBackupListAsync()
    {
        IsBusy = true;
        StatusMessage = "Fetching backup list...";

        var message = new NetworkMessage
        {
            Type = "BackupListRequest",
            Payload = JsonSerializer.SerializeToElement(new BackupListRequestPayload(), JsonOptions)
        };
        await _webSocketService.SendMessageAsync(message);
    }

    private void OnMessageReceived(object? sender, NetworkMessage message)
    {
        if (message.Type == "BackupListResponse")
            HandleBackupListResponse(message.Payload);
        else if (message.Type == "FileDownloadChunk")
            HandleFileDownloadChunk(message.Payload);
    }

    private void HandleBackupListResponse(JsonElement payload)
    {
        var response = payload.Deserialize<BackupListResponsePayload>(JsonOptions);
        if (response is null) return;

        Application.Current?.Dispatcher?.Invoke(() =>
        {
            Backups.Clear();
            foreach (var backup in response.Backups)
                Backups.Add(backup);

            StatusMessage = $"{Backups.Count} backup(s) found.";
            IsBusy = false;
            IsCreating = false;
        });
    }

    private void HandleFileDownloadChunk(JsonElement payload)
    {
        var chunk = payload.Deserialize<FileDownloadChunkPayload>(JsonOptions);
        if (chunk is null || string.IsNullOrWhiteSpace(_downloadSavePath)) return;

        try
        {
            var bytes = Convert.FromBase64String(chunk.Base64Data);

            using (var fs = new FileStream(_downloadSavePath, FileMode.Append, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes, 0, bytes.Length);
            }

            var progress = (double)(chunk.ChunkIndex + 1) / chunk.TotalChunks * 100.0;

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                DownloadProgress = progress;
                StatusMessage = $"Downloading {chunk.FileName}... {progress:F0}%";

                if (chunk.ChunkIndex + 1 >= chunk.TotalChunks)
                {
                    IsDownloading = false;
                    DownloadProgress = 100;
                    StatusMessage = $"Download complete: {chunk.FileName}";
                    _downloadSavePath = null;
                }
            });
        }
        catch (Exception ex)
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                IsDownloading = false;
                DownloadProgress = 0;
                StatusMessage = $"Download failed: {ex.Message}";
                _downloadSavePath = null;
            });
        }
    }

    public void ResetState()
    {
        _webSocketService.MessageReceived -= OnMessageReceived;
        Backups.Clear();
        StatusMessage = string.Empty;
        IsBusy = false;
        IsCreating = false;
        IsDownloading = false;
        DownloadProgress = 0;
        _downloadSavePath = null;
    }

    public async void Resubscribe()
    {
        _webSocketService.MessageReceived -= OnMessageReceived;
        _webSocketService.MessageReceived += OnMessageReceived;

        await RequestBackupListAsync();
    }
}
