# Shed Link Dashboard — Features & Changelog

## Project Overview
A custom WPF remote admin client for managing a Vintage Story game server.
Built with strict MVVM, no third-party UI or MVVM frameworks, custom dark-themed UI.

---

## Architecture

| Layer | Details |
|-------|---------|
| Pattern | MVVM (hand-rolled `ViewModelBase`, `RelayCommand`, `AsyncRelayCommand`) |
| UI | Custom WPF controls, no third-party libraries |
| Networking | `System.Net.WebSockets.ClientWebSocket` |
| Target | .NET 10, WPF |

---

## Features

### Batch 1 — Core Foundation & Custom Window
- **ViewModelBase** (`Core/ViewModelBase.cs`) — `INotifyPropertyChanged` with `SetProperty<T>` helper.
- **RelayCommand** (`Core/RelayCommand.cs`) — Synchronous `ICommand` wired to `CommandManager.RequerySuggested`.
- **Color Palette** (`Themes/Colors.xaml`) — Centralized `ResourceDictionary` with named `Color` and `SolidColorBrush` resources.
- **Custom Window Chrome** (`MainWindow.xaml`) — `WindowStyle="None"`, `AllowsTransparency="True"`, `WindowChrome` with `CaptionHeight="0"` and 6px resize grip. Outer `Border` with `CornerRadius="10"`.
- **Custom Title Bar** — Draggable grid with app icon, bound title, and Minimize / Maximize-Restore / Close buttons.
- **MainWindowViewModel** (`ViewModels/MainWindowViewModel.cs`) — `Title` property, `MinimizeCommand`, `MaximizeRestoreCommand`, `CloseCommand`.
- **Double-click maximize** — Title bar code-behind toggles `WindowState` on double-click.

### Batch 2 — Main UI Shell & Navigation
- **Sidebar** — 200px left column with `BackgroundMediumBrush`, bottom status area ("Status: Disconnected"), `CornerRadius` on bottom-left.
- **SidebarMenuButton style** — Custom `RadioButton` template (no radio circle), flat background, accent left strip, hover/checked triggers.
- **Navigation system** — `CurrentView` property on `MainWindowViewModel`, `NavigateCommand` switches via string parameter.
- **DataTemplates** — Implicit `DataTemplate` in `Window.Resources` maps `ConnectionViewModel` → `ConnectionView`, `DashboardViewModel` → `DashboardView`.
- **ConnectionViewModel / DashboardViewModel** — Empty stubs extending `ViewModelBase`.
- **ConnectionView / DashboardView** — Placeholder `UserControl` files.

### Batch 3 — Connection View
- **AsyncRelayCommand** (`Core/AsyncRelayCommand.cs`) — Async `ICommand`, disables during execution, invalidates `CommandManager` on completion.
- **PasswordBoxHelper** (`Core/PasswordBoxHelper.cs`) — Attached properties `Attach` and `BoundPassword` for two-way MVVM binding on `PasswordBox`.
- **ConnectionViewModel** — `ServerIp` (default `127.0.0.1`), `Port` (default `42420`), `SecurityToken`, `IsConnecting`. `ConnectCommand` (`AsyncRelayCommand`) simulates a 2-second delay then raises `Connected` event. `CanExecute` requires all fields non-empty.
- **ConnectionView** — Centered login card with labeled fields (TextBox × 2, PasswordBox × 1), Connect button, "Connecting..." indicator via `DataTrigger`.
- **Auto-navigation** — `MainWindowViewModel` subscribes to `ConnectionVm.Connected`, switches `CurrentView` to `DashboardVm`.

### Batch 4 — Premium Color Palette & Typography
- **Monochromatic palette** — Removed all blue tint. New hex values: `BackgroundDark #18181B`, `BackgroundMedium #09090B`, `Surface #27272A`, `InputBackground #18181B`, `Accent #10B981` (emerald), `AccentHover #059669`, `TextPrimary #FAFAFA`, `TextSecondary #A1A1AA`, `BorderSubtle #3F3F46`.
- **AppFont** (`Themes/Colors.xaml`) — `Segoe UI, Inter, sans-serif` font family resource, applied to root `Border` via `TextElement.FontFamily`.
- **HeaderStyle** — `TextBlock` style: 24px, SemiBold, TextPrimary.
- **LabelStyle** — `TextBlock` style: 12px, SemiBold, TextSecondary, uppercase by convention.

### Batch 5 — Premium Inputs & Buttons
- **ModernTextBox** — `InputBackgroundBrush` fill, 1px `BorderSubtleBrush` border, `CornerRadius="6"`, `IsKeyboardFocused` trigger swaps border to `AccentBrush`.
- **ModernPasswordBox** — Identical template targeting `PasswordBox`.
- **AccentButton** (rewritten) — `Foreground="White"`, `FontWeight="Bold"`, `CornerRadius="6"`, `Padding="0,10"`, hover → `AccentHoverBrush`, disabled → `BorderSubtleBrush`.
- **SidebarMenuButton** (rewritten) — `Padding="15,12"`, accent strip is a `Rectangle` with `Opacity="0"` (animatable), hover uses solid `#27272A`, checked sets `Opacity="1"` + `AccentBrush` foreground.

### Batch 6 — Re-assembled Connection View
- **CardBackgroundBrush** added to `Colors.xaml` (alias for `SurfaceColor #27272A`).
- **ConnectionView rewrite** — `CardBackgroundBrush`, `Padding="40"`, `Width="400"`, inline `DropShadowEffect` (Black, Blur 20, Opacity 0.3, Direction 270°). Each input group wrapped in its own `StackPanel` with `Margin="0,0,0,20"`. Header uses `HeaderStyle` at native 24px. Icon bumped to 32px.

### Batch 7 — Network Models & Interface
- **NetworkMessage** (`Models/Network/NetworkMessage.cs`) — JSON-based protocol message with `string Type` (e.g. `"Auth"`, `"Chat"`, `"PlayerList"`) and `JsonElement Payload` for arbitrary data.
- **AuthPayload** (`Models/Network/AuthPayload.cs`) — Authentication payload with `string Token`.
- **IWebSocketService** (`Services/IWebSocketService.cs`) — Service contract: `ConnectAsync(ip, port, token)`, `DisconnectAsync()`, `SendMessageAsync(NetworkMessage)`, `MessageReceived` event, `Disconnected` event.

### Batch 8 — WebSocket Service Implementation
- **ShedWebSocketService** (`Services/ShedWebSocketService.cs`) — Concrete `IWebSocketService` using `System.Net.WebSockets.ClientWebSocket`.
  - `ConnectAsync` — Tears down any existing socket, builds `ws://{ip}:{port}/shedlink` URI, connects, immediately sends an `"Auth"` `NetworkMessage` with `AuthPayload`, then spawns a background receive loop. Returns `true` on success, catches exceptions and returns `false`.
  - `ReceiveLoopAsync` — Reads frames in a loop using a 4 KB buffer, accumulates multi-frame messages via `MemoryStream`, deserializes complete messages to `NetworkMessage` with `System.Text.Json`, invokes `MessageReceived`. On close frame or exception invokes `Disconnected`.
  - `SendMessageAsync` — Serializes to JSON/UTF-8, sends under a `Lock` to prevent interleaved frames.
  - `DisconnectAsync` — Cancels receive loop, attempts graceful `CloseAsync` with 3-second timeout, disposes socket.
  - Implements `IDisposable` with proper cleanup of `CancellationTokenSource` and `ClientWebSocket`.
  - Uses `JsonNamingPolicy.CamelCase` for wire-format consistency.

### Batch 9 — Connecting the UI to WebSocket Service
- **ConnectionViewModel** — Now accepts `IWebSocketService` via constructor injection. `ConnectCommand` calls `_webSocketService.ConnectAsync(ServerIp, Port, SecurityToken)` instead of `Task.Delay`. Clears `ErrorMessage` on attempt, sets it to `"Failed to connect. Check IP, Port, or Token."` on failure, invokes `Connected` event on success.
- **ErrorMessage property** (`ConnectionViewModel`) — `string?` bound to a red error `TextBlock` in `ConnectionView.xaml`. Visible via dual `DataTrigger` (collapses when `null` or empty).
- **MainWindowViewModel** — Creates `ShedWebSocketService` instance, passes it to `ConnectionViewModel` constructor. Exposes `WebSocketService` property for future use by other VMs.

### Hotfix — .NET 10 Fluent Theme & TextBox Foreground
- **ThemeMode.None** (`App.xaml.cs`) — .NET 10 WPF enables the Fluent theme by default, which overrides `Foreground` on all controls with light-theme (dark text) colors. `ThemeMode = ThemeMode.None` in the `App` constructor disables the Fluent theme, restoring classic WPF behavior. Suppressed experimental API warning `WPF0001`.
- **TextElement.Foreground on root Border** (`MainWindow.xaml`) — Added `TextElement.Foreground="{StaticResource TextPrimaryBrush}"` to the root `Border` so all controls inherit `#FAFAFA` foreground via the visual tree.
- **ModernTextBox / ModernPasswordBox rewrite** (`App.xaml`) — Bulletproof template against .NET 10 theming:
  - `OverridesDefaultStyle="True"` — completely ignores the Fluent theme's hidden TextBox style.
  - `TextElement.Foreground="{TemplateBinding Foreground}"` on the template `Border` — forces the `#FAFAFA` brush down to WPF's internal text rendering engine via visual tree inheritance.
  - `PART_ContentHost` left plain (no `Foreground`/`Background` bindings) — prevents interference with WPF's internal `TextBoxView` rendering.
  - `Margin="{TemplateBinding Padding}"` on `PART_ContentHost` — padding applied as margin on the `ScrollViewer`.
  - `SelectionBrush="{StaticResource AccentBrush}"` — emerald text selection highlight.
  - `CaretBrush="{StaticResource TextPrimaryBrush}"` — white caret for visibility on dark background.
  - `IsEnabled="False"` trigger — 50% opacity fade for disabled state.

### Batch 10 — Shed Link Server-Side Mod (Vintage Story)
- **Standalone mod** — Separate project (`ShedLink/`) targeting Vintage Story (.NET 8.0). Acts as the WebSocket server counterpart to the WPF dashboard client.
- **modinfo.json** (`ShedLink/modinfo.json`) — `"type": "code"`, `"modid": "shedlink"`, `"side": "server"`, `"requiredOnClient": false`. Server-side only.
- **ShedLinkConfig** (`ShedLink/src/ShedLinkConfig.cs`) — `DashboardPort` (default `42420`), `SecurityToken` (default `"CHANGE_ME"`). Persisted to `config/shedlink.json` via the Vintage Story config API.
- **ShedLinkSystem** (`ShedLink/src/ShedLinkSystem.cs`) — Core `ModSystem`:
  - `ShouldLoad` — returns `true` only for `EnumAppSide.Server`.
  - `StartServerSide` — loads/generates `ShedLinkConfig`, warns if token is default, starts the `HttpListener`, registers shutdown cleanup.
  - `StartListener` — creates `HttpListener` on `http://*:{port}/shedlink/`, starts it, launches `ListenLoopAsync` on the thread pool via `Task.Run`.
  - `ListenLoopAsync` — awaits `GetContextAsync().WaitAsync(ct)` in a loop. Rejects non-WebSocket requests with `400 Bad Request`. Accepts WebSocket upgrades via `AcceptWebSocketAsync(null)`. Catches `OperationCanceledException`, `HttpListenerException`, and `ObjectDisposedException` for clean shutdown.
  - `StopListener` — cancels the `CancellationTokenSource`, stops/closes the `HttpListener`, disposes resources. Guarded against double-call and disposed objects.
  - `Dispose` — calls `StopListener`, then `base.Dispose()`.
  - Registered on `EnumServerRunPhase.Shutdown` for clean server-stop handling.

