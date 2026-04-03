namespace ShedLink.Models;

/// <summary>
/// Tells the dashboard whether its token was accepted.
/// If it wasn't, the socket gets closed right after this.
/// </summary>
public class AuthResponsePayload
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;
}
