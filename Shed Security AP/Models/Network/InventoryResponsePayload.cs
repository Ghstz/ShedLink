using System.Collections.Generic;

namespace Shed_Security_AP.Models.Network;

public class InventoryResponsePayload
{
    public List<InventorySlot> Slots { get; set; } = new();
}
