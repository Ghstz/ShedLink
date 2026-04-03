namespace ShedLink.Models;

/// <summary>
/// Dashboard wants to peek at a specific player's inventory.
/// </summary>
public class InventoryRequestPayload
{
    public string PlayerName { get; set; } = string.Empty;
}
