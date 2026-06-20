using CommunityToolkit.Mvvm.ComponentModel;

namespace NavigationPlan.Models;

public partial class NavLeg : ObservableObject
{
    [ObservableProperty] private string waypointName = string.Empty;
    [ObservableProperty] private string level = string.Empty;
    [ObservableProperty] private int tas;
    [ObservableProperty] private int trueTrack;
    [ObservableProperty] private int trueHeading;
    [ObservableProperty] private string wca = string.Empty;
    [ObservableProperty] private int magneticHeading;
    [ObservableProperty] private int groundSpeed;
    [ObservableProperty] private double distanceNm;
    [ObservableProperty] private int timeMin;
    [ObservableProperty] private string eta = string.Empty;
    [ObservableProperty] private string ata = string.Empty;
    [ObservableProperty] private string remarks = string.Empty;
}
