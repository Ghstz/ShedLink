using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ShedLink.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace ShedLink;

/// <summary>
/// The beating heart of ShedLink. Spins up a WebSocket server alongside
/// the game server so the WPF dashboard can connect, authenticate, and
/// remote-control everything from player management to world time.
/// </summary>
public class ShedLinkSystem : ModSystem
{
    private const string ConfigFile = "shedlink.json";
    private const string LogPrefix = "[Shed Link]";
    private const string ShedSecurityPrefix = "[Shed Security]";

    private ICoreServerAPI _serverApi = null!;
    private ShedLinkConfig _config = null!;

    private HttpListener? _httpListener;
    private CancellationTokenSource? _cts;

    private readonly ConcurrentDictionary<string, ShedWebClient> _clients = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ───────────────────── Lifecycle ─────────────────────

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI api)
    {
        _serverApi = api;

        _config = api.LoadModConfig<ShedLinkConfig>(ConfigFile) ?? new ShedLinkConfig();
        api.StoreModConfig(_config, ConfigFile);

        if (_config.SecurityToken is "CHANGE_ME" or "")
        {
            api.Logger.Warning($"{LogPrefix} Security token is still the default! " +
                               $"Edit config/shedlink.json before exposing the port.");
        }

        StartListener();

        // Telemetry runs on its own thread so we never block the server tick loop
        _ = Task.Run(() => TelemetryLoopAsync(_cts!.Token));

        // Pipe every server log line to connected dashboards in real time
        api.Logger.EntryAdded += OnLogEntryAdded;

        // Make sure we tear down the listener even if Dispose doesn't fire
        api.Event.ServerRunPhase(EnumServerRunPhase.Shutdown, StopListener);
    }

    public override void Dispose()
    {
        StopListener();
        base.Dispose();
    }

    // ───────────────────── Listener ─────────────────────

    private void StartListener()
    {
        _cts = new CancellationTokenSource();
        _httpListener = new HttpListener();

        var prefix = $"http://*:{_config.DashboardPort}/shedlink/";
        _httpListener.Prefixes.Add(prefix);

        try
        {
            _httpListener.Start();
            _serverApi.Logger.Notification(
                $"{LogPrefix} Listening for dashboard connections on port {_config.DashboardPort}");

            // Accept loop runs off the main thread so the game keeps ticking
            _ = Task.Run(() => ListenLoopAsync(_cts.Token));
        }
        catch (HttpListenerException ex)
        {
            _serverApi.Logger.Error(
                $"{LogPrefix} Failed to start listener on {prefix} — {ex.Message}. " +
                "On Linux, ensure the port is allowed or run with appropriate permissions.");
        }
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // GetContextAsync doesn't accept a token, so WaitAsync is our escape hatch
                var context = await _httpListener!.GetContextAsync().WaitAsync(ct);

                if (!context.Request.IsWebSocketRequest)
                {
                    context.Response.StatusCode = 400;
                    context.Response.StatusDescription = "WebSocket upgrade required";
                    context.Response.Close();
                    continue;
                }

                var wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
                var clientIp = context.Request.RemoteEndPoint?.ToString() ?? "unknown";
                _serverApi.Logger.Notification(
                    $"{LogPrefix} Dashboard connected from {clientIp}");

                var client = new ShedWebClient(wsContext.WebSocket, clientIp);
                _clients.TryAdd(clientIp, client);
                _ = Task.Run(() => HandleClientAsync(client, ct));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                _serverApi.Logger.Error($"{LogPrefix} Listener error: {ex.Message}");
            }
        }
    }

    // ───────────────────── Client Session ─────────────────────

    private async Task HandleClientAsync(ShedWebClient client, CancellationToken ct)
    {
        var buffer = new byte[4096];

        try
        {
            while (client.Socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                NetworkMessage? message;
                try
                {
                    message = await ReadMessageAsync(client.Socket, buffer, ct);
                }
                catch (WebSocketException)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (message is null)
                    break;

                await ProcessMessageAsync(client, message, ct);
            }
        }
        catch (Exception ex)
        {
            _serverApi.Logger.Error($"{LogPrefix} Session error ({client.ClientIp}): {ex.Message}");
        }
        finally
        {
            _clients.TryRemove(client.ClientIp, out _);
            await CloseSocketAsync(client.Socket);
            _serverApi.Logger.Notification($"{LogPrefix} Dashboard disconnected ({client.ClientIp})");
        }
    }

    private async Task ProcessMessageAsync(ShedWebClient client, NetworkMessage message, CancellationToken ct)
    {
        switch (message.Type)
        {
            case "Auth":
                await HandleAuthAsync(client, message, ct);
                break;

            case "ConsoleCommand":
                if (client.IsAuthenticated)
                    await HandleConsoleCommandAsync(client, message);
                break;

            case "PlayerAction":
                if (client.IsAuthenticated)
                    await HandlePlayerActionAsync(client, message);
                break;

            case "ConfigRequest":
                if (client.IsAuthenticated)
                    await HandleConfigRequestAsync(client, message, ct);
                break;

            case "ConfigSave":
                if (client.IsAuthenticated)
                    await HandleConfigSaveAsync(client, message, ct);
                break;

            case "ConfigListRequest":
                if (client.IsAuthenticated)
                    await HandleConfigListRequestAsync(client, ct);
                break;

            case "PlayerInspectRequest":
                if (client.IsAuthenticated)
                    await HandlePlayerInspectAsync(client, message, ct);
                break;

            case "ModListRequest":
                if (client.IsAuthenticated)
                    await HandleModListRequestAsync(client, ct);
                break;

            case "ModToggleRequest":
                if (client.IsAuthenticated)
                    await HandleModToggleRequestAsync(client, message, ct);
                break;

            case "FileUploadChunk":
                if (client.IsAuthenticated)
                    await HandleFileUploadChunkAsync(client, message, ct);
                break;

            case "BackupListRequest":
                if (client.IsAuthenticated)
                    await HandleBackupListRequestAsync(client, ct);
                break;

            case "BackupCreateRequest":
                if (client.IsAuthenticated)
                    await HandleBackupCreateRequestAsync(client, ct);
                break;

            case "FileDownloadRequest":
                if (client.IsAuthenticated)
                    await HandleFileDownloadRequestAsync(client, message, ct);
                break;

            case "InventoryRequest":
                if (client.IsAuthenticated)
                    await HandleInventoryRequestAsync(client, message, ct);
                break;

            case "DiagnosticsRequest":
                if (client.IsAuthenticated)
                    await HandleDiagnosticsRequestAsync(client, ct);
                break;

            case "DespawnHostiles":
                if (client.IsAuthenticated)
                    await HandleDespawnHostilesAsync(client, ct);
                break;

            case "DespawnDroppedItems":
                if (client.IsAuthenticated)
                    await HandleDespawnDroppedItemsAsync(client, ct);
                break;

            case "SpawnItem":
                if (client.IsAuthenticated)
                    await HandleSpawnItemAsync(client, message, ct);
                break;

            case "GetItemList":
                if (client.IsAuthenticated)
                    await HandleGetItemListAsync(client, ct);
                break;

            case "AccessControlRequest":
                if (client.IsAuthenticated)
                    await HandleAccessControlRequestAsync(client, ct);
                break;

            case "AccessControlAction":
                if (client.IsAuthenticated)
                    await HandleAccessControlActionAsync(client, message, ct);
                break;

            case "WorldControlRequest":
                if (client.IsAuthenticated)
                    await HandleWorldControlRequestAsync(client, message, ct);
                break;

            case "TeleportRequest":
                if (client.IsAuthenticated)
                    await HandleTeleportRequestAsync(client, message, ct);
                break;

            default:
                if (!client.IsAuthenticated)
                {
                    _serverApi.Logger.Warning(
                        $"{LogPrefix} Unauthenticated message '{message.Type}' from {client.ClientIp} — closing.");
                    await SendMessageAsync(client.Socket, new NetworkMessage
                    {
                        Type = "AuthResponse",
                        Payload = SerializePayload(new AuthResponsePayload
                        {
                            Success = false,
                            Message = "Not authenticated. Send an Auth message first."
                        })
                    }, ct);
                    await CloseSocketAsync(client.Socket);
                }
                break;
        }
    }

    private async Task HandleAuthAsync(ShedWebClient client, NetworkMessage message, CancellationToken ct)
    {
        AuthPayload? auth;
        try
        {
            auth = message.Payload.Deserialize<AuthPayload>(JsonOptions);
        }
        catch (JsonException)
        {
            auth = null;
        }

        if (auth is not null && auth.Token == _config.SecurityToken)
        {
            client.IsAuthenticated = true;
            _serverApi.Logger.Notification($"{LogPrefix} Client {client.ClientIp} authenticated.");

            await SendMessageAsync(client.Socket, new NetworkMessage
            {
                Type = "AuthResponse",
                Payload = SerializePayload(new AuthResponsePayload
                {
                    Success = true,
                    Message = "Authenticated."
                })
            }, ct);
        }
        else
        {
            _serverApi.Logger.Warning($"{LogPrefix} Auth failed from {client.ClientIp} — bad token.");

            await SendMessageAsync(client.Socket, new NetworkMessage
            {
                Type = "AuthResponse",
                Payload = SerializePayload(new AuthResponsePayload
                {
                    Success = false,
                    Message = "Invalid security token."
                })
            }, ct);

            await CloseSocketAsync(client.Socket);
        }
    }

    private async Task HandleAccessControlRequestAsync(ShedWebClient client, CancellationToken ct)
    {
        // Try the live in-memory config first (always freshest), but the server
        // uses reflection-only types so we fall back to the JSON files on disk.
        var whitelist = ReadPlayerListFromConfig("WhitelistedPlayers")
                     ?? ReadPlayerListFile("playerswhitelisted.json");
        var banlist = ReadPlayerListFromConfig("BannedPlayers")
                   ?? ReadPlayerListFile("playersbanned.json");

        await SendMessageAsync(client.Socket, new NetworkMessage
        {
            Type = "AccessControlList",
            Payload = SerializePayload(new AccessControlListPayload
            {
                Whitelist = whitelist,
                Banlist = banlist
            })
        }, ct);
    }

    /// <summary>
    /// Grabs a player list straight from the server's live config using reflection.
    /// We snapshot it with <c>ToArray()</c> because the server thread can mutate the
    /// collection while we're reading it. Returns <c>null</c> when reflection can't
    /// find the field, which tells the caller to try the file-based fallback instead.
    /// </summary>
    private List<string>? ReadPlayerListFromConfig(string fieldName)
    {
        try
        {
            var config = _serverApi.Server.Config;
            if (config is null) return null;

            var configType = config.GetType();

            // VintagestoryLib's ServerConfig exposes these as public fields,
            // but the API only gives us the interface — hence the reflection.
            var value = configType.GetField(fieldName)?.GetValue(config)
                     ?? configType.GetProperty(fieldName)?.GetValue(config);

            if (value is not System.Collections.IEnumerable enumerable)
                return null;

            // Snapshot the collection to dodge concurrent-modification bombs.
            // One retry if we lose the race — good enough for a non-critical read.
            object[] snapshot;
            try
            {
                snapshot = enumerable.Cast<object>().ToArray();
            }
            catch (InvalidOperationException)
            {
                Thread.Sleep(50);
                snapshot = enumerable.Cast<object>().ToArray();
            }

            var names = new List<string>(snapshot.Length);
            foreach (var entry in snapshot)
            {
                var entryType = entry.GetType();
                var playerName =
                    (entryType.GetProperty("PlayerName")?.GetValue(entry)
                  ?? entryType.GetField("PlayerName")?.GetValue(entry)) as string;

                if (!string.IsNullOrEmpty(playerName))
                    names.Add(playerName);
            }

            return names;
        }
        catch (Exception ex)
        {
            _serverApi.Logger.Warning(
                $"{LogPrefix} Reflection read of {fieldName} failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Plan B — reads the whitelist/banlist from the JSON files VS keeps on disk.
    /// Slower than the in-memory route but always works.
    /// </summary>
    private List<string> ReadPlayerListFile(string fileName)
    {
        var names = new List<string>();
        var filePath = Path.Combine(_serverApi.DataBasePath, "Playerdata", fileName);

        try
        {
            if (!File.Exists(filePath))
                return names;

            var json = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return names;

            foreach (var entry in doc.RootElement.EnumerateArray())
            {
                string? name = null;

                if (entry.TryGetProperty("PlayerName", out var nameProp))
                    name = nameProp.GetString();

                if (!string.IsNullOrEmpty(name))
                    names.Add(name);
            }
        }
        catch (Exception ex)
        {
            _serverApi.Logger.Warning($"{LogPrefix} Failed to read {fileName}: {ex.Message}");
        }

        return names;
    }

    private async Task HandleAccessControlActionAsync(ShedWebClient client, NetworkMessage message, CancellationToken ct)
    {
        AccessControlActionPayload? payload;
        try
        {
            payload = message.Payload.Deserialize<AccessControlActionPayload>(JsonOptions);
        }
        catch (JsonException)
        {
            payload = null;
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.ActionType)
                           || string.IsNullOrWhiteSpace(payload.PlayerName))
            return;

        var command = payload.ActionType switch
        {
            "AddWhitelist" => $"/whitelist add {payload.PlayerName}",
            "RemoveWhitelist" => $"/whitelist remove {payload.PlayerName}",
            "AddBan" => $"/ban {payload.PlayerName}",
            "RemoveBan" => $"/unban {payload.PlayerName}",
            _ => null
        };

        if (command is null) return;

        _serverApi.Logger.Notification(
            $"{LogPrefix} AccessControl '{payload.ActionType}' for '{payload.PlayerName}' by {client.ClientIp}");

        _serverApi.InjectConsole(command);

        await BroadcastConsoleLogAsync(
            $"[Dashboard] {payload.ActionType}: {payload.PlayerName}");

        // Give the server a second to flush the change to disk before we re-read
        await Task.Delay(1000, ct);
        await HandleAccessControlRequestAsync(client, ct);
    }

    private async Task HandleWorldControlRequestAsync(ShedWebClient client, NetworkMessage message, CancellationToken ct)
    {
        WorldControlRequestPayload? payload;
        try
        {
            payload = message.Payload.Deserialize<WorldControlRequestPayload>(JsonOptions);
        }
        catch (JsonException)
        {
            payload = null;
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.Action))
            return;

        string? command = payload.Action switch
        {
            "SetTime" when payload.Value.HasValue => FormatTimeSetCommand(payload.Value.Value),
            "ForceStorm" => "/nexttempstorm now",
            _ => null
        };

        // StopStorm has no console command — we have to poke the mod system directly
        if (payload.Action == "StopStorm")
        {
            _serverApi.Logger.Notification(
                $"{LogPrefix} WorldControl 'StopStorm' by {client.ClientIp}");

            StopTemporalStorm();

            // Push the updated state right away so the dashboard toggle flips instantly
            var refreshedStatus = new NetworkMessage
            {
                Type = "WorldStatusUpdate",
                Payload = SerializePayload(BuildWorldStatusPayload())
            };
            await BroadcastToAuthenticatedAsync(refreshedStatus, ct);

            await BroadcastConsoleLogAsync("[Dashboard] World control: StopStorm");
            return;
        }

        if (command is null) return;

        _serverApi.Logger.Notification(
            $"{LogPrefix} WorldControl '{payload.Action}' (Value={payload.Value}) by {client.ClientIp}");

        _serverApi.InjectConsole(command);

        await BroadcastConsoleLogAsync(
            $"[Dashboard] World control: {payload.Action}" +
            (payload.Value.HasValue ? $" → {payload.Value.Value:F1}" : ""));
    }

    /// <summary>
    /// The dashboard slider sends decimal hours (e.g. 6.5 = 6:30 AM).
    /// VS expects <c>/time set H:MM</c>, so we convert here.
    /// </summary>
    private static string FormatTimeSetCommand(double decimalHour)
    {
        var h = (int)decimalHour;
        var m = (int)Math.Round((decimalHour - h) * 60);
        return $"/time set {h}:{m:D2}";
    }

    /// <summary>
    /// Kills an active temporal storm by zeroing out the runtime storm data.
    /// There's no built-in command for this, so we reach into
    /// <see cref="SystemTemporalStability"/> and flip the flags ourselves.
    /// </summary>
    private void StopTemporalStorm()
    {
        var tempStability = _serverApi.ModLoader.GetModSystem<SystemTemporalStability>();
        if (tempStability is null)
        {
            _serverApi.Logger.Warning($"{LogPrefix} SystemTemporalStability not found — cannot stop storm.");
            return;
        }

        var stormData = tempStability.StormData;
        if (stormData is null || !stormData.nowStormActive)
        {
            _serverApi.Logger.Notification($"{LogPrefix} No active temporal storm to stop.");
            return;
        }

        stormData.nowStormActive = false;
        stormData.stormActiveTotalDays = 0;

        _serverApi.BroadcastMessageToAllGroups(
            "The temporal storm seems to be waning", EnumChatType.AllGroups);

        _serverApi.Logger.Notification($"{LogPrefix} Temporal storm stopped via dashboard.");
    }

    /// <summary>
    /// Dashboard said "nuke hostiles" — wipe every loaded hostile mob from the world.
    /// </summary>
    private async Task HandleDespawnHostilesAsync(ShedWebClient client, CancellationToken ct)
    {
        _serverApi.Logger.Notification(
            $"{LogPrefix} DespawnHostiles requested by {client.ClientIp}");

        var count = DespawnHostileEntities();

        await BroadcastConsoleLogAsync(
            $"[Dashboard] Despawned {count} hostile entities");
    }

    /// <summary>
    /// Walks every loaded entity, matches known hostile prefixes (drifters, wolves,
    /// locusts, etc.), and force-kills them. Returns the body count.
    /// </summary>
    private int DespawnHostileEntities()
    {
        // Snapshot to avoid modifying the collection while iterating
        var entities = _serverApi.World.LoadedEntities.Values.ToList();
        var count = 0;

        foreach (var entity in entities)
        {
            var codePath = entity.Code?.Path ?? "";

            if (codePath.Contains("drifter") || codePath.Contains("wolf") ||
                codePath.Contains("hyena")   || codePath.Contains("bear") ||
                codePath.Contains("locust")  || codePath.Contains("bell"))
            {
                try
                {
                    entity.DespawnReason = new EntityDespawnData
                    {
                        Reason = EnumDespawnReason.Death
                    };
                    entity.Die();
                    count++;
                }
                catch (Exception ex)
                {
                    _serverApi.Logger.Warning(
                        $"{LogPrefix} Failed to despawn entity {codePath}: {ex.Message}");
                }
            }
        }

        _serverApi.Logger.Notification(
            $"{LogPrefix} Despawned {count} hostile entities out of {entities.Count} loaded.");

        return count;
    }

    /// <summary>
    /// Dashboard said "nuke items" — clean up every dropped item entity on the ground.
    /// </summary>
    private async Task HandleDespawnDroppedItemsAsync(ShedWebClient client, CancellationToken ct)
    {
        _serverApi.Logger.Notification(
            $"{LogPrefix} DespawnDroppedItems requested by {client.ClientIp}");

        var count = DespawnDroppedItemEntities();

        await BroadcastConsoleLogAsync(
            $"[Dashboard] Despawned {count} dropped item entities");
    }

    /// <summary>
    /// Finds every <c>EntityItem</c> in loaded chunks and expires it.
    /// These are the items players drop or mobs leave behind.
    /// </summary>
    private int DespawnDroppedItemEntities()
    {
        var entities = _serverApi.World.LoadedEntities.Values.ToList();
        var count = 0;

        foreach (var entity in entities)
        {
            var className = entity.Properties?.Class;
            if (className != "EntityItem") continue;

            try
            {
                entity.DespawnReason = new EntityDespawnData
                {
                    Reason = EnumDespawnReason.Expire
                };
                entity.Die();
                count++;
            }
            catch (Exception ex)
            {
                _serverApi.Logger.Warning(
                    $"{LogPrefix} Failed to despawn dropped item: {ex.Message}");
            }
        }

        _serverApi.Logger.Notification(
            $"{LogPrefix} Despawned {count} dropped items out of {entities.Count} loaded.");

        return count;
    }

    /// <summary>
    /// Scrapes the current world state — time of day, season, storm activity —
    /// into a payload the dashboard can display. Pulls storm data from both the
    /// live mod system and the save file so we get both "active now" and "next ETA".
    /// </summary>
    private WorldStatusPayload BuildWorldStatusPayload()
    {
        var payload = new WorldStatusPayload();
        try
        {
            var calendar = _serverApi.World.Calendar;
            if (calendar != null)
            {
                payload.HourOfDay = Math.Round(calendar.HourOfDay, 2);
                payload.Season = calendar.GetSeason(_serverApi.World.DefaultSpawnPosition?.AsBlockPos)
                                         .ToString();
            }

            try
            {
                var tempStability = _serverApi.ModLoader.GetModSystem<SystemTemporalStability>();
                if (tempStability?.StormData != null)
                    payload.IsStormActive = tempStability.StormData.nowStormActive;
            }
            catch (Exception)
            {
            }

            // The "next storm" timestamp lives in save game data, not the mod system
            try
            {
                var bytes = _serverApi.WorldManager.SaveGame.GetData("temporalStormData");
                if (bytes != null)
                {
                    var json = Encoding.UTF8.GetString(bytes);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("nextStormTotalDays", out var nsd))
                    {
                        var totalDaysNow = _serverApi.World.Calendar?.TotalDays ?? 0;
                        payload.DaysUntilStorm = Math.Max(0, Math.Round(nsd.GetDouble() - totalDaysNow, 2));
                    }
                }
            }
            catch (Exception)
            {
            }
        }
        catch (Exception)
        {
        }

        return payload;
    }

    private async Task HandleTeleportRequestAsync(ShedWebClient client, NetworkMessage message, CancellationToken ct)
    {
        TeleportRequestPayload? payload;
        try
        {
            payload = message.Payload.Deserialize<TeleportRequestPayload>(JsonOptions);
        }
        catch (JsonException)
        {
            payload = null;
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.PlayerName))
            return;

        string logDetail;

        if (!string.IsNullOrWhiteSpace(payload.DestinationPlayer))
        {
            // Player-to-player: /tp handles this fine since there's no coord ambiguity
            _serverApi.InjectConsole($"/tp {payload.PlayerName} {payload.DestinationPlayer}");
            logDetail = $"{payload.PlayerName} → {payload.DestinationPlayer}";
        }
        else
        {
            // Coordinate teleport: the dashboard shows spawn-relative coords, but the
            // Entity API needs absolute engine coords. Add the spawn offset back in.
            var player = Array.Find(
                _serverApi.World.AllOnlinePlayers,
                p => p.PlayerName.Equals(payload.PlayerName, StringComparison.OrdinalIgnoreCase));

            if (player?.Entity == null)
            {
                _serverApi.Logger.Warning(
                    $"{LogPrefix} Teleport failed — player '{payload.PlayerName}' not found.");
                await BroadcastConsoleLogAsync(
                    $"[Dashboard] Teleport failed — player '{payload.PlayerName}' not found.");
                return;
            }

            var spawn = _serverApi.World.DefaultSpawnPosition?.XYZInt;
            double absX = payload.X + (spawn?.X ?? 0);
            double absY = payload.Y;
            double absZ = payload.Z + (spawn?.Z ?? 0);

            _serverApi.Logger.Notification(
                $"{LogPrefix} Teleport coords: input=({payload.X}, {payload.Y}, {payload.Z}) " +
                $"spawnOffset=({spawn?.X}, {spawn?.Z}) absolute=({absX}, {absY}, {absZ})");

            player.Entity.TeleportTo(new EntityPos(absX, absY, absZ));
            logDetail = $"{payload.PlayerName} → ({payload.X:F1}, {payload.Y:F1}, {payload.Z:F1})";
        }

        _serverApi.Logger.Notification(
            $"{LogPrefix} Teleport {logDetail} by {client.ClientIp}");

        await BroadcastConsoleLogAsync($"[Dashboard] Teleported {logDetail}");
    }

    private async Task HandleSpawnItemAsync(ShedWebClient client, NetworkMessage message, CancellationToken ct)
    {
        SpawnItemPayload? payload;
        try
        {
            payload = message.Payload.Deserialize<SpawnItemPayload>(JsonOptions);
        }
        catch (JsonException)
        {
            payload = null;
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.TargetPlayer)
                           || string.IsNullOrWhiteSpace(payload.ItemCode))
            return;

        var qty = Math.Max(1, payload.Quantity);
        var command = $"/giveitem {payload.ItemCode} {qty} {payload.TargetPlayer}";

        _serverApi.Logger.Notification(
            $"{LogPrefix} Spawn item '{payload.ItemCode}' x{qty} → {payload.TargetPlayer} by {client.ClientIp}");

        _serverApi.InjectConsole(command);

        await BroadcastConsoleLogAsync(
            $"[Dashboard] Spawned {payload.ItemCode} x{qty} for {payload.TargetPlayer}");
    }

    private async Task HandleGetItemListAsync(ShedWebClient client, CancellationToken ct)
    {
        var codes = new HashSet<string>(StringComparer.Ordinal);

        // Every registered item the game knows about
        foreach (var item in _serverApi.World.Items)
        {
            if (item?.Code != null && item.Code.Path.Length > 0)
                codes.Add(item.Code.ToString());
        }

        // Also include blocks that show up in creative inventory — these are the
        // placeable things players actually care about (chests, doors, machines, etc.)
        foreach (var block in _serverApi.World.Blocks)
        {
            if (block?.Code == null || block.Code.Path.Length == 0) continue;
            if (block.BlockId == 0) continue;
            if (block.CreativeInventoryTabs != null && block.CreativeInventoryTabs.Length > 0)
                codes.Add(block.Code.ToString());
        }

        var sorted = codes.OrderBy(c => c).ToArray();

        _serverApi.Logger.Notification(
            $"{LogPrefix} Sending item list ({sorted.Length} entries) to {client.ClientIp}");

        await SendMessageAsync(client.Socket, new NetworkMessage
        {
            Type = "ItemListResponse",
            Payload = SerializePayload(new ItemListResponsePayload { Items = sorted })
        }, ct);
    }

    private async Task HandleConsoleCommandAsync(ShedWebClient client, NetworkMessage message)
    {
        CommandPayload? cmd;
        try
        {
            cmd = message.Payload.Deserialize<CommandPayload>(JsonOptions);
        }
        catch (JsonException)
        {
            cmd = null;
        }

        if (cmd is null || string.IsNullOrWhiteSpace(cmd.Command))
            return;

        _serverApi.Logger.Notification(
            $"{LogPrefix} Command from {client.ClientIp}: {cmd.Command}");

        _serverApi.InjectConsole(cmd.Command);
    }

    private async Task HandlePlayerActionAsync(ShedWebClient client, NetworkMessage message)
    {
        PlayerActionPayload? action;
        try
        {
            action = message.Payload.Deserialize<PlayerActionPayload>(JsonOptions);
        }
        catch (JsonException)
        {
            action = null;
        }

        if (action is null || string.IsNullOrWhiteSpace(action.ActionType)
                          || string.IsNullOrWhiteSpace(action.TargetPlayer))
            return;

        var command = action.ActionType.ToLowerInvariant() switch
        {
            "kick" => string.IsNullOrWhiteSpace(action.Reason)
                ? $"/kick {action.TargetPlayer}"
                : $"/kick {action.TargetPlayer} {action.Reason}",
            "ban" => $"/shed shadowban {action.TargetPlayer}",
            "mute" => $"/shed mute {action.TargetPlayer}",
            "pardon" => $"/shed clear {action.TargetPlayer}",
            _ => null
        };

        if (command is null)
            return;

        _serverApi.Logger.Notification(
            $"{LogPrefix} {action.ActionType} action on '{action.TargetPlayer}' from {client.ClientIp}");

        _serverApi.InjectConsole(command);

        await BroadcastConsoleLogAsync(
            $"[Dashboard] {action.ActionType}: {action.TargetPlayer}");
    }

    private async Task HandleConfigRequestAsync(ShedWebClient client, NetworkMessage message, CancellationToken ct)
    {
        ConfigRequestPayload? payload;
        try
        {
            payload = message.Payload.Deserialize<ConfigRequestPayload>(JsonOptions);
        }
        catch (JsonException)
        {
            payload = null;
        }

        var fileName = SanitizeConfigFileName(payload?.FileName);
        if (fileName is null)
            return;

        var configPath = Path.Combine(_serverApi.DataBasePath, "ModConfig", fileName);

        string jsonContent;
        try
        {
            jsonContent = File.Exists(configPath)
                ? await File.ReadAllTextAsync(configPath, ct)
                : "{}";
        }
        catch (Exception ex)
        {
            _serverApi.Logger.Error($"{LogPrefix} Failed to read {fileName}: {ex.Message}");
            jsonContent = "{}";
        }

        _serverApi.Logger.Notification($"{LogPrefix} Config '{fileName}' requested by {client.ClientIp}");

        await SendMessageAsync(client.Socket, new NetworkMessage
        {
            Type = "ConfigResponse",
            Payload = SerializePayload(new ConfigResponsePayload { JsonContent = jsonContent })
        }, ct);
    }

    private async Task HandleConfigSaveAsync(ShedWebClient client, NetworkMessage message, CancellationToken ct)
    {
        ConfigSavePayload? payload;
        try
        {
            payload = message.Payload.Deserialize<ConfigSavePayload>(JsonOptions);
        }
        catch (JsonException)
        {
            payload = null;
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.JsonContent))
            return;

        var fileName = SanitizeConfigFileName(payload.FileName);
        if (fileName is null)
            return;

        var configPath = Path.Combine(_serverApi.DataBasePath, "ModConfig", fileName);

        try
        {
            await File.WriteAllTextAsync(configPath, payload.JsonContent, ct);
            _serverApi.Logger.Notification($"{LogPrefix} Config '{fileName}' saved by {client.ClientIp}");
        }
        catch (Exception ex)
        {
            _serverApi.Logger.Error($"{LogPrefix} Failed to write {fileName}: {ex.Message}");
            return;
        }

        // Poke Shed Security to pick up the new config without a server restart
        _serverApi.InjectConsole("/shed reload");

        await BroadcastConsoleLogAsync($"[Dashboard] Config '{fileName}' updated and reload triggered.");
    }

    private async Task HandleConfigListRequestAsync(ShedWebClient client, CancellationToken ct)
    {
        var modConfigPath = _serverApi.GetOrCreateDataPath("ModConfig");
        var files = new System.Collections.Generic.List<string>();

        try
        {
            foreach (var fullPath in Directory.GetFiles(modConfigPath, "*.json"))
                files.Add(Path.GetFileName(fullPath));
        }
        catch (Exception ex)
        {
            _serverApi.Logger.Error($"{LogPrefix} Failed to scan ModConfig directory: {ex.Message}");
        }

        _serverApi.Logger.Notification(
            $"{LogPrefix} Config list ({files.Count} files) requested by {client.ClientIp}");

        await SendMessageAsync(client.Socket, new NetworkMessage
        {
            Type = "ConfigListResponse",
            Payload = SerializePayload(new ConfigListResponsePayload { Files = files })
        }, ct);
    }

    /// <summary>
    /// Quick guard against directory-traversal shenanigans.
    /// Only allows bare <c>.json</c> filenames — no slashes, no <c>..</c>.
    /// </summary>
    private static string? SanitizeConfigFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        if (fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(".."))
            return null;

        if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return null;

        return fileName;
    }

    private async Task HandleInventoryRequestAsync(ShedWebClient client, NetworkMessage message, CancellationToken ct)
    {
        InventoryRequestPayload? request;
        try
        {
            request = message.Payload.Deserialize<InventoryRequestPayload>(JsonOptions);
        }
        catch (JsonException)
        {
            request = null;
        }

        if (request is null || string.IsNullOrWhiteSpace(request.PlayerName))
            return;

        var onlinePlayers = _serverApi.World.AllOnlinePlayers;
        var target = onlinePlayers?.FirstOrDefault(p =>
            string.Equals(p.PlayerName, request.PlayerName, StringComparison.OrdinalIgnoreCase))
            as IServerPlayer;

        var response = new InventoryResponsePayload();

        if (target?.InventoryManager != null)
        {
            foreach (var kvp in target.InventoryManager.Inventories)
            {
                var inv = kvp.Value;
                var className = inv.ClassName?.ToLowerInvariant() ?? "";

                string category;
                if (className.Contains("hotbar"))
                    category = "Hotbar";
                else if (className.Contains("backpack"))
                    category = "Backpack";
                else if (className.Contains("character"))
                    category = "Gear";
                else
                    continue;

                for (int i = 0; i < inv.Count; i++)
                {
                    var slot = inv[i];
                    if (slot?.Itemstack == null) continue;

                    var code = slot.Itemstack.Collectible?.Code?.ToString() ?? "unknown";
                    response.Slots.Add(new InventorySlot
                    {
                        ItemName = code,
                        Quantity = slot.Itemstack.StackSize,
                        Category = category
                    });
                }
            }
        }

        _serverApi.Logger.Notification(
            $"{LogPrefix} Inventory inspect '{request.PlayerName}' ({response.Slots.Count} slots) by {client.ClientIp}");

        await SendMessageAsync(client.Socket, new NetworkMessage
        {
            Type = "InventoryResponse",
            Payload = SerializePayload(response)
        }, ct);
    }

    private async Task HandlePlayerInspectAsync(ShedWebClient client, NetworkMessage message, CancellationToken ct)
    {
        PlayerInspectRequestPayload? request;
        try
        {
            request = message.Payload.Deserialize<PlayerInspectRequestPayload>(JsonOptions);
        }
        catch (JsonException)
        {
            request = null;
        }

        if (request is null || string.IsNullOrWhiteSpace(request.PlayerName))
            return;

        var onlinePlayers = _serverApi.World.AllOnlinePlayers;
        var target = onlinePlayers?.FirstOrDefault(p =>
            string.Equals(p.PlayerName, request.PlayerName, StringComparison.OrdinalIgnoreCase))
            as IServerPlayer;

        if (target is null)
        {
            await SendMessageAsync(client.Socket, new NetworkMessage
            {
                Type = "PlayerInspectResponse",
                Payload = SerializePayload(new PlayerInspectResponsePayload
                {
                    PlayerName = request.PlayerName,
                    IpAddress = "Offline",
                    Playtime = "--",
                    Position = "--",
                    TotalStrikes = 0
                })
            }, ct);
            return;
        }

        // Mask the last two octets — enough info for region identification without doxing anyone
        var rawIp = target.IpAddress ?? "Unknown";
        var maskedIp = rawIp;
        var octets = rawIp.Split('.');
        if (octets.Length == 4)
            maskedIp = $"{octets[0]}.{octets[1]}.*.* ";

        // Convert absolute engine coords to spawn-relative (what players actually see in-game)
        var pos = target.Entity?.Pos;
        string position;
        if (pos is not null)
        {
            var spawn = _serverApi.World.DefaultSpawnPosition?.XYZInt;
            position = $"X: {pos.X - (spawn?.X ?? 0):F1}  Y: {pos.Y:F1}  Z: {pos.Z - (spawn?.Z ?? 0):F1}";
        }
        else
        {
            position = "Unknown";
        }

        // Session time since their last login — not lifetime playtime
        var playtime = "--";
        var lastJoinStr = target.ServerData?.LastJoinDate;
        if (!string.IsNullOrEmpty(lastJoinStr) && DateTime.TryParse(lastJoinStr, out var lastJoin))
        {
            var sessionSpan = DateTime.UtcNow - lastJoin;
            if (sessionSpan.TotalSeconds > 0)
            {
                playtime = sessionSpan.TotalHours >= 1
                    ? $"{(int)sessionSpan.TotalHours}h {sessionSpan.Minutes}m"
                    : $"{sessionSpan.Minutes}m {sessionSpan.Seconds}s";
            }
        }

        // Pull their strike count from Shed Security's data file, if it exists
        var strikePath = Path.Combine(_serverApi.DataBasePath, "ModData", "shed-strikes.json");
        var totalStrikes = 0;
        try
        {
            if (File.Exists(strikePath))
            {
                var json = await File.ReadAllTextAsync(strikePath, ct);
                using var doc = JsonDocument.Parse(json);
                foreach (var entry in doc.RootElement.EnumerateObject())
                {
                    if (entry.Value.TryGetProperty("Username", out var userProp) &&
                        string.Equals(userProp.GetString(), target.PlayerName, StringComparison.OrdinalIgnoreCase) &&
                        entry.Value.TryGetProperty("Count", out var countProp))
                    {
                        totalStrikes = countProp.GetInt32();
                        break;
                    }
                }
            }
        }
        catch (Exception)
        {
        }

        _serverApi.Logger.Notification(
            $"{LogPrefix} Player inspect '{target.PlayerName}' by {client.ClientIp}");

        await SendMessageAsync(client.Socket, new NetworkMessage
        {
            Type = "PlayerInspectResponse",
            Payload = SerializePayload(new PlayerInspectResponsePayload
            {
                PlayerName = target.PlayerName,
                IpAddress = maskedIp,
                Playtime = playtime,
                Position = position,
                TotalStrikes = totalStrikes
            })
        }, ct);
    }

    private async Task HandleModListRequestAsync(ShedWebClient client, CancellationToken ct)
    {
        var modsPath = Path.Combine(_serverApi.DataBasePath, "Mods");
        var modList = new ModListResponsePayload();

        try
        {
            if (Directory.Exists(modsPath))
            {
                var files = Directory.GetFiles(modsPath, "*.zip")
                    .Concat(Directory.GetFiles(modsPath, "*.disabled"));

                foreach (var fullPath in files)
                {
                    var fi = new FileInfo(fullPath);
                    var sizeKb = fi.Length / 1024.0;
                    var size = sizeKb >= 1024
                        ? $"{sizeKb / 1024.0:F1} MB"
                        : $"{sizeKb:F0} KB";

                    modList.Mods.Add(new Models.ModInfo
                    {
                        Name = fi.Name,
                        Size = size,
                        IsEnabled = fi.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase)
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _serverApi.Logger.Error($"{LogPrefix} Failed to scan Mods directory: {ex.Message}");
        }

        _serverApi.Logger.Notification(
            $"{LogPrefix} Mod list ({modList.Mods.Count} mods) requested by {client.ClientIp}");

        await SendMessageAsync(client.Socket, new NetworkMessage
        {
            Type = "ModListResponse",
            Payload = SerializePayload(modList)
        }, ct);
    }

    private async Task HandleModToggleRequestAsync(ShedWebClient client, NetworkMessage message, CancellationToken ct)
    {
        ModToggleRequestPayload? payload;
        try
        {
            payload = message.Payload.Deserialize<ModToggleRequestPayload>(JsonOptions);
        }
        catch (JsonException)
        {
            payload = null;
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.FileName))
            return;

        if (payload.FileName.Contains('/') || payload.FileName.Contains('\\') || payload.FileName.Contains(".."))
            return;

        var modsPath = Path.Combine(_serverApi.DataBasePath, "Mods");
        var sourcePath = Path.Combine(modsPath, payload.FileName);

        if (!File.Exists(sourcePath))
        {
            _serverApi.Logger.Warning($"{LogPrefix} Mod file not found: {payload.FileName}");
            return;
        }

        string targetPath;
        if (payload.Enable)
        {
            // .disabled → .zip (VS only loads .zip files)
            if (!payload.FileName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                return;
            targetPath = Path.Combine(modsPath, payload.FileName[..^".disabled".Length] + ".zip");
        }
        else
        {
            // .zip → .disabled (hides it from VS without deleting anything)
            if (!payload.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return;
            targetPath = Path.Combine(modsPath, payload.FileName[..^".zip".Length] + ".disabled");
        }

        try
        {
            File.Move(sourcePath, targetPath);
            var action = payload.Enable ? "Enabled" : "Disabled";
            _serverApi.Logger.Notification(
                $"{LogPrefix} Mod {action}: {payload.FileName} by {client.ClientIp}");

            await BroadcastConsoleLogAsync(
                $"[Dashboard] Mod {action}: {Path.GetFileName(targetPath)}");

            await HandleModListRequestAsync(client, ct);
        }
        catch (Exception ex)
        {
            _serverApi.Logger.Error($"{LogPrefix} Failed to toggle mod {payload.FileName}: {ex.Message}");
        }
    }

    private async Task HandleFileUploadChunkAsync(ShedWebClient client, NetworkMessage message, CancellationToken ct)
    {
        FileUploadChunkPayload? payload;
        try
        {
            payload = message.Payload.Deserialize<FileUploadChunkPayload>(JsonOptions);
        }
        catch (JsonException)
        {
            payload = null;
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.FileName))
            return;

        if (payload.FileName.Contains('/') || payload.FileName.Contains('\\') || payload.FileName.Contains(".."))
            return;

        if (!payload.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return;

        var modsPath = Path.Combine(_serverApi.DataBasePath, "Mods");
        Directory.CreateDirectory(modsPath);

        var partPath = Path.Combine(modsPath, payload.FileName + ".part");

        try
        {
            var chunkBytes = Convert.FromBase64String(payload.Base64Data);

            using (var fs = new FileStream(partPath, payload.ChunkIndex == 0 ? FileMode.Create : FileMode.Append,
                       FileAccess.Write, FileShare.None))
            {
                await fs.WriteAsync(chunkBytes, ct);
            }

            _serverApi.Logger.Notification(
                $"{LogPrefix} Upload chunk {payload.ChunkIndex + 1}/{payload.TotalChunks} for '{payload.FileName}' from {client.ClientIp}");

            // Last chunk arrived — finalize the file
            if (payload.ChunkIndex == payload.TotalChunks - 1)
            {
                var finalPath = Path.Combine(modsPath, payload.FileName);

                if (File.Exists(finalPath))
                    File.Delete(finalPath);

                File.Move(partPath, finalPath);

                _serverApi.Logger.Notification(
                    $"{LogPrefix} Mod uploaded: {payload.FileName} by {client.ClientIp}");

                await BroadcastConsoleLogAsync(
                    $"[Dashboard] Mod uploaded: {payload.FileName}");

                await HandleModListRequestAsync(client, ct);
            }
        }
        catch (Exception ex)
        {
            _serverApi.Logger.Error($"{LogPrefix} Failed to write upload chunk for {payload.FileName}: {ex.Message}");

            // Don't leave half-written files lying around
            try { if (File.Exists(partPath)) File.Delete(partPath); } catch { }
        }
    }

    private async Task HandleBackupListRequestAsync(ShedWebClient client, CancellationToken ct)
    {
        var backupsPath = Path.Combine(_serverApi.DataBasePath, "Backups");
        Directory.CreateDirectory(backupsPath);

        var backupList = new BackupListResponsePayload();

        try
        {
            foreach (var fullPath in Directory.GetFiles(backupsPath, "*.zip")
                         .OrderByDescending(f => new FileInfo(f).CreationTimeUtc))
            {
                var fi = new FileInfo(fullPath);
                var sizeMb = fi.Length / (1024.0 * 1024.0);
                var size = sizeMb >= 1024
                    ? $"{sizeMb / 1024.0:F1} GB"
                    : $"{sizeMb:F1} MB";

                backupList.Backups.Add(new BackupInfo
                {
                    Name = fi.Name,
                    Size = size,
                    Date = fi.CreationTimeUtc.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
        }
        catch (Exception ex)
        {
            _serverApi.Logger.Error($"{LogPrefix} Failed to scan Backups directory: {ex.Message}");
        }

        _serverApi.Logger.Notification(
            $"{LogPrefix} Backup list ({backupList.Backups.Count} backups) requested by {client.ClientIp}");

        await SendMessageAsync(client.Socket, new NetworkMessage
        {
            Type = "BackupListResponse",
            Payload = SerializePayload(backupList)
        }, ct);
    }

    private async Task HandleBackupCreateRequestAsync(ShedWebClient client, CancellationToken ct)
    {
        var savesPath = Path.Combine(_serverApi.DataBasePath, "Saves");
        var backupsPath = Path.Combine(_serverApi.DataBasePath, "Backups");
        Directory.CreateDirectory(backupsPath);

        if (!Directory.Exists(savesPath))
        {
            _serverApi.Logger.Warning($"{LogPrefix} Saves directory not found at {savesPath}");
            await BroadcastConsoleLogAsync("[Dashboard] Backup failed: Saves directory not found.");
            // Still send the list so the client knows to stop spinning
            await HandleBackupListRequestAsync(client, ct);
            return;
        }

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backupFileName = $"Backup_{timestamp}.zip";
        var backupFilePath = Path.Combine(backupsPath, backupFileName);

        _serverApi.Logger.Notification(
            $"{LogPrefix} Backup creation started by {client.ClientIp}: {backupFileName}");
        await BroadcastConsoleLogAsync($"[Dashboard] Creating backup: {backupFileName}...");

        // Zip on a background thread so we don't stall the game. FileShare.ReadWrite
        // lets us read the active save DB even while the server has it locked.
        var success = await Task.Run(() =>
        {
            try
            {
                using (var zip = ZipFile.Open(backupFilePath, ZipArchiveMode.Create))
                {
                    foreach (var file in Directory.GetFiles(savesPath, "*", SearchOption.AllDirectories))
                    {
                        var entryName = file.Substring(savesPath.Length + 1).Replace('\\', '/');
                        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);

                        using (var entryStream = entry.Open())
                        using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            fileStream.CopyTo(entryStream);
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                _serverApi.Logger.Error($"{LogPrefix} Backup creation failed: {ex.Message}");
                try { if (File.Exists(backupFilePath)) File.Delete(backupFilePath); } catch { }
                return false;
            }
        }, ct);

        if (success)
        {
            _serverApi.Logger.Notification(
                $"{LogPrefix} Backup created: {backupFileName} by {client.ClientIp}");
            await BroadcastConsoleLogAsync($"[Dashboard] Backup created: {backupFileName}");
        }
        else
        {
            await BroadcastConsoleLogAsync("[Dashboard] Backup failed — see server log for details.");
        }

        await HandleBackupListRequestAsync(client, ct);
    }

    private async Task HandleFileDownloadRequestAsync(ShedWebClient client, NetworkMessage message, CancellationToken ct)
    {
        var payload = message.Payload.Deserialize<FileDownloadRequestPayload>(JsonOptions);
        if (payload is null || string.IsNullOrWhiteSpace(payload.FileName)) return;

        // Strip any path trickery — we only serve files from the Backups folder
        var safeName = Path.GetFileName(payload.FileName);
        var backupsPath = Path.Combine(_serverApi.DataBasePath, "Backups");
        var filePath = Path.Combine(backupsPath, safeName);

        if (!File.Exists(filePath))
        {
            _serverApi.Logger.Warning($"{LogPrefix} Download requested for non-existent backup: {safeName}");
            return;
        }

        _serverApi.Logger.Notification(
            $"{LogPrefix} File download started by {client.ClientIp}: {safeName}");

        const int chunkSize = 1024 * 1024; // 1 MB
        var fileBytes = await Task.Run(() => File.ReadAllBytes(filePath), ct);
        var totalChunks = (int)Math.Ceiling((double)fileBytes.Length / chunkSize);

        for (int i = 0; i < totalChunks; i++)
        {
            var offset = i * chunkSize;
            var length = Math.Min(chunkSize, fileBytes.Length - offset);
            var base64 = Convert.ToBase64String(fileBytes, offset, length);

            await SendMessageAsync(client.Socket, new NetworkMessage
            {
                Type = "FileDownloadChunk",
                Payload = SerializePayload(new FileDownloadChunkPayload
                {
                    FileName = safeName,
                    ChunkIndex = i,
                    TotalChunks = totalChunks,
                    Base64Data = base64
                })
            }, ct);
        }

        _serverApi.Logger.Notification(
            $"{LogPrefix} File download complete: {safeName} ({totalChunks} chunks) to {client.ClientIp}");
    }

    private async Task HandleDiagnosticsRequestAsync(ShedWebClient client, CancellationToken ct)
    {
        int players = 0, droppedItems = 0, hostileMobs = 0, creatures = 0, other = 0;

        foreach (var entity in _serverApi.World.LoadedEntities.Values)
        {
            var className = entity.Properties?.Class;

            if (className == "EntityPlayer")
            {
                players++;
                continue;
            }

            if (className == "EntityItem")
            {
                droppedItems++;
                continue;
            }

            var codePath = entity.Code?.Path ?? "";
            if (codePath.StartsWith("drifter") || codePath.StartsWith("wolf") ||
                codePath.StartsWith("hyena") || codePath.StartsWith("bear") ||
                codePath.StartsWith("locust") || codePath.StartsWith("bell"))
            {
                hostileMobs++;
            }
            else if (entity is EntityAgent)
            {
                creatures++;
            }
            else
            {
                other++;
            }
        }

        var loadedEntities = _serverApi.World.LoadedEntities.Count;

        // Assemble the pie chart data — only include categories that actually have entities
        var entityDistribution = new Dictionary<string, double>();
        if (players > 0) entityDistribution["Players"] = players;
        if (hostileMobs > 0) entityDistribution["Hostile Mobs"] = hostileMobs;
        if (creatures > 0) entityDistribution["Creatures"] = creatures;
        if (droppedItems > 0) entityDistribution["Dropped Items"] = droppedItems;
        if (other > 0) entityDistribution["Other Entities"] = other;

        if (entityDistribution.Count == 0)
            entityDistribution["No Entities"] = 1;

        _serverApi.Logger.Notification(
            $"{LogPrefix} Diagnostics requested by {client.ClientIp}: {droppedItems} items, {hostileMobs} hostiles, {loadedEntities} entities");

        await SendMessageAsync(client.Socket, new NetworkMessage
        {
            Type = "DiagnosticsResponse",
            Payload = SerializePayload(new DiagnosticsDataPayload
            {
                LoadedChunks = loadedEntities,
                DroppedItems = droppedItems,
                HostileMobs = hostileMobs,
                TickProfile = entityDistribution
            })
        }, ct);
    }

    private void OnLogEntryAdded(EnumLogType logType, string message, params object[] args)
    {
        // Don't echo our own log lines back to dashboards — infinite loop territory
        if (message.Contains(LogPrefix))
            return;

        // The server parrots back every command we inject ("Handling Console Command /tp ...").
        // The dashboard already shows a friendly message, so suppress the ugly raw echo.
        if (message.StartsWith("Handling Console Command"))
            return;

        var formatted = args is { Length: > 0 }
            ? string.Format(message, args)
            : message;

        // Sniff for Shed Security alert lines and promote them to real-time SecurityAlert messages
        if (formatted.Contains(ShedSecurityPrefix))
        {
            _ = TryBroadcastSecurityAlertAsync(formatted);
        }

        var logLine = $"[{logType}] {formatted}";
        _ = BroadcastConsoleLogAsync(logLine);
    }

    // ───────────────────── Shed Security Integration ─────────────────────

    /// <summary>
    /// Picks apart Shed Security log lines and turns them into structured alerts
    /// for the dashboard's anti-cheat feed.
    /// Format we're looking for: "[Shed Security] SPEED_HACK | PlayerName | Details | Strikes: 3"
    /// If the format is weird, we just send the raw text — better than dropping it.
    /// </summary>
    private async Task TryBroadcastSecurityAlertAsync(string logLine)
    {
        if (_cts is null || _cts.IsCancellationRequested)
            return;

        var playerName = "Unknown";
        var violationType = "Alert";
        var details = logLine;
        var strikeCount = 0;

        // Strip the prefix to get the payload text
        var prefixIndex = logLine.IndexOf(ShedSecurityPrefix, StringComparison.Ordinal);
        if (prefixIndex >= 0)
        {
            var payload = logLine[(prefixIndex + ShedSecurityPrefix.Length)..].Trim();

            // Audit/system logs don't have the pipe-delimited player format
            if (payload.Contains("[AUDIT]", StringComparison.OrdinalIgnoreCase)
                || !payload.Contains('|'))
            {
                playerName = "System";
                violationType = "Audit";
                strikeCount = 0;
                details = payload
                    .Replace("[AUDIT]", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Trim();
            }
            else
            {
                var parts = payload.Split('|', StringSplitOptions.TrimEntries);

                if (parts.Length >= 2)
                {
                    violationType = parts[0];
                    playerName = parts[1];
                    details = parts.Length >= 3 ? parts[2] : violationType;

                    if (parts.Length >= 4 && parts[3].StartsWith("Strikes:", StringComparison.OrdinalIgnoreCase))
                    {
                        var strikePart = parts[3]["Strikes:".Length..].Trim();
                        int.TryParse(strikePart, out strikeCount);
                    }
                }
            }
        }

        var timestamp = DateTime.UtcNow.ToString("HH:mm:ss");

        var msg = new NetworkMessage
        {
            Type = "SecurityAlert",
            Payload = SerializePayload(new SecurityAlertPayload
            {
                Timestamp = timestamp,
                PlayerName = playerName,
                AlertType = violationType,
                Details = details,
                StrikeCount = strikeCount
            })
        };

        await BroadcastToAuthenticatedAsync(msg, _cts.Token);
    }

    private async Task BroadcastConsoleLogAsync(string text)
    {
        if (_cts is null || _cts.IsCancellationRequested)
            return;

        var msg = new NetworkMessage
        {
            Type = "ConsoleLog",
            Payload = SerializePayload(new ConsoleLogPayload { Message = text })
        };

        await BroadcastToAuthenticatedAsync(msg, _cts.Token);
    }

    // ───────────────────── WebSocket Helpers ─────────────────────

    /// <summary>
    /// Reads a complete WebSocket message (may arrive in multiple frames),
    /// then deserializes it. Returns <c>null</c> on close or empty read.
    /// </summary>
    private async Task<NetworkMessage?> ReadMessageAsync(
        WebSocket socket, byte[] buffer, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;

        do
        {
            result = await socket.ReceiveAsync(
                new ArraySegment<byte>(buffer), ct);

            if (result.MessageType == WebSocketMessageType.Close)
                return null;

            ms.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        if (ms.Length == 0)
            return null;

        ms.Position = 0;
        return await JsonSerializer.DeserializeAsync<NetworkMessage>(ms, JsonOptions, ct);
    }

    /// <summary>
    /// Fires a message down the wire as a single UTF-8 text frame.
    /// Silently skips if the socket is already closed.
    /// </summary>
    private static async Task SendMessageAsync(
        WebSocket socket, NetworkMessage message, CancellationToken ct)
    {
        if (socket.State != WebSocketState.Open)
            return;

        var json = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        await socket.SendAsync(
            new ArraySegment<byte>(json),
            WebSocketMessageType.Text,
            endOfMessage: true,
            ct);
    }

    /// <summary>
    /// Converts a typed object into a <see cref="JsonElement"/> so it can ride
    /// inside a <see cref="NetworkMessage.Payload"/>.
    /// </summary>
    private static JsonElement SerializePayload<T>(T payload)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        using var doc = JsonDocument.Parse(bytes);
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// Tries a polite WebSocket close handshake with a 3-second timeout.
    /// If the socket is already dead, we just move on.
    /// </summary>
    private static async Task CloseSocketAsync(WebSocket socket)
    {
        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, "Closing", timeout.Token);
            }
        }
        catch (Exception)
        {
        }
    }

    // ───────────────────── Telemetry Broadcaster ─────────────────────

    private async Task TelemetryLoopAsync(CancellationToken ct)
    {
        // Give the server a few seconds to finish booting before we start scraping state
        await Task.Delay(TimeSpan.FromSeconds(3), ct).ConfigureAwait(false);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // ── Metrics ──
                var server = _serverApi.Server;
                var uptimeSpan = TimeSpan.FromSeconds(server.ServerUptimeSeconds);
                var uptime = uptimeSpan.TotalHours >= 1
                    ? $"{(int)uptimeSpan.TotalHours}h {uptimeSpan.Minutes}m"
                    : $"{uptimeSpan.Minutes}m {uptimeSpan.Seconds}s";

                var ramBytes = GC.GetTotalMemory(false);
                var ramMb = ramBytes / (1024.0 * 1024.0);
                var ram = ramMb >= 1024
                    ? $"{ramMb / 1024.0:F1} GB"
                    : $"{ramMb:F0} MB";

                var metricsPayload = new MetricsPayload
                {
                    Tps = $"{1000.0 / server.Config.TickTime:F1}",
                    RamUsage = ram,
                    Uptime = uptime
                };

                var metricsMessage = new NetworkMessage
                {
                    Type = "MetricsUpdate",
                    Payload = SerializePayload(metricsPayload)
                };

                // ── Players ──
                var onlinePlayers = _serverApi.World.AllOnlinePlayers;
                var playerList = new PlayerListPayload();

                if (onlinePlayers is not null)
                {
                    foreach (var p in onlinePlayers)
                    {
                        var sp = p as IServerPlayer;
                        var info = new PlayerInfoPayload
                        {
                            Name = p.PlayerName,
                            Ping = (int)(sp?.Ping ?? 0)
                        };

                        if (p.Entity?.Pos != null)
                        {
                            var spawn = _serverApi.World.DefaultSpawnPosition?.XYZInt;
                            info.X = Math.Round(p.Entity.Pos.X - (spawn?.X ?? 0), 1);
                            info.Z = Math.Round(p.Entity.Pos.Z - (spawn?.Z ?? 0), 1);
                        }

                        if (p.Entity?.WatchedAttributes != null)
                        {
                            var healthTree = p.Entity.WatchedAttributes.GetTreeAttribute("health");
                            if (healthTree != null)
                            {
                                info.Health = healthTree.GetFloat("currenthealth", 0f);
                                info.MaxHealth = healthTree.GetFloat("maxhealth", 0f);
                            }

                            info.Satiety = p.Entity.WatchedAttributes.GetFloat("satiety", 0f);
                        }

                        playerList.Players.Add(info);
                    }
                }

                var playerMessage = new NetworkMessage
                {
                    Type = "PlayerListUpdate",
                    Payload = SerializePayload(playerList)
                };

                // ── Push to dashboards ──
                await BroadcastToAuthenticatedAsync(metricsMessage, ct);
                await BroadcastToAuthenticatedAsync(playerMessage, ct);

                // ── World status ──
                var worldStatus = BuildWorldStatusPayload();

                var worldMessage = new NetworkMessage
                {
                    Type = "WorldStatusUpdate",
                    Payload = SerializePayload(worldStatus)
                };
                await BroadcastToAuthenticatedAsync(worldMessage, ct);

                // ── Shadowbans (from Shed Security's data file) ──
                var shadowbanPayload = new ShadowbanListPayload();
                var shadowbanPath = Path.Combine(_serverApi.DataBasePath, "ModData", "shed-shadowbans.json");
                try
                {
                    if (File.Exists(shadowbanPath))
                    {
                        var sbJson = await File.ReadAllTextAsync(shadowbanPath, ct);
                        using var sbDoc = JsonDocument.Parse(sbJson);

                        if (sbDoc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in sbDoc.RootElement.EnumerateArray())
                            {
                                var name = item.GetString();
                                if (!string.IsNullOrEmpty(name))
                                    shadowbanPayload.Players.Add(name);
                            }
                        }
                        else if (sbDoc.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in sbDoc.RootElement.EnumerateObject())
                            {
                                if (prop.Value.TryGetProperty("Username", out var userProp))
                                {
                                    var name = userProp.GetString();
                                    if (!string.IsNullOrEmpty(name))
                                        shadowbanPayload.Players.Add(name);
                                }
                                else
                                {
                                    // Some versions use the player name as the JSON key itself
                                    if (!string.IsNullOrEmpty(prop.Name))
                                        shadowbanPayload.Players.Add(prop.Name);
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                }

                var shadowbanMessage = new NetworkMessage
                {
                    Type = "ShadowbanListUpdate",
                    Payload = SerializePayload(shadowbanPayload)
                };
                await BroadcastToAuthenticatedAsync(shadowbanMessage, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _serverApi.Logger.Error($"{LogPrefix} Telemetry error: {ex.Message}");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task BroadcastToAuthenticatedAsync(NetworkMessage message, CancellationToken ct)
    {
        foreach (var kvp in _clients)
        {
            var client = kvp.Value;
            if (!client.IsAuthenticated || client.Socket.State != WebSocketState.Open)
                continue;

            try
            {
                await SendMessageAsync(client.Socket, message, ct);
            }
            catch (Exception)
            {
                // They probably disconnected — the read loop will clean up
            }
        }
    }

    // ───────────────────── Shutdown ─────────────────────

    private void StopListener()
    {
        if (_cts is null) return;

        _serverApi.Logger.Notification($"{LogPrefix} Shutting down...");

        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException) { }

        try
        {
            if (_httpListener?.IsListening == true)
            {
                _httpListener.Stop();
                _httpListener.Close();
            }
        }
        catch (ObjectDisposedException) { }

        _cts.Dispose();
        _cts = null;
        _httpListener = null;

        _serverApi.Logger.Notification($"{LogPrefix} Shut down.");
    }
}
