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
/// Remote mod manager. Lists every mod in the server's Mods directory with
/// enable/disable toggles (renames the file), plus a chunked upload feature
/// for pushing new mods to the server without FTP access. Changes need a
/// server restart to actually load/unload the mod.
/// </summary>
public class ModManagerViewModel : ViewModelBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IWebSocketService _webSocketService;
    private readonly IAuditService _auditService;

    private const int ChunkSize = 1_048_576; // 1 MB

    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private double _uploadProgress;
    private bool _isUploading;

    public ObservableCollection<ModInfo> Mods { get; } = [];

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

    public double UploadProgress
    {
        get => _uploadProgress;
        set => SetProperty(ref _uploadProgress, value);
    }

    public bool IsUploading
    {
        get => _isUploading;
        set => SetProperty(ref _isUploading, value);
    }

    public ICommand ToggleModCommand { get; }
    public ICommand RefreshModsCommand { get; }
    public ICommand UploadModCommand { get; }

    public ModManagerViewModel(IWebSocketService webSocketService, IAuditService auditService)
    {
        _webSocketService = webSocketService;
        _auditService = auditService;
        _webSocketService.MessageReceived += OnMessageReceived;

        ToggleModCommand = new AsyncRelayCommand(async param =>
        {
            if (param is not ModInfo mod)
                return;

            var message = new NetworkMessage
            {
                Type = "ModToggleRequest",
                Payload = JsonSerializer.SerializeToElement(
                    new ModToggleRequestPayload { FileName = mod.Name, Enable = !mod.IsEnabled }, JsonOptions)
            };
            await _webSocketService.SendMessageAsync(message);
                _auditService.LogAction("Mod Toggle", mod.Name, mod.IsEnabled ? "Disable" : "Enable");
        });

        RefreshModsCommand = new AsyncRelayCommand(async _ =>
        {
            await RequestModListAsync();
        });

        UploadModCommand = new AsyncRelayCommand(async _ =>
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Mod files (*.zip)|*.zip",
                Title = "Select a mod to upload"
            };

            if (dialog.ShowDialog() != true)
                return;

            await UploadFileAsync(dialog.FileName);
        }, _ => !IsUploading);
    }

    private async Task RequestModListAsync()
    {
        IsBusy = true;
        StatusMessage = "Fetching mod list...";

        var message = new NetworkMessage
        {
            Type = "ModListRequest",
            Payload = JsonSerializer.SerializeToElement(new ModListRequestPayload(), JsonOptions)
        };
        await _webSocketService.SendMessageAsync(message);
    }

    private void OnMessageReceived(object? sender, NetworkMessage message)
    {
        if (message.Type == "ModListResponse")
            HandleModListResponse(message.Payload);
    }

    private void HandleModListResponse(JsonElement payload)
    {
        var response = payload.Deserialize<ModListResponsePayload>(JsonOptions);
        if (response is null) return;

        Application.Current?.Dispatcher?.Invoke(() =>
        {
            Mods.Clear();
            foreach (var mod in response.Mods)
                Mods.Add(mod);

            StatusMessage = $"{Mods.Count} mod(s) found.";
            IsBusy = false;
        });
    }

    private async Task UploadFileAsync(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        byte[] fileBytes;

        try
        {
            fileBytes = await File.ReadAllBytesAsync(filePath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to read file: {ex.Message}";
            return;
        }

        IsUploading = true;
        UploadProgress = 0;

        var totalChunks = (int)Math.Ceiling((double)fileBytes.Length / ChunkSize);
        if (totalChunks == 0) totalChunks = 1;

        StatusMessage = $"Uploading {fileName}...";

        try
        {
            for (var i = 0; i < totalChunks; i++)
            {
                var offset = i * ChunkSize;
                var length = Math.Min(ChunkSize, fileBytes.Length - offset);
                var chunk = new byte[length];
                Array.Copy(fileBytes, offset, chunk, 0, length);

                var payload = new FileUploadChunkPayload
                {
                    FileName = fileName,
                    ChunkIndex = i,
                    TotalChunks = totalChunks,
                    Base64Data = Convert.ToBase64String(chunk)
                };

                var message = new NetworkMessage
                {
                    Type = "FileUploadChunk",
                    Payload = JsonSerializer.SerializeToElement(payload, JsonOptions)
                };

                await _webSocketService.SendMessageAsync(message);

                UploadProgress = (double)(i + 1) / totalChunks * 100;
                StatusMessage = $"Uploading {fileName}... {UploadProgress:F0}%";
            }

            StatusMessage = $"{fileName} uploaded successfully.";
            _auditService.LogAction("Mod Upload", fileName, "");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Upload failed: {ex.Message}";
        }
        finally
        {
            IsUploading = false;
        }
    }

    public void ResetState()
    {
        _webSocketService.MessageReceived -= OnMessageReceived;
        Mods.Clear();
        StatusMessage = string.Empty;
        IsBusy = false;
        IsUploading = false;
        UploadProgress = 0;
    }

    public async void Resubscribe()
    {
        _webSocketService.MessageReceived -= OnMessageReceived;
        _webSocketService.MessageReceived += OnMessageReceived;

        await RequestModListAsync();
    }
}
