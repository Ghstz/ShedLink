namespace Shed_Security_AP.Models.Local;

/// <summary>
/// A saved server connection — IP, port, and auth token bundled together
/// so you can switch between servers without retyping everything.
/// <see cref="ToString"/> returns the name so dropdown controls display it correctly.
/// </summary>
public class ServerProfile
{
    public string Name { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public string Port { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;

    public override string ToString() => Name;
}