### Batch 11 — Server-Side Auth & Session Management
- **NetworkMessage** (`ShedLink/src/Models/NetworkMessage.cs`) — Server-side mirror of the dashboard's `NetworkMessage`. `string Type` + `JsonElement Payload`, deserialized with `JsonNamingPolicy.CamelCase` for wire-format parity.
- **AuthPayload** (`ShedLink/src/Models/AuthPayload.cs`) — Server-side mirror of the dashboard's `AuthPayload`. `string Token`.
- **AuthResponsePayload** (`ShedLink/src/Models/AuthResponsePayload.cs`) — New response model sent back to the client: `bool Success` and `string Message`.
- **ShedWebClient** (`ShedLink/src/ShedWebClient.cs`) — Lightweight session wrapper holding a `WebSocket`, `string ClientIp`, and `bool IsAuthenticated`.
- **HandleClientAsync** (`ShedLinkSystem`) — Per-client read loop spawned via `Task.Run` after `AcceptWebSocketAsync`. Reads full WebSocket messages (multi-frame via `MemoryStream`), deserializes to `NetworkMessage`, dispatches to `ProcessMessageAsync`. Catches `WebSocketException` and `OperationCanceledException` for clean exit. Calls `CloseSocketAsync` in `finally` and logs disconnect.
- **ProcessMessageAsync** — Routes by `message.Type`. `"Auth"` → `HandleAuthAsync`. Any other type from an unauthenticated client sends `AuthResponse { Success = false }` and closes the socket.
- **HandleAuthAsync** — Deserializes `AuthPayload` from `message.Payload`. Compares `payload.Token` against `_config.SecurityToken`. On match: sets `client.IsAuthenticated = true`, sends `AuthResponse { Success = true, Message = "Authenticated." }`. On mismatch: sends `AuthResponse { Success = false, Message = "Invalid security token." }`, closes socket. Malformed payload treated as failure.
- **ReadMessageAsync** — Reads WebSocket frames into a `MemoryStream` until `EndOfMessage`, deserializes via `JsonSerializer.DeserializeAsync<NetworkMessage>`. Returns `null` on close frame or empty read.
- **SendMessageAsync** — Serializes `NetworkMessage` to UTF-8 JSON, sends as a single text frame. Guards against sending on non-open sockets.
- **SerializePayload\<T\>** — Converts an object to `JsonElement` by round-tripping through `SerializeToUtf8Bytes` + `JsonDocument.Parse` + `RootElement.Clone()`.
- **CloseSocketAsync** — Graceful close with 3-second timeout. Swallows exceptions from already-closed or aborted sockets.
- **JsonOptions** — Static `JsonSerializerOptions` with `CamelCase` naming policy, matching the dashboard client's serializer.

### Batch 12 — Client-Side Handshake via TaskCompletionSource
- **AuthResponsePayload** (`Models/Network/AuthResponsePayload.cs`) — Client-side model matching the server's `AuthResponse` payload: `bool Success` and `string Message`.
- **IWebSocketService** — `ConnectAsync` return type changed from `Task<bool>` to `Task<(bool Success, string? ErrorMessage)>` so the UI layer receives the server's specific error message.
- **ShedWebSocketService** — Full auth-handshake flow:
  - `_authTcs` — Private `TaskCompletionSource<(bool, string?)>` with `RunContinuationsAsynchronously`. Created in `ConnectAsync` before sending the `"Auth"` message.
  - `ConnectAsync` — After sending `"Auth"` and spawning the receive loop, awaits `_authTcs.Task`. A 10-second `CancellationTokenSource` registers a callback that sets `(false, "Server did not respond in time.")` on timeout. Returns the tuple from the TCS. On exception: cancels the TCS, disposes the socket, returns `(false, "Failed to connect. Check IP and Port.")`.
  - `ReceiveLoopAsync` — Intercepts `"AuthResponse"` messages before raising `MessageReceived`. Deserializes `AuthResponsePayload`, calls `_authTcs.TrySetResult(...)` with `Success` and `Message`. Malformed responses resolve to `(false, "Malformed auth response from server.")`. Non-auth messages still fire `MessageReceived`.
  - `OnDisconnected` — Fails the TCS with `(false, "Connection lost before authentication completed.")` so `ConnectAsync` never hangs on early disconnect.
- **ConnectionViewModel** — Deconstructs the tuple from `ConnectAsync`. On failure, sets `ErrorMessage` to the server-provided message (e.g. `"Invalid security token."`) instead of a generic string.

### Batch 13 — Dashboard UI Layout
- **DashboardView** (`Views/DashboardView.xaml`) — Full dashboard layout with 2-row / 2-column `Grid`:
  - **Top Status Bar** (Row 0, spans both columns) — `CardBackgroundBrush` `Border` with `CornerRadius="8"`. Horizontal `StackPanel` showing three metric groups (TPS, RAM, UPTIME) each with `LabelStyle` title and `HeaderStyle` value at `FontSize="20"`. Bound to `Tps`, `RamUsage`, `Uptime` properties.
  - **Live Console** (Row 1, Column 0) — `InputBackgroundBrush` `Border` with `BorderSubtleBrush` 1px border, `CornerRadius="8"`. Contains a `ScrollViewer` with an `ItemsControl` bound to `ConsoleLines`. Each line rendered in `Cascadia Mono, Consolas, Courier New` at 12px, `TextSecondaryBrush` foreground, `TextWrapping="Wrap"`. Below the console, a command input row: `ModernTextBox` bound to `CommandText` + `AccentButton` "Send" (80px wide) bound to `SendCommand`.
  - **Player List** (Row 1, Column 1, 250px) — `CardBackgroundBrush` `Border` with `CornerRadius="8"`. "PLAYERS ONLINE" header in `LabelStyle`. `ItemsControl` bound to `Players` collection. Each player rendered as a mini-card (`InputBackgroundBrush`, `CornerRadius="6"`, `Padding="12,10"`) with player name (`TextPrimaryBrush`, 13px) on the left and ping in ms (`TextSecondaryBrush`, 11px) on the right.
- **DashboardViewModel** (`ViewModels/DashboardViewModel.cs`) — No longer a stub:
  - `Tps` (string, default `"20.0"`), `RamUsage` (string, default `"1.2 GB"`), `Uptime` (string, default `"12h 4m"`) — Status bar metrics.
  - `CommandText` (string) — Two-way bound to the command input `TextBox`.
  - `ConsoleLines` (`ObservableCollection<string>`) — Mock server log entries (10 lines: join events, chat, autosave, TPS, chunk warnings).
  - `Players` (`ObservableCollection<PlayerInfo>`) — Mock player list (3 players with names and ping).
  - `SendCommand` (`RelayCommand`) — Appends `"[Command] > {text}"` to `ConsoleLines` and clears `CommandText`. `CanExecute` requires non-empty input.
- **PlayerInfo** — Simple model class with `string Name` and `int Ping`. Primary constructor.

### Batch 14 — Server Telemetry Broadcaster
- **MetricsPayload** (`ShedLink/src/Models/MetricsPayload.cs`) — Telemetry snapshot: `string Tps`, `string RamUsage`, `string Uptime`. Sent in `"MetricsUpdate"` messages.
- **PlayerInfoPayload** (`ShedLink/src/Models/PlayerInfoPayload.cs`) — Individual player entry: `string Name`, `int Ping`.
- **PlayerListPayload** (`ShedLink/src/Models/PlayerListPayload.cs`) — Full online player roster: `List<PlayerInfoPayload> Players`. Sent in `"PlayerListUpdate"` messages.
- **Client Tracking** (`ShedLinkSystem`) — `ConcurrentDictionary<string, ShedWebClient> _clients` for thread-safe tracking of all connected WebSocket clients. `TryAdd` on accept, `TryRemove` in `HandleClientAsync` `finally` block before socket close.
- **TelemetryLoopAsync** (`ShedLinkSystem`) — Background `Task` launched from `StartServerSide` via `Task.Run`. 3-second initial delay to let the server finish starting. Runs a `while (!ct.IsCancellationRequested)` loop with 1-second `Task.Delay` interval:
  - **Metrics gathering** — `sapi.Server.ServerUptimeSeconds` → formatted `TimeSpan` (e.g. `"12h 4m"` or `"3m 22s"`). `GC.GetTotalMemory(false)` → formatted as `"1.2 GB"` or `"845 MB"`. `sapi.Server.CurrentTPS` → `"20.0"` format.
  - **Player gathering** — Iterates `sapi.World.AllOnlinePlayers`, populates `PlayerListPayload` with each player's `PlayerName` and `Ping`.
  - Serializes both payloads into `NetworkMessage` objects (`"MetricsUpdate"`, `"PlayerListUpdate"`) and calls `BroadcastToAuthenticatedAsync` for each.
- **BroadcastToAuthenticatedAsync** — Iterates `_clients`. For each client where `IsAuthenticated == true` and `Socket.State == Open`, calls `SendMessageAsync`. Swallows per-client exceptions (read loop handles cleanup).

### Batch 15 — WPF Dashboard Live Data
- **MetricsPayload** (`Models/Network/MetricsPayload.cs`) — Client-side model matching the server's telemetry snapshot: `string Tps`, `string RamUsage`, `string Uptime`.
- **PlayerInfoPayload** (`Models/Network/PlayerInfoPayload.cs`) — Client-side player entry: `string Name`, `int Ping`.
- **PlayerListPayload** (`Models/Network/PlayerListPayload.cs`) — Client-side player list container: `List<PlayerInfoPayload> Players`.
- **DashboardViewModel** (rewritten) — No longer uses mock data:
  - Accepts `IWebSocketService` via constructor injection.
  - Subscribes to `_webSocketService.MessageReceived` in the constructor.
  - `OnMessageReceived` — Dispatches by `message.Type`: `"MetricsUpdate"` → `HandleMetricsUpdate`, `"PlayerListUpdate"` → `HandlePlayerListUpdate`.
  - `HandleMetricsUpdate` — Deserializes `MetricsPayload` from `JsonElement`, updates `Tps`, `RamUsage`, `Uptime` via `Application.Current.Dispatcher.Invoke` for thread safety.
  - `HandlePlayerListUpdate` — Deserializes `PlayerListPayload` from `JsonElement`, clears and repopulates `Players` `ObservableCollection` via `Application.Current.Dispatcher.Invoke`.
  - Default property values changed from mock strings to `"--"` placeholder until live data arrives.
  - `ConsoleLines` and `Players` initialized as empty collections.
  - `SendCommand` unchanged — still appends `"[Command] > {text}"` to `ConsoleLines`.
  - Static `JsonSerializerOptions` with `CamelCase` naming policy for deserialization parity with the server.
- **MainWindowViewModel** — `DashboardVm` changed from `new()` to `new DashboardViewModel(WebSocketService)` to inject the WebSocket service.

### Batch 16 — Console Injection & Chat Streaming
- **CommandPayload** (`Models/Network/CommandPayload.cs`) — Client-side payload for console commands: `string Command`.
- **ConsoleLogPayload** (`Models/Network/ConsoleLogPayload.cs`) — Client-side payload for log lines received from the server: `string Message`.
- **CommandPayload** (`ShedLink/src/Models/CommandPayload.cs`) — Server-side mirror: `string Command`. Received in `"ConsoleCommand"` messages.
- **ConsoleLogPayload** (`ShedLink/src/Models/ConsoleLogPayload.cs`) — Server-side log payload: `string Message`. Sent in `"ConsoleLog"` messages.
- **DashboardViewModel** — `SendCommand` upgraded from `RelayCommand` to `AsyncRelayCommand`:
  - Captures `CommandText`, clears the input, appends `"[Command] > {text}"` to `ConsoleLines`, then constructs a `"ConsoleCommand"` `NetworkMessage` with `CommandPayload` and sends it via `_webSocketService.SendMessageAsync`.
  - `OnMessageReceived` now also handles `"ConsoleLog"` → `HandleConsoleLog`.
  - `HandleConsoleLog` — Deserializes `ConsoleLogPayload` from `JsonElement`, appends `Message` to `ConsoleLines` via `Application.Current.Dispatcher.Invoke`.
  - `AppendConsoleLine` helper — Adds a line to `ConsoleLines` and trims from the front when the collection exceeds `MaxConsoleLines` (100) to prevent memory leaks.
