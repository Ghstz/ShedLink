namespace ShedLink;

/// <summary>
/// Lives in <c>config/shedlink.json</c>. The server generates defaults on first run
/// — you'll want to change the security token right away.
/// </summary>
public class ShedLinkConfig
{
    /// <summary>
    /// Port the WebSocket listener binds to. Make sure the dashboard is pointed at the same one.
    /// </summary>
    public int DashboardPort { get; set; } = 42420;

    /// <summary>
    /// Shared secret the dashboard sends during handshake. If this is still
    /// "CHANGE_ME" the server will yell at you in the log on every boot.
    /// </summary>
    public string SecurityToken { get; set; } = "CHANGE_ME";
}
