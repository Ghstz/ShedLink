namespace Shed_Security_AP.Models.Network;

/// <summary>
/// The full catalog of item and block codes the server knows about.
/// We request this once on connect so the spawner dropdown has real data
/// instead of a hardcoded list that goes stale every game update.
/// </summary>
public class ItemListResponsePayload
{
    public string[] Items { get; set; } = [];
}
