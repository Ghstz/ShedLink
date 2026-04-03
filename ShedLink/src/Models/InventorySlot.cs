namespace ShedLink.Models;

/// <summary>
/// One item stack from a player's inventory — what it is, how many, and which bag it's in.
/// </summary>
public class InventorySlot
{
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Category { get; set; } = string.Empty;
}