- **ShedLinkSystem** — Console command execution and chat forwarding:
  - `ProcessMessageAsync` — New `"ConsoleCommand"` case: if client `IsAuthenticated`, dispatches to `HandleConsoleCommandAsync`.
  - `HandleConsoleCommandAsync` — Deserializes `CommandPayload` from the message payload, logs the command with the client IP, and executes it on the Vintage Story server via `sapi.InjectConsole(cmd.Command)`.
  - `OnPlayerChat` — Event handler registered on `sapi.Event.PlayerChat` in `StartServerSide`. Formats the chat as `"[Chat] {playerName}: {message}"` and fire-and-forgets `BroadcastConsoleLogAsync`.
  - `BroadcastConsoleLogAsync` — Wraps a text string in a `"ConsoleLog"` `NetworkMessage` with `ConsoleLogPayload` and calls `BroadcastToAuthenticatedAsync` to push it to all connected dashboards.

### Batch 17 — Player Quick-Actions
- **PlayerActionPayload** (`Models/Network/PlayerActionPayload.cs`) — Client-side payload: `string ActionType` (`"Kick"`, `"Ban"`, `"Mute"`), `string TargetPlayer`, `string Reason`.
- **PlayerActionPayload** (`ShedLink/src/Models/PlayerActionPayload.cs`) — Server-side mirror of the client model.
- **DashboardView** — Player card `Border` in the player list `ItemsControl` now has a right-click `ContextMenu` with three `MenuItem`s: "Kick Player", "Ban Player", "Mute Player".
  - `Tag` on the `Border` stores the `DashboardViewModel` DataContext via `RelativeSource AncestorType=ItemsControl`.
  - Each `MenuItem.Command` binds to `PlacementTarget.Tag.{Action}PlayerCommand` (via `RelativeSource AncestorType=ContextMenu`) to escape the ContextMenu's detached visual tree.
  - `CommandParameter` binds to `PlacementTarget.DataContext` (the `PlayerInfo` item).
- **DashboardViewModel** — Three new `AsyncRelayCommand` properties:
  - `KickPlayerCommand` — Casts `CommandParameter` to `PlayerInfo`, calls `SendPlayerActionAsync("Kick", player.Name)`.
  - `BanPlayerCommand` — Casts `CommandParameter` to `PlayerInfo`, calls `SendPlayerActionAsync("Ban", player.Name)`.
  - `MutePlayerCommand` — Casts `CommandParameter` to `PlayerInfo`, calls `SendPlayerActionAsync("Mute", player.Name)`.
  - `SendPlayerActionAsync(actionType, targetPlayer, reason)` — Appends `"[Action] {actionType} → {targetPlayer}"` to `ConsoleLines`, constructs a `"PlayerAction"` `NetworkMessage` with `PlayerActionPayload`, and sends it via `_webSocketService.SendMessageAsync`.
- **ShedLinkSystem** — Player action execution:
  - `ProcessMessageAsync` — New `"PlayerAction"` case: if client `IsAuthenticated`, dispatches to `HandlePlayerActionAsync`.
  - `HandlePlayerActionAsync` — Deserializes `PlayerActionPayload`. Maps `ActionType` to a Vintage Story console command: `"kick"` → `/kick {player} {reason}`, `"ban"` → `/ban {player} {reason}`, `"mute"` → `/players {player} mute`. Executes via `sapi.InjectConsole(command)`. Broadcasts a `"ConsoleLog"` confirmation (`"[Dashboard] {action}: {player}"`) to all authenticated dashboards.

### Batch 18 — Full Server Logs & Auto-Scroll
- **Server Logger Hook** (`ShedLinkSystem`) — `sapi.Server.LogEntryAdded += OnLogEntryAdded` registered in `StartServerSide`.
  - `OnLogEntryAdded(EnumLogType, string, object[])` — Filters out lines containing `LogPrefix` (`"[Shed Link]"`) to prevent feedback loops. Formats the log as `"[{LogType}] {message}"` (with `string.Format` for parameterized entries). Fire-and-forgets `BroadcastConsoleLogAsync` to push to all connected dashboards.
- **AutoScrollBehavior** (`Core/AutoScrollBehavior.cs`) — WPF attached property for MVVM-friendly auto-scrolling:
  - `AutoScroll` (`bool`) — `DependencyProperty.RegisterAttached` targeting `ScrollViewer`.
  - When `true`, hooks `ScrollViewer.ScrollChanged`. On `ExtentHeightChange > 0` (new content added), calls `ScrollToBottom()`.
  - When `false`, unhooks the event to prevent leaks.
- **DashboardView** — Console `ScrollViewer` updated:
  - Added `xmlns:core="clr-namespace:Shed_Security_AP.Core"` namespace.
  - Applied `core:AutoScrollBehavior.AutoScroll="True"` to the console `ScrollViewer` so it automatically scrolls to the bottom when new log lines arrive.

### Batch 19 — Disconnect Handling & Server Power
- **Server Power Buttons** (`DashboardView.xaml`) — Status bar converted from `StackPanel` to `DockPanel`. Two `AccentButton`s ("Restart", "Stop") docked right. Metrics remain left-aligned.
- **RestartServerCommand** (`DashboardViewModel`) — `AsyncRelayCommand` that appends `"[Power] Restarting server..."` to console and sends a `"ConsoleCommand"` `NetworkMessage` with `/shed restart`.
- **StopServerCommand** (`DashboardViewModel`) — `AsyncRelayCommand` that appends `"[Power] Stopping server..."` to console and sends a `"ConsoleCommand"` `NetworkMessage` with `/stop`.
- **Disconnect Handling** (`MainWindowViewModel`) — Subscribes to `WebSocketService.Disconnected` event:
  - Uses `Application.Current.Dispatcher.Invoke` for thread safety.
  - Calls `DashboardVm.ResetState()` to unsubscribe from `MessageReceived`, clear metrics/console/players.
  - Sets `ConnectionVm.ErrorMessage = "Connection lost to server."` to display the error on the connection screen.
  - Sets `StatusText = "Status: Disconnected"` and navigates `CurrentView` back to `ConnectionVm`.
- **StatusText Property** (`MainWindowViewModel`) — New bindable `string` property (default `"Status: Disconnected"`). Set to `"Status: Connected"` on successful connection, `"Status: Disconnected"` on disconnect.
- **Sidebar Status Binding** (`MainWindow.xaml`) — Hardcoded `"Status: Disconnected"` replaced with `{Binding StatusText}` for live status display.
- **ResetState** (`DashboardViewModel`) — Unsubscribes from `_webSocketService.MessageReceived`, resets `Tps`/`RamUsage`/`Uptime` to `"--"`, clears `CommandText`, `ConsoleLines`, and `Players`.
- **Resubscribe** (`DashboardViewModel`) — Re-attaches `_webSocketService.MessageReceived` handler. Called from `MainWindowViewModel` on reconnect (via `Connected` event) to restore live data flow.
- **Connected Handler Update** (`MainWindowViewModel`) — Now calls `DashboardVm.Resubscribe()` and sets `StatusText = "Status: Connected"` in addition to navigating to the dashboard.

### Batch 20 — Saved Server Profiles
- **ServerProfile** (`Models/Local/ServerProfile.cs`) — Data model for saved connections: `string Name`, `string Ip`, `string Port`, `string Token`.
- **ProfileManager** (`Core/ProfileManager.cs`) — Static utility class for persisting profiles:
  - Saves/loads a `List<ServerProfile>` to `%AppData%\ShedLink\profiles.json` using `System.Text.Json`.
  - `Load()` — Reads and deserializes the JSON file. Returns an empty list if the file is missing or malformed.
  - `Save(List<ServerProfile>)` — Creates the directory if needed, serializes with `WriteIndented = true`, writes to disk.
- **ConnectionViewModel** — Profile management additions:
  - `SavedProfiles` (`ObservableCollection<ServerProfile>`) — Populated from `ProfileManager.Load()` in the constructor.
  - `SelectedProfile` (`ServerProfile?`) — When set to a non-null profile, auto-fills `ServerIp`, `Port`, and `SecurityToken` from the profile.
  - `SaveProfileCommand` (`RelayCommand`) — Saves the current inputs as a profile named `"{Ip}:{Port}"`. Updates an existing profile if one with the same name exists, otherwise adds a new entry. Calls `ProfileManager.Save(...)` to persist.
  - `LoadProfiles()` — Private helper called from the constructor to populate `SavedProfiles` from disk.
- **ModernComboBox Style** (`App.xaml`) — Dark-themed `ComboBox` with custom `ControlTemplate`:
  - `ComboBoxToggleButton` template — `InputBackgroundBrush` background, `BorderSubtleBrush` border, `CornerRadius="6"`, chevron arrow in `TextSecondaryBrush`. Accent border on hover.
  - `ModernComboBox` style — `OverridesDefaultStyle="True"`, 38px height, dropdown `Popup` with `CardBackgroundBrush` background and `BorderSubtleBrush` border.
  - `ModernComboBoxItem` style — `OverridesDefaultStyle="True"`, highlight with `InputBackgroundBrush`, selected item in `AccentBrush` foreground.
- **ConnectionView** — Profile UI additions:
  - "SAVED PROFILES" `ComboBox` at the top of the login card, bound to `SavedProfiles` with `DisplayMemberPath="Name"` and `SelectedItem` bound to `SelectedProfile`.
  - "Save Profile" link-style button below the Security Token field, bound to `SaveProfileCommand`. Styled as underlined text that turns accent on hover.

### Batch 21 — Anti-Cheat Dashboard Tab
- **SecurityAlert** (`Models/Local/SecurityAlert.cs`) — Immutable model (primary constructor): `string Timestamp`, `string PlayerName`, `string AlertType`, `string Details`.
- **SecurityAlertPayload** (`Models/Network/SecurityAlertPayload.cs`) — Network payload for incoming anti-cheat alerts: `string Timestamp`, `string PlayerName`, `string AlertType`, `string Details`.
- **ShadowbanListPayload** (`Models/Network/ShadowbanListPayload.cs`) — Network payload for the shadowbanned player roster: `List<string> Players`.
- **AntiCheatViewModel** (`ViewModels/AntiCheatViewModel.cs`) — Dedicated VM for the Shed Security anti-cheat tab:
  - `LiveAlerts` (`ObservableCollection<SecurityAlert>`) — Latest alerts inserted at index 0 (newest first). Capped at `MaxAlerts` (200).
  - `ShadowbannedPlayers` (`ObservableCollection<string>`) — Current shadowbanned/frozen players, replaced in full on each `"ShadowbanListUpdate"` message.
  - `PardonPlayerCommand` (`AsyncRelayCommand`) — Sends a `"PlayerAction"` `NetworkMessage` with `ActionType = "Pardon"` for the selected player. Removes the player from `ShadowbannedPlayers` on the UI thread.
  - `OnMessageReceived` — Handles `"SecurityAlert"` → `HandleSecurityAlert` and `"ShadowbanListUpdate"` → `HandleShadowbanListUpdate`.
  - `ResetState()` / `Resubscribe()` — Same pattern as `DashboardViewModel` for disconnect/reconnect lifecycle.
