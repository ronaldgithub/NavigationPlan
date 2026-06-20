namespace NavigationPlan.Models;

public class Waypoint
{
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsFixed { get; set; }
    public int Index { get; set; }
}
