using System.Collections.Generic;

namespace ShedLink.Models;

/// <summary>
/// Every non-empty slot from a player’s hotbar, backpack, and gear.
/// </summary>
public class InventoryResponsePayload
{
    public List<InventorySlot> Slots { get; set; } = new();
}