- **AntiCheatView** (`Views/AntiCheatView.xaml`) — Two-column layout:
  - **Left Column — "LIVE ALERTS"** — `CardBackgroundBrush` panel with `ScrollViewer` + `ItemsControl`. Each alert rendered as a card with an `AlertType` badge (`BorderSubtleBrush` background, `AccentBrush` text), player name, timestamp, and detail text with `TextWrapping`.
  - **Right Column — "SHADOWBANNED PLAYERS"** (280px) — `CardBackgroundBrush` panel with player names and a compact "Pardon" `AccentButton` per entry. Pardon command bound via `RelativeSource AncestorType=ItemsControl`.
- **Navigation** (`MainWindowViewModel`) — `AntiCheatVm` property added, instantiated with `WebSocketService`. `NavigateCommand` switch updated with `"AntiCheat"` case. `Connected` handler calls `AntiCheatVm.Resubscribe()`. `Disconnected` handler calls `AntiCheatVm.ResetState()`.
- **Sidebar** (`MainWindow.xaml`) — New "🛡 Anti-Cheat" `RadioButton` added after the Dashboard button, routing to `CommandParameter="AntiCheat"`.
- **DataTemplate** (`MainWindow.xaml`) — `AntiCheatViewModel` → `AntiCheatView` implicit `DataTemplate` added to `Window.Resources`.

### Batch 22 — Shed Security Event Hooking
- **SecurityAlertPayload** (`ShedLink/src/Models/SecurityAlertPayload.cs`) — Server-side alert model: `string Timestamp`, `string PlayerName`, `string AlertType`, `string Details`, `int StrikeCount`.
- **ShedSecurityPrefix** (`ShedLinkSystem`) — New constant `"[Shed Security]"` for detecting Shed Security log lines.
- **OnLogEntryAdded Enhancement** (`ShedLinkSystem`) — The existing logger hook now checks formatted log lines for `ShedSecurityPrefix`. When detected, calls `TryBroadcastSecurityAlertAsync` to parse and broadcast the alert as a `"SecurityAlert"` `NetworkMessage`. The line still flows through the normal `ConsoleLog` broadcast.
- **TryBroadcastSecurityAlertAsync** (`ShedLinkSystem`) — Parses Shed Security log lines with expected format: `"[Shed Security] VIOLATION_TYPE | PlayerName | Details | Strikes: N"`. Extracts `violationType`, `playerName`, `details`, and `strikeCount` via pipe-delimited splitting with `StringSplitOptions.TrimEntries`. Falls back to raw text if the format doesn't match. Generates a UTC `HH:mm:ss` timestamp and broadcasts a `"SecurityAlert"` `NetworkMessage` with `SecurityAlertPayload` to all authenticated clients.
- **StrikeCount** — Added to the full alert pipeline:
  - `SecurityAlertPayload` (client) — New `int StrikeCount` property.
  - `SecurityAlert` (model) — New `int StrikeCount` in primary constructor and property.
  - `AntiCheatViewModel.HandleSecurityAlert` — Passes `StrikeCount` through to `SecurityAlert` constructor.
  - `AntiCheatView.xaml` — Alert card grid expanded to 4 columns. New "×N" strike count display between player name and timestamp.
- **WPF Consumption** — `AntiCheatViewModel` already subscribes to `MessageReceived` (from Batch 21). Handles `"SecurityAlert"` messages by deserializing `SecurityAlertPayload` and inserting into `LiveAlerts` via `Dispatcher.Invoke`.

### Batch 23 — Desktop Notifications for Alerts
- **Focus Tracking** (`MainWindow.xaml.cs`) — `IsAppFocused` public `bool` property. `OnActivated` override sets it to `true`, `OnDeactivated` sets it to `false`. Allows the ViewModel layer to query whether the window currently has user focus without breaking MVVM (accessed via a `Func<bool>` delegate).
- **INotificationService** (`Services/INotificationService.cs`) — Service contract: `ShowAlert(string title, string message)`. Extends `IDisposable` for resource cleanup.
- **WindowsNotificationService** (`Services/WindowsNotificationService.cs`) — Concrete `INotificationService` using `System.Windows.Forms.NotifyIcon`:
  - Constructor creates a `NotifyIcon` with `System.Drawing.SystemIcons.Shield` icon, `"Shed Link Dashboard"` tooltip text, and `Visible = true`.
  - `ShowAlert` — Calls `NotifyIcon.ShowBalloonTip` with 3-second timeout, `ToolTipIcon.Warning`, on the WPF dispatcher thread. Guards against disposed state.
  - `Dispose` — Sets `Visible = false` and disposes the `NotifyIcon`. Guarded against double-dispose.
- **UseWindowsForms** (`Shed Security AP.csproj`) — `<UseWindowsForms>true</UseWindowsForms>` added to enable `System.Windows.Forms.NotifyIcon`. `<Using Remove="System.Windows.Forms" />` added to prevent implicit global using conflicts with WPF types (`Application`, `UserControl`).
- **AntiCheatViewModel** — Constructor expanded to accept `INotificationService` and `Func<bool> isAppFocused`. `HandleSecurityAlert` now checks `_isAppFocused()` after inserting the alert into `LiveAlerts`. If the app is **not** focused, calls `_notificationService.ShowAlert("Shed Security Alert", "{PlayerName} triggered {AlertType}!")` to show a native balloon-tip notification.
- **MainWindowViewModel** — Creates `WindowsNotificationService` instance, exposes it as `NotificationService` property. Passes `NotificationService` and a `Func<bool>` lambda (reads `MainWindow.IsAppFocused` via `Application.Current.MainWindow`) to `AntiCheatViewModel` constructor.

### Batch 24 — Remote Configuration Editor
- **ConfigRequestPayload** (`Models/Network/ConfigRequestPayload.cs`) — Empty client-side payload. Sent in `"ConfigRequest"` messages to ask the server for the anti-cheat config file.
- **ConfigResponsePayload** (`Models/Network/ConfigResponsePayload.cs`) — Client-side payload: `string JsonContent`. Received in `"ConfigResponse"` messages containing the raw JSON of `shedsecurity.json`.
- **ConfigSavePayload** (`Models/Network/ConfigSavePayload.cs`) — Client-side payload: `string JsonContent`. Sent in `"ConfigSave"` messages to overwrite the anti-cheat config on the server.
- **ConfigRequestPayload** (`ShedLink/src/Models/ConfigRequestPayload.cs`) — Server-side mirror of the empty request payload.
- **ConfigResponsePayload** (`ShedLink/src/Models/ConfigResponsePayload.cs`) — Server-side response payload: `string JsonContent`.
- **ConfigSavePayload** (`ShedLink/src/Models/ConfigSavePayload.cs`) — Server-side mirror of the save payload: `string JsonContent`.
- **SettingsViewModel** (`ViewModels/SettingsViewModel.cs`) — Dedicated VM for the remote config editor tab:
  - Accepts `IWebSocketService` via constructor injection. Subscribes to `MessageReceived`.
  - `ConfigJson` (`string`) — Two-way bound to the editor `TextBox`. Holds the raw JSON content.
  - `StatusMessage` (`string`) — Feedback text (e.g. `"Config loaded."`, `"Saving config..."`).
  - `IsBusy` (`bool`) — Indicates an in-flight request. Controls a spinner indicator via `DataTrigger`.
  - `FetchConfigCommand` (`AsyncRelayCommand`) — Sets `IsBusy`, sends a `"ConfigRequest"` `NetworkMessage` with empty `ConfigRequestPayload`.
  - `SaveConfigCommand` (`AsyncRelayCommand`) — Wraps `ConfigJson` in a `"ConfigSave"` `NetworkMessage` with `ConfigSavePayload`, sends it. `CanExecute` requires non-empty `ConfigJson`.
  - `OnMessageReceived` — Handles `"ConfigResponse"` → `HandleConfigResponse`.
  - `HandleConfigResponse` — Deserializes `ConfigResponsePayload`, sets `ConfigJson` and clears `IsBusy` via `Dispatcher.Invoke`.
  - `ResetState()` / `Resubscribe()` — Same lifecycle pattern as other VMs.
- **SettingsView** (`Views/SettingsView.xaml`) — Three-row `Grid` layout:
  - **Header Row** — `DockPanel` with `"Anti-Cheat Configuration"` title (`HeaderStyle`, 20px) on the left, "Fetch Config" and "Save Config" `AccentButton`s on the right.
  - **Editor Row** — `InputBackgroundBrush` `Border` with `CornerRadius="8"`, containing a `TextBox` with `AcceptsReturn="True"`, `AcceptsTab="True"`, `TextWrapping="NoWrap"`, `Cascadia Mono` font at 13px. Horizontal and vertical `ScrollBarVisibility="Auto"`. Styled with `ModernTextBox`.
  - **Status Row** — `DockPanel` with `StatusMessage` text on the left and a spinner `StackPanel` (visible via `DataTrigger` when `IsBusy` is `True`) on the right.
- **Navigation** (`MainWindowViewModel`) — `SettingsVm` property added, instantiated with `WebSocketService`. `NavigateCommand` switch updated with `"Settings"` case. `Connected` handler calls `SettingsVm.Resubscribe()`. `Disconnected` handler calls `SettingsVm.ResetState()`.
- **Sidebar** (`MainWindow.xaml`) — New "⚙ Settings" `RadioButton` added after the Anti-Cheat button, routing to `CommandParameter="Settings"`.
- **DataTemplate** (`MainWindow.xaml`) — `SettingsViewModel` → `SettingsView` implicit `DataTemplate` added to `Window.Resources`.
- **ShedLinkSystem** — Remote config handling:
  - `ProcessMessageAsync` — New `"ConfigRequest"` and `"ConfigSave"` cases (authenticated only).
  - `HandleConfigRequestAsync` — Reads `shedsecurity.json` from the Vintage Story `ModConfig` folder via `_serverApi.GetOrCreateDataPath("ModConfig")`. Sends the file contents as a `"ConfigResponse"` `NetworkMessage` with `ConfigResponsePayload`. Falls back to `"{}"` if the file is missing or unreadable.
  - `HandleConfigSaveAsync` — Deserializes `ConfigSavePayload` from the message. Overwrites `shedsecurity.json` with the incoming `JsonContent`. Calls `_serverApi.InjectConsole("/shedsecurity reload")` to trigger a hot-reload. Broadcasts a `"ConsoleLog"` confirmation to all authenticated dashboards.

### Batch 25 — Premium Palette & Sidebar Redesign
- **Color Palette Refresh** (`Themes/Colors.xaml`) — Shifted from pure zinc grays to a richer slate/dark theme for a premium hardware-monitoring feel:
  - `BackgroundDarkColor` — `#18181B` → `#1C1C21` (warmer app background).
  - `BackgroundMediumColor` — `#09090B` → `#151519` (deeper sidebar/title bar).
  - `SurfaceColor` / `CardBackgroundBrush` — `#27272A` → `#232329` (richer card surfaces).
  - `InputBackgroundColor` — `#18181B` → `#151519` (inset fields match sidebar depth).
  - `BorderSubtleColor` — `#3F3F46` → `#33333B` (softer border lines).
  - Accent colors (`#10B981`, `#059669`) and text colors (`#FAFAFA`, `#A1A1AA`) unchanged.
- **SidebarMenuButton Rewrite** (`App.xaml`) — Completely redesigned sidebar navigation buttons:
  - **Layout** — Switched from horizontal icon+text `StackPanel` to vertical `StackPanel` with icon centered above text. Icon bumped to 18px, text at 11px.
  - **Dimensions** — `Height="70"`, `Padding="10"`, creating a near-square button shape.
  - **ActiveIndicator** — Renamed from `AccentStrip`. Sharp 4px wide, 28px tall `Rectangle` with `RadiusX="2"` / `RadiusY="2"`, vertically centered on the left edge. `Fill="{StaticResource AccentBrush}"`, `Opacity="0"` by default.
  - **Hover Trigger** — Background changes to `#1C1C21` (matching the new app background), foreground to `TextPrimaryBrush`. Subtle lift without a heavy box.
  - **Checked Trigger** — `ActiveIndicator` fades in (`Opacity="1"`), background stays transparent, foreground set to `White` for a clean highlighted state without a bulky filled rectangle.
