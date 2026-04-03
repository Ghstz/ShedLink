namespace ShedLink.Models;

/// <summary>
/// A single player's snapshot for the dashboard roster. We send position as relative
/// coordinates (offset from world spawn) so the radar view can plot them without needing
/// to know the spawn origin. Health and satiety are included so admins can eyeball
/// who might be in trouble.
/// </summary>
public class PlayerInfoPayload
{
    public string Name { get; set; } = string.Empty;

    public int Ping { get; set; }

    public double X { get; set; }

    public double Z { get; set; }

    public float Health { get; set; }

    public float MaxHealth { get; set; }

    public float Satiety { get; set; }
}
