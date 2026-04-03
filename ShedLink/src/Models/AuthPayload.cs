namespace ShedLink.Models;

/// <summary>
/// First thing the dashboard sends after connecting — proves it knows the shared secret.
/// </summary>
public class AuthPayload
{
    public required string Token { get; set; }
}