- **Sidebar Button Content** (`MainWindow.xaml`) — All four nav buttons (Connection, Dashboard, Anti-Cheat, Settings) updated to use vertical `StackPanel` layout with centered icon and text.

### Batch 26 — Dashboard Metric Cards
- **ModernProgressBar Style** (`App.xaml`) — Custom `ProgressBar` with `OverridesDefaultStyle="True"`:
  - `Height="8"`, track background `#151519`, fill via `AccentBrush`.
  - Fully rounded caps: both `PART_Track` and `PART_Indicator` borders use `CornerRadius="4"`.
  - `PART_Indicator` is `HorizontalAlignment="Left"` so WPF stretches it proportionally.
- **Dashboard Top Row Redesign** (`Views/DashboardView.xaml`) — Replaced the single `DockPanel` status bar with a `UniformGrid` (`Rows="1"`, `Columns="3"`) containing three distinct metric cards:
  - **TPS Card** — `CardBackgroundBrush` `Border` with `CornerRadius="8"`, `Padding="15"`. `LabelStyle` title, `HeaderStyle` value at 22px bound to `Tps`, and a `ModernProgressBar` (`Value="20"`, `Maximum="20"`).
  - **RAM Card** — Same card structure. Value bound to `RamUsage`, `ModernProgressBar` (`Value="50"`, `Maximum="100"` — placeholder binding for visual polish).
  - **Uptime Card** — Uses a `Grid` layout with three rows: label, value bound to `Uptime`, and server power buttons ("Restart" / "Stop") nested at the bottom. Buttons resized to `Height="30"`, `FontSize="12"`, `Padding="12,0"` for compact fit inside the card.
  - Cards are spaced with asymmetric margins (`0,0,8,0` / `4,0,4,0` / `8,0,0,0`) for a uniform 8px gap between them.

### Batch 27 — Layout Framing & Section Headers
- **DashboardView.xaml — Bottom Row Polish**
  - Added `Margin="0,15,12,0"` to the Console `Grid` and `Margin="0,15,0,0"` to the Player List `Border` for a 15px gap separating the metric cards from the bottom row.
  - Console card now has an inner `Grid` with a header row: a `⌨` icon + "Live Server Console" (`HeaderStyle` at 16px) above the `ScrollViewer`.
  - Player List card gained `BorderBrush="{StaticResource BorderSubtleBrush}"` and `BorderThickness="1"` for edge definition. Header replaced with a `👥` icon + "Players Online" (`HeaderStyle` at 16px).
- **AntiCheatView.xaml — Card & Header Polish**
  - Both column `Border` elements now have `BorderBrush="{StaticResource BorderSubtleBrush}"` and `BorderThickness="1"` to define card edges against the background.
  - Live Alerts column margin increased from `0,0,12,0` to `0,0,15,0` for better breathing room.
  - "LIVE ALERTS" header replaced with `🚨` icon + "Live Alerts" (`HeaderStyle` 16px).
  - "SHADOWBANNED PLAYERS" header replaced with `🚫` icon + "Shadowbanned Players" (`HeaderStyle` 16px).
- **SettingsView.xaml — Code Editor & Header Polish**
  - Header renamed from "Anti-Cheat Configuration" to "Server Configuration" with a `⚙` icon prefix.
  - Fetch/Save buttons restyled: `Height="34"`, `FontSize="12"`, `Padding="16,0"`, `Margin="0,0,10,0"` between them (no fixed width).
  - JSON editor wrapped in a `CardBackgroundBrush` card with its own "CONFIG EDITOR" `LabelStyle` sub-header and `📝` icon.
  - Inner `TextBox` padding increased from `12` to `16,12` for a more spacious code-editor feel. Inner border uses `CornerRadius="6"`.

### Batch 28 — Single-File Publish & Polish
- **Single-File Executable** — Added `<PublishSingleFile>true</PublishSingleFile>` and `<SelfContained>true</SelfContained>` with `<RuntimeIdentifier>win-x64</RuntimeIdentifier>` to compile the WPF app into one portable `.exe`.
- **Native Library Extraction** — `<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>` ensures WPF's native dependencies are bundled inside the single file.
- **Embedded PDB** — `<DebugType>embedded</DebugType>` embeds debug symbols directly into the assembly, producing a perfectly clean output folder with no `.pdb` files.
- **Application Icon** — `<ApplicationIcon>app.ico</ApplicationIcon>` links a placeholder accent-green icon with "S" glyph. Replace `app.ico` with a custom icon at any time.
- **Assembly Metadata** — `<Product>Shed Link</Product>`, `<Company>Shed Security</Company>`, `<Version>1.0.0</Version>`, `<FileVersion>1.0.0.0</FileVersion>` embedded in the compiled assembly.

### Batch 29 — True Radiograph Sidebar Layout
- **Segoe Fluent Icons** (`MainWindow.xaml`) — Removed all emoji icons (🔌, 📊, 🛡, ⚙) from the sidebar buttons. Replaced with proper `Segoe Fluent Icons, Segoe MDL2 Assets` font glyphs at 24px:
  - Connection → `&#xE8BA;` (network plug)
  - Dashboard → `&#xE9D9;` (data bar)
  - Anti-Cheat → `&#xEA18;` (shield)
  - Settings → `&#xE713;` (gear)
- **SidebarMenuButton Rewrite** (`App.xaml`) — Complete template overhaul for a Radiograph-style layout:
  - `OverridesDefaultStyle="True"` added to prevent Fluent theme interference.
  - **Dimensions** — `Height="80"`, `Width="80"` creating a true square button shape.
  - **Margin** — Tightened to `0,2` for compact vertical stacking.
  - **ActiveIndicator** — 4px wide, 20px tall pill (`RadiusX="2"`, `RadiusY="2"`), vertically centered on the far left edge. `Opacity="0"` by default, fades in on `IsChecked`.
  - **Background Border** — `CornerRadius="8"`, `Margin="6,0,0,0"` to leave space for the indicator pill on the left edge.
  - **ContentPresenter** centered both horizontally and vertically inside the border.
  - **Hover Trigger** — Background `#FF1C1C21`, foreground to `TextPrimaryBrush`.
  - **Checked Trigger** — ActiveIndicator `Opacity="1"`, transparent background, white foreground.
- **Button Content** (`MainWindow.xaml`) — Each button's content uses a `StackPanel Orientation="Vertical"` with icon (`FontSize="24"`, `Margin="0,0,0,8"`) above label (`FontSize="12"`), both `HorizontalAlignment="Center"`.
- **Sidebar Width** (`MainWindow.xaml`) — Column shrunk from `200` to `100` to tightly hug the square buttons.
- **Status Area** — Font reduced to 10px, margins tightened to `8,0,8,12`, `TextWrapping="Wrap"` and `TextAlignment="Center"` for readability in the narrower column.
- **Nav StackPanel** — Added `HorizontalAlignment="Center"` to center the 80px buttons within the 100px column.

### Batch 30 — Radiograph Metric Cards
- **ModernProgressBar Thickened** (`App.xaml`) — `Height` increased from `8` to `12` for a bolder hardware-monitor feel. `CornerRadius` updated from `4` to `6` on both `PART_Track` and `PART_Indicator` to match the thicker profile. Track background remains `#151519` for the inset groove look.
- **TPS Card Redesign** (`Views/DashboardView.xaml`) — Replaced the stacked label+big-value layout with a compact Radiograph-style `Grid`:
  - Row 0: "TPS" label (`TextSecondaryBrush`, 12px, `SemiBold`, left-aligned) and live TPS value (`TextPrimaryBrush`, 12px, right-aligned) on the same line.
  - Row 1: `ModernProgressBar` (`Value="20"`, `Maximum="20"`, `Margin="0,8,0,0"`) underneath.
  - Entire content `VerticalAlignment="Center"` within the card.
- **RAM Card Redesign** — Same compact layout: "RAM" label left, `RamUsage` value right, thick progress bar below (`Value="50"`, `Maximum="100"`).
- **Uptime Card Redesign** — "UPTIME" label left, `Uptime` value right. Row 1 replaced with right-aligned power buttons ("Restart" / "Stop") at `Height="28"`, `FontSize="11"`, `Padding="10,0"`, `Margin="0,8,0,0"`.

### Batch 31 — Segoe Fluent Icons & Card Shadows
- **View Emoji Removal** — Replaced all remaining emoji icons in view headers with `Segoe Fluent Icons, Segoe MDL2 Assets` font glyphs for a consistent, professional look:
  - **DashboardView** — `⌨` → `&#xE756;` (keyboard/console), `👥` → `&#xE716;` (people/players).
  - **AntiCheatView** — `🚨` → `&#xEA39;` (alert/warning), `🚫` → `&#xE8F8;` (block/banned).
  - **SettingsView** — `⚙` → `&#xE713;` (gear), `📝` → `&#xE70F;` (edit pencil).
  - All icon `TextBlock`s use `FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"` with explicit `FontSize` matching original emoji sizing.
- **Card Drop Shadows** — Added `DropShadowEffect` (`Color="Black"`, `BlurRadius="10"`, `Opacity="0.2"`, `ShadowDepth="2"`, `Direction="270"`) to seven card `Border` elements for subtle depth:
  - **DashboardView** — TPS card, RAM card, Uptime card, Console card, Player List card (5 borders).
  - **AntiCheatView** — Live Alerts card, Shadowbanned Players card (2 borders).

### Batch 32 — Ngrok Support & Secure WebSockets
- **Smart URI Construction** (`Services/ShedWebSocketService.cs`) — `ConnectAsync` now detects ngrok tunnel URLs and switches to secure WebSockets:
  - If the `ip` parameter contains `"ngrok"` (case-insensitive), strips any `https://` or `http://` prefix and trailing `/`, then constructs the URI as `wss://{cleanedIp}/shedlink` (port parameter ignored entirely).
  - If the `ip` parameter does **not** contain `"ngrok"`, constructs the URI normally as `ws://{ip}:{port}/shedlink`.
  - Enables remote server management through ngrok tunnels without any additional configuration.

---

## Changelog (Batches 33–41)

### Batch 33 — Smart Parsing & Better Notifications
- **System/Audit Detection** (`ShedLinkSystem.TryBroadcastSecurityAlertAsync`) — Log lines containing `[AUDIT]` or lacking a `|` delimiter are now classified as system events: `PlayerName = "System"`, `AlertType = "Audit"`, `StrikeCount = 0`. The `[AUDIT]` tag is stripped from the details text.
- **Enhanced Notification Text** (`AntiCheatViewModel.HandleSecurityAlert`) — Desktop balloon-tip text now shows context-rich messages: `"System Audit: {details}"` for system events, `"{playerName} ({alertType}): {details}"` for player violations.
- **System Alert Badge** (`AntiCheatView.xaml`) — New `DataTrigger` on `PlayerName == "System"` applies a neutral slate-blue badge style: background `#FF2A2D3A`, text `#FF6B8ADB`. Distinguishes system audit entries from player violations.

