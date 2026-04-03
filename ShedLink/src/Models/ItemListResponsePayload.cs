namespace ShedLink.Models;

/// <summary>
/// Every spawnable item/block code the game knows about, pulled straight from the registries.
/// Feeds the spawner dropdown on the dashboard.
/// </summary>
public class ItemListResponsePayload
{
    public string[] Items { get; set; } = [];
}
