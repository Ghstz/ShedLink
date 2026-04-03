namespace Shed_Security_AP.Models.Network;

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