### Batch 34 — Shed Security Command Mapping
- **Config Reload Command** (`ShedLinkSystem.HandleConfigSaveAsync`) — Changed the hot-reload command from `/shedsecurity reload` to `/shed reload` to match the Shed Security mod's actual command prefix.
- **Player Action Mapping** (`ShedLinkSystem.HandlePlayerActionAsync`) — Updated command routing for Shed Security compatibility:
  - `"Kick"` → `/kick {player}` (vanilla, unchanged).
  - `"Ban"` → `/shed shadowban {player}` (was `/ban`, now uses Shed Security's shadowban system).
  - `"Mute"` → `/shed mute {player}` (was `/players {player} mute`).
  - `"Pardon"` → `/shed clear {player}` (new action type for removing all Shed Security penalties).

### Batch 36 — Live Metric Graphs
- **LiveChartsCore** (`Shed Security AP.csproj`) — Added `LiveChartsCore.SkiaSharpView.WPF` v2.0.0-rc5.4 with `NoWarn="NU1701"` for pre-release compatibility.
- **Sparkline Data** (`DashboardViewModel`) — Two `ObservableCollection<ObservableValue>` collections (`TpsHistory`, `RamHistory`) capped at 60 data points. `HandleMetricsUpdate` parses incoming string values to doubles and appends new `ObservableValue` entries. `ParseRamToMb()` handles both `"GB"` and `"MB"` suffixes.
- **Chart Series** (`DashboardViewModel.BuildSparklineSeries`) — Creates `LineSeries<ObservableValue>` sparklines: emerald accent stroke, alpha-30 fill, no data points (`GeometrySize = 0`), 150ms animation speed.
- **Hidden Axes** — `BuildHiddenAxes()` returns `Axis[]` with `ShowSeparatorLines = false`, `TextSize = 0` for clean sparkline-only rendering.
- **DashboardView** — Replaced the TPS and RAM `ProgressBar` controls with `<lvc:CartesianChart>` elements bound to `TpsSeries`, `RamSeries`, `HiddenXAxes`, `HiddenYAxes`. Chart `Height="50"`, transparent background, no tooltip or legend.

### Batch 37 — Quick-Action Macros
- **Macro Commands** (`DashboardViewModel`) — Four new `AsyncRelayCommand` properties wired to common admin commands:
  - `TimeMorningCommand` → `/time morning`
  - `ClearWeatherCommand` → `/weather clear`
  - `SaveWorldCommand` → `/save`
  - `ClearEntitiesCommand` → `/entity removeitem drop`
- **CreateQuickAction Helper** (`DashboardViewModel`) — Shared factory method that appends `"[Quick] > {command}"` to console, sends a `"ConsoleCommand"` `NetworkMessage`, returns an `AsyncRelayCommand`.
- **QuickActionBtn Style** (`DashboardView.xaml`) — Inline `Button` style: 34×30, transparent background, `TextSecondaryBrush` foreground, `CornerRadius="4"`, hover → `#FF1C1C21`, press → `#FF33333B`.
- **Toolbar** (`DashboardView.xaml`) — Horizontal `StackPanel` of four icon buttons (🌅 `&#xE706;`, ☀ `&#xE869;`, 💾 `&#xE74E;`, 🧹 `&#xE74D;`) placed above the console `ScrollViewer` with a `Separator` below.

### Batch 38 — Detailed Player Inspect
- **Payload Models** — Four new models (client + server):
  - `PlayerInspectRequestPayload` — `string PlayerName`. Sent in `"PlayerInspectRequest"` messages.
  - `PlayerInspectResponsePayload` — `string PlayerName`, `string IpAddress`, `string Playtime`, `string Position`, `int TotalStrikes`. Sent in `"PlayerInspectResponse"` messages.
- **Server Handler** (`ShedLinkSystem.HandlePlayerInspectAsync`) — Finds the target player in `AllOnlinePlayers`. Masks IP address (first two octets visible, last two replaced with `*.*`). Reads `Entity.Pos` for position. Computes session duration from `ServerData.LastJoinDate`. Reads `shed-strikes.json` for total strike count. Returns `"Offline"` / `"--"` for disconnected players.
- **Client ViewModel** (`DashboardViewModel`) — `InspectPlayerCommand` sends the request. `HandlePlayerInspectResponse` populates a `PlayerDossier` model and sets `IsInspectModalOpen = true`.
- **Inspect Modal** (`DashboardView.xaml`) — Semi-transparent overlay with a centered dossier card. Labeled fields for Name, IP, Playtime, Position, Strikes. Click-outside-to-dismiss via `ModalOverlay_MouseDown` code-behind. Visibility controlled by `BooleanToVisibilityConverter`.
- **BooleanToVisibilityConverter** (`App.xaml`) — `x:Key="BoolToVisConverter"` resource added for modal visibility binding.

### Batch 39 — Restructured Navigation & App Settings
- **Settings Split** — The single "Settings" tab was split into two separate views:
  - **Config Editor** — Remote server config editor (formerly Settings). Renamed `SettingsViewModel` → `ConfigEditorViewModel`, `SettingsView` → `ConfigEditorView`. Icon changed to `&#xE943;` (document).
  - **App Settings** — Local desktop preferences. New `AppSettingsViewModel` and `AppSettingsView`.
- **LocalPreferences** (`Core/LocalPreferences.cs`) — Static class with `bool EnableDesktopNotifications { get; set; } = true` for app-wide preference state.
- **AppSettingsViewModel** (`ViewModels/AppSettingsViewModel.cs`) — Binds to `LocalPreferences.EnableDesktopNotifications` with two-way property change notification.
- **AppSettingsView** (`Views/AppSettingsView.xaml`) — `CardBackgroundBrush` card with "DESKTOP PREFERENCES" `LabelStyle` header (`&#xE7F4;` notification icon) and a `CheckBox` for toggling desktop notifications.
- **Navigation Updates** (`MainWindowViewModel`, `MainWindow.xaml`) — `SettingsVm` renamed to `ConfigEditorVm`. New `AppSettingsVm` property. `NavigateCommand` switch updated: `"ConfigEditor"` → `ConfigEditorVm`, `"AppSettings"` → `AppSettingsVm`. Sidebar split: "Config" button (`&#xE943;`) and "Settings" button (`&#xE713;`). Two new `DataTemplate`s for `ConfigEditorViewModel` and `AppSettingsViewModel`.
- **Notification Gating** (`AntiCheatViewModel.HandleSecurityAlert`) — Desktop notifications now gated by `LocalPreferences.EnableDesktopNotifications && !_isAppFocused()`.
- **Old Files Removed** — `SettingsViewModel.cs`, `SettingsView.xaml`, `SettingsView.xaml.cs` deleted.

### Batch 40 — Server-Side Universal Config Scanner
- **New Payloads** — `ConfigListRequestPayload` (empty) and `ConfigListResponsePayload` (`List<string> Files`) added to both client and server model folders.
- **FileName Property** — Added `string FileName` to both `ConfigRequestPayload` and `ConfigSavePayload` (client and server) to support multi-file config editing.
- **Config Scanner** (`ShedLinkSystem.HandleConfigListRequestAsync`) — Handles `"ConfigListRequest"` messages. Uses `_serverApi.GetOrCreateDataPath("ModConfig")` to locate the mod config directory, scans with `Directory.GetFiles(path, "*.json")`, extracts file names, and sends a `"ConfigListResponse"` with the list.
- **Updated Read** (`ShedLinkSystem.HandleConfigRequestAsync`) — Now deserializes `ConfigRequestPayload` from the message, sanitizes `FileName` via `SanitizeConfigFileName`, and reads the specific file from `ModConfig/`.
- **Updated Write** (`ShedLinkSystem.HandleConfigSaveAsync`) — Now sanitizes `payload.FileName` and writes to that specific file instead of hardcoded `anticheat-config.json`.
- **SanitizeConfigFileName** (`ShedLinkSystem`) — Security helper that rejects file names containing `/`, `\`, `..`, or not ending in `.json` to prevent directory-traversal attacks. Returns `null` if invalid.
- **Process Route** (`ShedLinkSystem.ProcessMessageAsync`) — New `"ConfigListRequest"` case added for authenticated clients.

### Batch 41 — Client-Side Config Dropdown UI
- **AvailableConfigs** (`ConfigEditorViewModel`) — `ObservableCollection<string>` populated from `"ConfigListResponse"` messages. Stores the list of JSON file names available on the server.
- **SelectedConfig** (`ConfigEditorViewModel`) — `string?` property. When changed to a non-null value, automatically sends a `"ConfigRequest"` with that `FileName` to fetch the file contents.
- **Auto-Fetch on Selection** — `FetchSelectedConfigAsync()` private helper sends the request and updates status. Triggered by the `SelectedConfig` setter.
- **Config List on Connect** (`ConfigEditorViewModel.Resubscribe`) — Now `async void`. Immediately sends a `"ConfigListRequest"` after resubscribing to populate the dropdown on connection.
- **HandleConfigListResponse** — Populates `AvailableConfigs`, auto-selects the first item, updates status message with file count.
- **SaveConfigCommand Updated** — Now includes `SelectedConfig` as the `FileName` in the `ConfigSavePayload`. `CanExecute` requires both non-empty `ConfigJson` and a selected config file.
- **FetchConfigCommand Removed** — Manual fetch button no longer needed since selection auto-fetches.
- **ConfigEditorView.xaml** — "Fetch Config" button replaced with a 200px `ModernComboBox` bound to `AvailableConfigs` / `SelectedConfig`. "Save Config" button retained alongside.
- **ResetState** — Now also clears `AvailableConfigs` and resets `_selectedConfig`.

### Shadowban List Fix — Server-Side Broadcasting
- **ShadowbanListPayload** (`ShedLink/src/Models/ShadowbanListPayload.cs`) — New server-side payload model: `List<string> Players`.
- **Telemetry Loop Integration** (`ShedLinkSystem.TelemetryLoopAsync`) — Every 1-second tick, the server now reads `ModData/shed-shadowbans.json` and broadcasts a `"ShadowbanListUpdate"` message to all authenticated clients. Handles both JSON array format (list of player name strings) and JSON object format (entries with `Username` property, or keys as player names). Fails silently to an empty list if the file is missing or unreadable.

### Playtime Fix — Server-Side Player Inspect
- **LastJoinDate Parsing** (`ShedLinkSystem.HandlePlayerInspectAsync`) — `IServerPlayer.ConnectionTotalTimeInSeconds` does not exist in the Vintage Story API. Replaced with parsing `target.ServerData.LastJoinDate` (a `string`) via `DateTime.TryParse` to compute session duration since the player's last join. Falls back to `"--"` if unparseable.

### Batch 42 — Mod Manager
- **Models** — `ModInfo` (`string Name`, `string Size`, `bool IsEnabled`), `ModListRequestPayload` (empty), `ModListResponsePayload` (`List<ModInfo> Mods`), `ModToggleRequestPayload` (`string FileName`). Created on both client and server sides.
- **Server Handler** (`ShedLinkSystem.HandleModListRequestAsync`) — Scans `{DataBasePath}/Mods` for `.zip` and `.disabled` files. Builds `ModListResponsePayload` with name, formatted size, and enabled state (`.zip` = enabled, `.disabled` = disabled).
- **Server Handler** (`ShedLinkSystem.HandleModToggleRequestAsync`) — Renames between `.zip` ↔ `.disabled` extensions to toggle a mod's enabled state. Sends updated mod list after toggling.
- **ModManagerViewModel** (`ViewModels/ModManagerViewModel.cs`) — `Mods` (`ObservableCollection<ModInfo>`), `ToggleModCommand` (sends `ModToggleRequest`), `RefreshModsCommand` (sends `ModListRequest`). `Resubscribe()` / `ResetState()` lifecycle.
- **ModManagerView** (`Views/ModManagerView.xaml`) — `ItemsControl` with `CheckBox` toggle per mod, column headers (ENABLED, MOD NAME, SIZE). `Tag` binding pattern for parent VM command access from `DataTemplate`.
- **Navigation** — `ModManagerVm` property on `MainWindowViewModel`. Sidebar button `&#xE74C;` ("Mods"). `DataTemplate` for `ModManagerViewModel` → `ModManagerView`.
- **Namespace Conflict Fix** — `ModInfo` fully qualified as `Models.ModInfo` in `ShedLinkSystem.cs` to avoid `CS0104` ambiguity with `Vintagestory.API.Common.ModInfo`.

### Batch 43 — Mod Uploading (WebSocket Chunking)
- **FileUploadChunkPayload** — `string FileName`, `int ChunkIndex`, `int TotalChunks`, `string Base64Data`. Created on both client and server sides.
- **Client Upload** (`ModManagerViewModel`) — `UploadModCommand` opens `OpenFileDialog` (`.zip` filter), reads the file, splits into 1 MB chunks, Base64-encodes each chunk, and sends sequential `"FileUploadChunk"` `NetworkMessage`s. `UploadProgress` (`double`, 0–100) and `IsUploading` (`bool`) properties drive a `ModernProgressBar` in the view.
- **Server Handler** (`ShedLinkSystem.HandleFileUploadChunkAsync`) — Decodes Base64, appends bytes to a `.part` temp file. On final chunk, renames `.part` to `.zip` in the Mods folder. Sends updated mod list to the client. Cleans up partial files on error.
- **ModManagerView** — "Upload Mod" `AccentButton` added to header. `ModernProgressBar` row added at the bottom, visible when `IsUploading`.

### Batch 44 — Backup Manager
- **Models** — `BackupInfo` (`string Name`, `string Size`, `string Date`), `BackupListRequestPayload` (empty), `BackupListResponsePayload` (`List<BackupInfo> Backups`), `BackupCreateRequestPayload` (empty). Created on both sides.
- **Server Handler** (`ShedLinkSystem.HandleBackupListRequestAsync`) — Scans `{DataBasePath}/Backups` for `.zip` files, sorts by `CreationTimeUtc` descending. Returns formatted size (MB/GB) and UTC date.
- **Server Handler** (`ShedLinkSystem.HandleBackupCreateRequestAsync`) — Runs `ZipFile.CreateFromDirectory()` on a `Task.Run` background thread to zip the `Saves` directory into `Backups/Backup_YYYYMMDD_HHMMSS.zip`. Broadcasts console log progress. Sends updated backup list on completion.
- **BackupManagerViewModel** (`ViewModels/BackupManagerViewModel.cs`) — `Backups` (`ObservableCollection<BackupInfo>`), `CreateBackupCommand` (sends `BackupCreateRequest`, sets `IsCreating`), `RefreshBackupsCommand`. `Resubscribe()` / `ResetState()` lifecycle.
- **BackupManagerView** (`Views/BackupManagerView.xaml`) — Backup list with columns (BACKUP NAME, SIZE, DATE), "Create New Backup" and "Refresh" `AccentButton`s. Creating-state spinner indicator.
- **Navigation** — `BackupManagerVm` property on `MainWindowViewModel`. Sidebar button `&#xE753;` ("Backups"). `DataTemplate` for `BackupManagerViewModel` → `BackupManagerView`.

### Batch 45 — Backup Downloading (WebSocket Chunking)
- **Models** — `FileDownloadRequestPayload` (`string FileName`), `FileDownloadChunkPayload` (`string FileName`, `int ChunkIndex`, `int TotalChunks`, `string Base64Data`). Created on both sides.
- **Server Handler** (`ShedLinkSystem.HandleFileDownloadRequestAsync`) — Sanitizes the requested filename with `Path.GetFileName()`. Reads the backup `.zip` via `File.ReadAllBytes` on a background thread. Splits into 1 MB chunks, Base64-encodes each, and sends sequential `"FileDownloadChunk"` messages to the requesting client.
- **Client Download** (`BackupManagerViewModel`) — `DownloadBackupCommand` opens `SaveFileDialog` (`.zip` filter), stores the save path, sends `"FileDownloadRequest"`. `HandleFileDownloadChunk` decodes Base64, appends bytes to the save file via `FileStream(Append)`, updates `DownloadProgress` (0–100%). Sets `IsDownloading = false` on final chunk.
- **BackupManagerView** — ACTIONS column added with a download button (`&#xE896;` icon) per backup row using the `Tag` binding pattern. `ModernProgressBar` row at the bottom, visible when `IsDownloading`.

---

## File Manifest

### Dashboard (WPF Client — `Shed Security AP/`)

| Path | Purpose |
|------|---------|
| `Core/ViewModelBase.cs` | INotifyPropertyChanged base class |
| `Core/RelayCommand.cs` | Synchronous ICommand |
| `Core/AsyncRelayCommand.cs` | Async ICommand |
| `Core/PasswordBoxHelper.cs` | PasswordBox MVVM attached properties |
| `Core/AutoScrollBehavior.cs` | ScrollViewer auto-scroll attached property |
| `Core/ProfileManager.cs` | Server profile persistence (JSON, %AppData%) |
| `Core/LocalPreferences.cs` | Static app-wide preferences (EnableDesktopNotifications) |
| `Themes/Colors.xaml` | Color palette, brushes, font, typography styles |
| `Models/Network/NetworkMessage.cs` | WebSocket message model (Type + JsonElement Payload) |
| `Models/Network/AuthPayload.cs` | Auth token payload model |
| `Models/Network/AuthResponsePayload.cs` | Auth response model (Success + Message) |
| `Models/Network/MetricsPayload.cs` | Telemetry snapshot model (Tps, RamUsage, Uptime) |
| `Models/Network/PlayerInfoPayload.cs` | Player entry model (Name, Ping) |
| `Models/Network/PlayerListPayload.cs` | Player list container (List\<PlayerInfoPayload\>) |
| `Models/Network/CommandPayload.cs` | Console command payload (Command) |
| `Models/Network/ConsoleLogPayload.cs` | Console log payload (Message) |
| `Models/Network/PlayerActionPayload.cs` | Player action payload (ActionType, TargetPlayer, Reason) |
| `Models/Network/SecurityAlertPayload.cs` | Anti-cheat alert payload (Timestamp, PlayerName, AlertType, Details, StrikeCount) |
| `Models/Network/ShadowbanListPayload.cs` | Shadowbanned player list payload (List\<string\>) |
| `Models/Network/ConfigRequestPayload.cs` | Config request payload (FileName) |
| `Models/Network/ConfigResponsePayload.cs` | Config response payload (JsonContent) |
| `Models/Network/ConfigSavePayload.cs` | Config save payload (FileName, JsonContent) |
| `Models/Network/ConfigListRequestPayload.cs` | Config list request payload (empty) |
| `Models/Network/ConfigListResponsePayload.cs` | Config list response payload (List\<string\> Files) |
| `Models/Network/PlayerInspectRequestPayload.cs` | Player inspect request payload (PlayerName) |
| `Models/Network/PlayerInspectResponsePayload.cs` | Player inspect response payload (PlayerName, IpAddress, Playtime, Position, TotalStrikes) |
| `Models/Network/ModInfo.cs` | Mod info model (Name, Size, IsEnabled) |
| `Models/Network/ModListRequestPayload.cs` | Mod list request payload (empty) |
| `Models/Network/ModListResponsePayload.cs` | Mod list response payload (List\<ModInfo\> Mods) |
| `Models/Network/ModToggleRequestPayload.cs` | Mod toggle request payload (FileName) |
| `Models/Network/FileUploadChunkPayload.cs` | File upload chunk payload (FileName, ChunkIndex, TotalChunks, Base64Data) |
| `Models/Network/BackupInfo.cs` | Backup info model (Name, Size, Date) |
| `Models/Network/BackupListRequestPayload.cs` | Backup list request payload (empty) |
| `Models/Network/BackupListResponsePayload.cs` | Backup list response payload (List\<BackupInfo\> Backups) |
| `Models/Network/BackupCreateRequestPayload.cs` | Backup create request payload (empty) |
| `Models/Network/FileDownloadRequestPayload.cs` | File download request payload (FileName) |
| `Models/Network/FileDownloadChunkPayload.cs` | File download chunk payload (FileName, ChunkIndex, TotalChunks, Base64Data) |
| `Models/Local/ServerProfile.cs` | Saved server profile model (Name, Ip, Port, Token) |
| `Models/Local/SecurityAlert.cs` | Anti-cheat alert model (Timestamp, PlayerName, AlertType, Details, StrikeCount) |
| `Models/Local/Waypoint.cs` | Teleport waypoint model (Name, X, Y, Z) with `ToString() => Name` |
| `Models/Local/AuditEntry.cs` | Audit log entry model |
| `Models/Network/TeleportRequestPayload.cs` | Teleport request payload (PlayerName, DestinationPlayer, X, Y, Z) |
| `Models/Network/WorldControlRequestPayload.cs` | World control request payload (Action, Value) |
| `Models/Network/WorldStatusPayload.cs` | World status payload (Season, Temperature, DaysUntilStorm, IsStormActive) |
| `Models/Network/SpawnItemPayload.cs` | Spawn/give item payload (ItemCode, Quantity, TargetPlayer) |
| `Models/Network/AccessControlListPayload.cs` | Whitelist/ban list response payload |
| `Models/Network/AccessControlActionPayload.cs` | Whitelist/ban action payload (ActionType, PlayerName) |
| `Models/Network/DiagnosticsDataPayload.cs` | Server diagnostics data payload |
| `Models/Network/InventoryRequestPayload.cs` | Player inventory request payload |
| `Models/Network/InventoryResponsePayload.cs` | Player inventory response payload |
| `Models/Network/InventorySlot.cs` | Inventory slot model |
| `Models/Network/MappedPlayer.cs` | Radar map player model |
| `Services/INotificationService.cs` | Desktop notification service interface |
| `Services/WindowsNotificationService.cs` | NotifyIcon-based desktop notification service |
| `Services/IWebSocketService.cs` | WebSocket service interface |
| `Services/ShedWebSocketService.cs` | WebSocket service implementation (ClientWebSocket, ngrok wss:// support) |
| `Services/IAuditService.cs` | Audit logging service interface |
| `Services/LocalAuditService.cs` | Local audit log service implementation |
| `ViewModels/MainWindowViewModel.cs` | Shell VM — navigation, window commands, VM lifecycle |
| `ViewModels/ConnectionViewModel.cs` | Connection form VM — fields, connect logic, saved profiles |
| `ViewModels/DashboardViewModel.cs` | Dashboard VM — metrics, sparkline charts, console, players, quick-actions, player inspect modal |
| `ViewModels/AntiCheatViewModel.cs` | Anti-Cheat VM — live alerts, shadowbanned players, pardon command, notification gating |
| `ViewModels/ConfigEditorViewModel.cs` | Config Editor VM — remote config fetch/save, file dropdown, auto-fetch on selection |
| `ViewModels/AppSettingsViewModel.cs` | App Settings VM — local preferences (desktop notifications toggle) |
| `ViewModels/ModManagerViewModel.cs` | Mod Manager VM — mod listing, toggle, file upload with chunking |
| `ViewModels/BackupManagerViewModel.cs` | Backup Manager VM — backup listing, creation, file download with chunking |
| `ViewModels/NavigationViewModel.cs` | Navigation VM — waypoint management, teleport-to-waypoint/player commands |
| `ViewModels/WorldControlViewModel.cs` | World Control VM — time set, storm control, world status display |
| `ViewModels/SpawnerViewModel.cs` | Spawner VM — give items to players |
| `ViewModels/AccessControlViewModel.cs` | Access Control VM — whitelist/ban list display and management |
| `ViewModels/DiagnosticsViewModel.cs` | Diagnostics VM — server entity stats, nuke hostiles command |
| `ViewModels/RadarViewModel.cs` | Radar VM — live player position map |
| `ViewModels/HistoryViewModel.cs` | History VM — audit log display |
| `Views/ConnectionView.xaml(.cs)` | Connection login card UI |
| `Views/DashboardView.xaml(.cs)` | Dashboard UI — sparkline charts, console with quick-action toolbar, player list with inspect modal |
| `Views/AntiCheatView.xaml(.cs)` | Anti-Cheat UI — live alerts, shadowbanned players |
| `Views/ConfigEditorView.xaml(.cs)` | Config Editor UI — JSON editor with file dropdown |
| `Views/AppSettingsView.xaml(.cs)` | App Settings UI — notification preferences card |
| `Views/ModManagerView.xaml(.cs)` | Mod Manager UI — mod list with toggle, upload button, progress bar |
| `Views/BackupManagerView.xaml(.cs)` | Backup Manager UI — backup list with download buttons, create/refresh, progress bar |
| `Views/NavigationView.xaml(.cs)` | Navigation UI — saved waypoints, teleport console (beam to waypoint/player) |
| `Views/WorldControlView.xaml(.cs)` | World Control UI — time set, storm control, season/temperature status |
| `Views/SpawnerView.xaml(.cs)` | Spawner UI — item code input, quantity, target player |
| `Views/AccessControlView.xaml(.cs)` | Access Control UI — whitelist/ban lists with add/remove actions |
| `Views/DiagnosticsView.xaml(.cs)` | Diagnostics UI — entity breakdown, nuke hostiles button |
| `Views/RadarView.xaml(.cs)` | Radar UI — live overhead player position map |
| `Views/HistoryView.xaml(.cs)` | History UI — searchable audit log |
| `Core/WaypointManager.cs` | Waypoint persistence (JSON, %AppData%/ShedLink/waypoints.json) |
| `MainWindow.xaml(.cs)` | App shell — title bar, 8-button sidebar, content host |
| `App.xaml(.cs)` | Resource merging, control styles, ThemeMode.None |
| `app.ico` | Application icon (placeholder accent-green "S" glyph) |

### Server Mod (Vintage Story — `ShedLink/`)

| Path | Purpose |
|------|---------|
| `modinfo.json` | Mod metadata (server-side only, modid: shedlink) |
| `src/ShedLinkConfig.cs` | Config model (port, security token) |
| `src/ShedLinkSystem.cs` | ModSystem — HttpListener, WebSocket accept, auth, telemetry, console, chat, player actions, server logs, Shed Security event hooking, remote config (multi-file with scanner), shadowban list broadcasting, player inspect, mod management (list/toggle/upload), backup management (list/create/download) |
| `src/ShedWebClient.cs` | Session wrapper (WebSocket, ClientIp, IsAuthenticated) |
| `src/Models/NetworkMessage.cs` | Server-side protocol message (Type + JsonElement Payload) |
| `src/Models/AuthPayload.cs` | Auth token payload model |
| `src/Models/AuthResponsePayload.cs` | Auth response model (Success + Message) |
| `src/Models/MetricsPayload.cs` | Telemetry snapshot (Tps, RamUsage, Uptime) |
| `src/Models/PlayerInfoPayload.cs` | Player entry (Name, Ping) |
| `src/Models/PlayerListPayload.cs` | Player list container (List\<PlayerInfoPayload\>) |
| `src/Models/CommandPayload.cs` | Console command payload (Command) |
| `src/Models/ConsoleLogPayload.cs` | Console log payload (Message) |
| `src/Models/PlayerActionPayload.cs` | Player action payload (ActionType, TargetPlayer, Reason) |
| `src/Models/SecurityAlertPayload.cs` | Anti-cheat alert payload (Timestamp, PlayerName, AlertType, Details, StrikeCount) |
| `src/Models/ShadowbanListPayload.cs` | Shadowban list payload (List\<string\> Players) |
| `src/Models/ConfigRequestPayload.cs` | Config request payload (FileName) |
| `src/Models/ConfigResponsePayload.cs` | Config response payload (JsonContent) |
| `src/Models/ConfigSavePayload.cs` | Config save payload (FileName, JsonContent) |
| `src/Models/ConfigListRequestPayload.cs` | Config list request payload (empty) |
| `src/Models/ConfigListResponsePayload.cs` | Config list response payload (List\<string\> Files) |
| `src/Models/PlayerInspectRequestPayload.cs` | Player inspect request payload (PlayerName) |
| `src/Models/PlayerInspectResponsePayload.cs` | Player inspect response payload (PlayerName, IpAddress, Playtime, Position, TotalStrikes) |
| `src/Models/ModInfo.cs` | Mod info model (Name, Size, IsEnabled) |
| `src/Models/ModListRequestPayload.cs` | Mod list request payload (empty) |
| `src/Models/ModListResponsePayload.cs` | Mod list response payload (List\<ModInfo\> Mods) |
| `src/Models/ModToggleRequestPayload.cs` | Mod toggle request payload (FileName) |
| `src/Models/FileUploadChunkPayload.cs` | File upload chunk payload (FileName, ChunkIndex, TotalChunks, Base64Data) |
| `src/Models/BackupInfo.cs` | Backup info model (Name, Size, Date) |
| `src/Models/BackupListRequestPayload.cs` | Backup list request payload (empty) |
| `src/Models/BackupListResponsePayload.cs` | Backup list response payload (List\<BackupInfo\> Backups) |
| `src/Models/BackupCreateRequestPayload.cs` | Backup create request payload (empty) |
| `src/Models/FileDownloadRequestPayload.cs` | File download request payload (FileName) |
| `src/Models/FileDownloadChunkPayload.cs` | File download chunk payload (FileName, ChunkIndex, TotalChunks, Base64Data) |
| `src/Models/TeleportRequestPayload.cs` | Teleport request payload (PlayerName, DestinationPlayer, X, Y, Z) |
| `src/Models/WorldControlRequestPayload.cs` | World control request payload (Action, Value) |
| `src/Models/WorldStatusPayload.cs` | World status payload (Season, Temperature, DaysUntilStorm, IsStormActive, etc.) |
| `src/Models/SpawnItemPayload.cs` | Spawn/give item payload (ItemCode, Quantity, TargetPlayer) |
| `src/Models/AccessControlListPayload.cs` | Whitelist/ban list response payload |
| `src/Models/AccessControlActionPayload.cs` | Whitelist/ban action payload (ActionType, PlayerName) |
| `src/Models/InventoryRequestPayload.cs` | Player inventory request payload |
| `src/Models/InventoryResponsePayload.cs` | Player inventory response payload |
| `src/Models/InventorySlot.cs` | Inventory slot model |

---

## Changelog (Bug Fixes & Improvements)

### Coordinate Display Fix — Spawn-Relative Player Positions
- **Telemetry Loop** (`ShedLinkSystem`) — Player X/Z coordinates sent to the dashboard are now converted from absolute engine coordinates to spawn-relative coordinates by subtracting `DefaultSpawnPosition.XYZInt.X/Z`. Matches the coordinate system shown in-game (F1 display). Includes null-safety fallback if `DefaultSpawnPosition` is not yet available.
- **Player Inspect** (`ShedLinkSystem.HandlePlayerInspectAsync`) — Position string formatted using the same spawn-relative conversion.

### Time Set Format Fix — Decimal to H:MM
- **FormatTimeSetCommand** (`ShedLinkSystem`) — The "Set Time" world control previously sent a raw decimal value (e.g. `8.5`). Now converts to `H:MM` format (e.g. `8:30`) as required by the Vintage Story `/time set` command.

### Temporal Storm Control — Direct API
- **StopTemporalStorm** (`ShedLinkSystem`) — The "Stop Storm" action previously attempted a nonexistent `/nexttempstorm cancel` command. Replaced with direct `SystemTemporalStability` API calls from VSSurvivalMod.dll to programmatically end the active storm.
- **Storm Status** (`WorldStatusPayload`) — Added `bool IsStormActive` field (both client and server models). Populated from `SystemTemporalStability.StormData.nowStormActive`. The dashboard now uses this boolean instead of inferring storm state from `DaysUntilStorm <= 0`.
- **Immediate Refresh** — After stopping a storm, the server immediately broadcasts an updated `WorldStatusUpdate` to all clients so the dashboard UI refreshes instantly.

### Whitelist & Ban List — Correct Data Sources
- **File Discovery** — Vintage Story stores whitelist/ban data in `Playerdata/playerswhitelisted.json` and `Playerdata/playersbanned.json`, not `serverconfig.json`. Rewrote `HandleAccessControlRequestAsync` to read the correct files.
- **Reflection API** (`ReadPlayerListFromConfig`) — Primary reader uses reflection on `api.Server.Config` with `.ToArray()` snapshots for thread safety. Retries on `InvalidOperationException` (collection modified during enumeration).
- **File Fallback** (`ReadPlayerListFile`) — Secondary reader parses the JSON files directly if reflection fails. Handles entries with `PlayerUID`, `PlayerName`, `UntilDate`, `Reason`, `IssuedByPlayerName`.

### Hostile Entity Removal — Pure C# API
- **DespawnHostiles Message** — Replaced the console command approach (`/entity remove`) with a dedicated `DespawnHostiles` message type.
- **DespawnHostileEntities** (`ShedLinkSystem`) — Iterates `LoadedEntities.Values.ToList()`, checks `entity.Code.Path` against hostile patterns (drifter, wolf, hyena, bear, locust, bell), calls `entity.Die()` with `EnumDespawnReason.Death` for each match. Returns a count of despawned entities.
- **JsonElement Fix** — `DespawnHostiles` and `AccessControlRequest` (Refresh) messages now initialize `Payload` with `JsonSerializer.SerializeToElement(new { })` to prevent `InvalidOperationException` from default `JsonElement` (Undefined).

### GiveItem Command Order Fix
- **Argument Reorder** — The `/giveitem` command was sending arguments as `{player} {item} {qty}`. Corrected to `{item} {qty} {player}` to match Vintage Story's expected format.

### Console Log Filter — Suppress Command Echoes
- **OnLogEntryAdded** (`ShedLinkSystem`) — Added a filter to suppress `"Handling Console Command"` log echoes from being forwarded to the dashboard. These are VS's internal command echoes that contained absolute coordinates (e.g. `/tp Suja 512046.0 ...`), causing confusion in the dashboard console.

### Teleport Coordinates — Direct Entity API
- **Bypassed `/tp` command** — `InjectConsole("/tp ...")` routes through the server console's command parser, which handles coordinates differently from in-game chat. This caused incorrect teleport destinations regardless of whether relative or absolute coords were passed.
- **Entity.TeleportTo** (`ShedLinkSystem.HandleTeleportRequestAsync`) — Now finds the player by name via `World.AllOnlinePlayers`, converts the dashboard's spawn-relative coordinates back to absolute engine coordinates using `DefaultSpawnPosition.XYZInt` (the same offset the telemetry subtracts), and calls `player.Entity.TeleportTo(new EntityPos(absX, absY, absZ))` directly.
- **Player-to-Player Teleport** — Still uses `/tp PlayerName DestPlayer` since there's no coordinate math involved.
- **Debug Logging** — Added a detailed log line showing input coords, spawn offset, and computed absolute coords for easy verification in the server console.

### Dropdown Display Fix — Custom ComboBox Template Compatibility
- **ToString Overrides** — Added `ToString() => Name` to both `Waypoint` and `ServerProfile` model classes.
- **ItemTemplate Replacement** — Replaced `DisplayMemberPath="Name"` with explicit `<ComboBox.ItemTemplate>` containing `<TextBlock Text="{Binding Name}" />` on the saved profiles ComboBox (`ConnectionView.xaml`) and the waypoints ComboBox (`NavigationView.xaml`). The custom `ModernComboBox` style (`OverridesDefaultStyle="True"`) could conflict with `DisplayMemberPath` when the selected item visual is detached from its data context, causing the type name to be displayed instead of the property value.
