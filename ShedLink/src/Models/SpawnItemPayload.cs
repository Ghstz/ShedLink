namespace ShedLink.Models;

/// <summary>
/// Dashboard tells us to drop an item into a player's inventory.
/// <c>ItemCode</c> is the full Vintage Story asset code (e.g. "game:sword-iron"),
/// and we default <c>Quantity</c> to 1 if the dashboard doesn't specify.
/// </summary>
public class SpawnItemPayload
{
    public string TargetPlayer { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}
