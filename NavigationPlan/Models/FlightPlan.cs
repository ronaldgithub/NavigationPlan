namespace NavigationPlan.Models;

public class FlightPlan
{
    public string Registration { get; set; } = string.Empty;
    public string AircraftType { get; set; } = string.Empty;
    public string Pilot { get; set; } = string.Empty;

    public string From { get; set; } = "EHTE";
    public string To { get; set; } = string.Empty;
    public string Alternate { get; set; } = string.Empty;
    public TimeOnly DepartureTime { get; set; } = new TimeOnly(10, 0);

    public int WindDirection { get; set; }
    public int WindSpeed     { get; set; }
    public int Qnh           { get; set; } = 1013;

    public int TAS { get; set; } = 100;
    public string Level { get; set; } = "1000";
    public double MagneticVariation { get; set; } = 2.5;
}
