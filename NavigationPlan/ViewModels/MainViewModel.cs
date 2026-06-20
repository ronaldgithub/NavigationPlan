using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NavigationPlan.Models;
using NavigationPlan.Services;

namespace NavigationPlan.ViewModels;

public partial class MainViewModel : ObservableObject
{
    // Aircraft info
    [ObservableProperty] private string registration = string.Empty;
    [ObservableProperty] private string aircraftType = string.Empty;
    [ObservableProperty] private string pilot = string.Empty;

    // Flight info
    [ObservableProperty] private string from = "EHTE";
    [ObservableProperty] private string to = string.Empty;
    [ObservableProperty] private string alternate = string.Empty;
    [ObservableProperty] private string departureTime = "10:00";

    // Wind
    [ObservableProperty] private string windDirection = "270";
    [ObservableProperty] private string windSpeed = "15";
    [ObservableProperty] private string qnh = "1013";

    // Performance
    [ObservableProperty] private string tas = "100";
    [ObservableProperty] private string level = "1000";
    [ObservableProperty] private string magneticVariation = "2.5";

    public ObservableCollection<Waypoint> Waypoints { get; } = new();
    public ObservableCollection<NavLeg> NavLegs { get; } = new();

    // Notes loaded from file; applied on next Calculate() then cleared
    private Dictionary<string, (string Frequency, string Ata, string Remarks)> _pendingNotes = new();

    public MainViewModel()
    {
        Waypoints.Add(new Waypoint
        {
            Name = "EHTE",
            Latitude = 52.2442,
            Longitude = 6.0464,
            IsFixed = true,
            Index = 1
        });
    }

    public void AddWaypoint(double lat, double lon, string name)
    {
        Waypoints.Add(new Waypoint
        {
            Name = name,
            Latitude = lat,
            Longitude = lon,
            IsFixed = false,
            Index = Waypoints.Count + 1
        });
    }

    public void RemoveWaypoint(Waypoint wp)
    {
        if (wp.IsFixed) return;
        Waypoints.Remove(wp);
        for (int i = 0; i < Waypoints.Count; i++)
            Waypoints[i].Index = i + 1;
    }

    // Called from code-behind after Open loads waypoints (map will auto-refresh via CollectionChanged)
    public void LoadWaypoints(IEnumerable<Waypoint> waypoints)
    {
        Waypoints.Clear();
        foreach (var wp in waypoints)
            Waypoints.Add(wp);
    }

    [RelayCommand]
    private void Calculate()
    {
        if (!TryBuildFlightPlan(out var plan)) return;

        // Preserve user-entered values across recalculate; merge with any notes loaded from file
        var saved = NavLegs.ToDictionary(
            l => l.WaypointName,
            l => (l.Frequency, l.Ata, l.Remarks));

        foreach (var (name, note) in _pendingNotes)
            saved[name] = note;
        _pendingNotes.Clear();

        var legs = NavigationCalculator.Calculate(Waypoints.ToList(), plan);
        NavLegs.Clear();
        foreach (var leg in legs)
        {
            if (saved.TryGetValue(leg.WaypointName, out var s))
            {
                leg.Frequency = s.Frequency;
                leg.Ata       = s.Ata;
                leg.Remarks   = s.Remarks;
            }
            NavLegs.Add(leg);
        }
    }

    [RelayCommand]
    private void Clear()
    {
        Registration = string.Empty;
        AircraftType = string.Empty;
        Pilot = string.Empty;
        From = "EHTE";
        To = string.Empty;
        Alternate = string.Empty;
        DepartureTime = "10:00";
        WindDirection = "270";
        WindSpeed = "15";
        Qnh = "1013";
        Tas = "100";
        Level = "1000";
        MagneticVariation = "2.5";

        NavLegs.Clear();
        var fixed1 = Waypoints.FirstOrDefault(w => w.IsFixed);
        Waypoints.Clear();
        if (fixed1 != null) Waypoints.Add(fixed1);
    }

    public void PrintWithMap(System.Windows.Media.Imaging.BitmapSource? mapImage)
    {
        if (!TryBuildFlightPlan(out var plan)) return;
        PrintService.Print(NavLegs.ToList(), plan, mapImage);
    }

    [RelayCommand]
    private void Save()
    {
        if (NavLegs.Count == 0)
        {
            MessageBox.Show("Calculate the navigation plan first.", "Nothing to Save",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Title = "Save Navigation Plan",
            Filter = "Navigation Plan (*.txt)|*.txt",
            FileName = $"NavPlan_{From}_{To}_{DateTime.Now:yyyyMMdd_HHmm}.txt"
        };
        if (dlg.ShowDialog() != true) return;

        var sb = new StringBuilder();

        sb.AppendLine("=================================================================");
        sb.AppendLine("                  NAVIGATION PLAN — Flight School Teuge");
        sb.AppendLine("=================================================================");
        sb.AppendLine($"  Reg      : {Registration}");
        sb.AppendLine($"  Type     : {AircraftType}");
        sb.AppendLine($"  Pilot    : {Pilot}");
        sb.AppendLine($"  From     : {From}");
        sb.AppendLine($"  To       : {To}");
        sb.AppendLine($"  Alternate: {Alternate}");
        sb.AppendLine($"  Dep.Time : {DepartureTime}");
        sb.AppendLine($"  Wind     : {WindDirection}° / {WindSpeed} kt");
        sb.AppendLine($"  QNH      : {Qnh} hPa");
        sb.AppendLine($"  TAS      : {Tas} kt");
        sb.AppendLine($"  Level    : {Level} ft");
        sb.AppendLine($"  Mag.Var  : {MagneticVariation}°E");
        sb.AppendLine("=================================================================");
        sb.AppendLine();

        // Waypoints section — needed for reopening
        sb.AppendLine("[WAYPOINTS]");
        foreach (var wp in Waypoints)
            sb.AppendLine($"{wp.Name}|{wp.Latitude.ToString("F6", CultureInfo.InvariantCulture)}|{wp.Longitude.ToString("F6", CultureInfo.InvariantCulture)}|{(wp.IsFixed ? "fixed" : "")}");
        sb.AppendLine("[/WAYPOINTS]");
        sb.AppendLine();

        // Notes (frequency, ATA, remarks per waypoint) — parsed on Open()
        sb.AppendLine("[NOTES]");
        foreach (var leg in NavLegs)
            sb.AppendLine($"{leg.WaypointName}|{leg.Frequency}|{leg.Ata}|{leg.Remarks}");
        sb.AppendLine("[/NOTES]");
        sb.AppendLine();

        // Nav plan table
        sb.AppendLine($"{"Waypoint",-14} {"Freq",8} {"Lvl",5} {"TAS",4} {"TT",4} {"TH",4} {"WCA",4} {"MH",4} {"GS",4} {"Dist",6} {"Time",5} {"ETA",5} {"ATA",5}  Remarks");
        sb.AppendLine(new string('-', 104));

        foreach (var leg in NavLegs)
        {
            int idx = NavLegs.IndexOf(leg);
            string tt   = idx > 0 ? leg.TrueTrack.ToString("000")        : "---";
            string th   = idx > 0 ? leg.TrueHeading.ToString("000")      : "---";
            string mh   = idx > 0 ? leg.MagneticHeading.ToString("000")  : "---";
            string wca  = idx > 0 ? leg.Wca                              : "---";
            string dist = idx > 0 ? leg.DistanceNm.ToString("F1")        : "---";
            string time = idx > 0 ? leg.TimeMin.ToString()               : "---";

            sb.AppendLine($"{leg.WaypointName,-14} {leg.Frequency,8} {leg.Level,5} {leg.Tas,4} {tt,4} {th,4} {wca,4} {mh,4} {leg.GroundSpeed,4} {dist,6} {time,5} {leg.Eta,5} {leg.Ata,5}  {leg.Remarks}");
        }

        sb.AppendLine(new string('-', 104));
        sb.AppendLine($"\nSaved: {DateTime.Now:yyyy-MM-dd HH:mm}");

        File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
        MessageBox.Show($"Saved to:\n{dlg.FileName}", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // Returns loaded waypoints so code-behind can restore them on the map
    public List<Waypoint>? Open()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open Navigation Plan",
            Filter = "Navigation Plan (*.txt)|*.txt"
        };
        if (dlg.ShowDialog() != true) return null;

        var lines = File.ReadAllLines(dlg.FileName, Encoding.UTF8);

        string Val(string key)
        {
            var line = lines.FirstOrDefault(l => l.TrimStart().StartsWith(key));
            return line == null ? string.Empty : line[(line.IndexOf(':') + 1)..].Trim();
        }

        Registration      = Val("Reg      :");
        AircraftType      = Val("Type     :");
        Pilot             = Val("Pilot    :");
        From              = Val("From     :");
        To                = Val("To       :");
        Alternate         = Val("Alternate:");
        DepartureTime     = Val("Dep.Time :");
        Qnh               = Val("QNH      :").Replace(" hPa", "");
        Tas               = Val("TAS      :").Replace(" kt", "");
        Level             = Val("Level    :").Replace(" ft", "");
        MagneticVariation = Val("Mag.Var  :").Replace("°E", "");

        var windRaw = Val("Wind     :"); // "270° / 15 kt"
        var windParts = windRaw.Replace("°", "").Replace("kt", "").Split('/');
        if (windParts.Length == 2)
        {
            WindDirection = windParts[0].Trim();
            WindSpeed     = windParts[1].Trim();
        }

        // Parse waypoints
        var waypoints = new List<Waypoint>();
        bool inWp = false;
        int idx = 1;
        foreach (var line in lines)
        {
            if (line.Trim() == "[WAYPOINTS]") { inWp = true; continue; }
            if (line.Trim() == "[/WAYPOINTS]") { inWp = false; continue; }
            if (!inWp) continue;

            var parts = line.Split('|');
            if (parts.Length < 3) continue;
            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double lat)) continue;
            if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double lon)) continue;

            waypoints.Add(new Waypoint
            {
                Name = parts[0],
                Latitude = lat,
                Longitude = lon,
                IsFixed = parts.Length > 3 && parts[3] == "fixed",
                Index = idx++
            });
        }

        // Parse notes section so Calculate() can restore frequency/ATA/remarks
        _pendingNotes.Clear();
        bool inNotes = false;
        foreach (var line in lines)
        {
            if (line.Trim() == "[NOTES]")  { inNotes = true;  continue; }
            if (line.Trim() == "[/NOTES]") { inNotes = false; continue; }
            if (!inNotes) continue;
            var parts = line.Split('|');
            if (parts.Length >= 4)
                _pendingNotes[parts[0]] = (parts[1], parts[2], parts[3]);
        }

        NavLegs.Clear();
        return waypoints.Count > 0 ? waypoints : null;
    }

    public (string Title, string Text)? BuildRtCalls()
    {
        if (NavLegs.Count == 0)
        {
            MessageBox.Show("Calculate the navigation plan first.", "RT Calls",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }

        var cs   = string.IsNullOrWhiteSpace(Registration) ? "[CALLSIGN]" : Registration.ToUpper();
        var type = string.IsNullOrWhiteSpace(AircraftType) ? "[TYPE]"     : AircraftType;
        var from = From.ToUpper();
        var to   = string.IsNullOrWhiteSpace(To) ? "[DEST]" : To.ToUpper();
        var legs = NavLegs.ToList();

        const string TeugeFrq    = "119.700";
        const string DutchMilFrq = "132.350";

        var sb = new StringBuilder();
        void L(string s = "")  { sb.AppendLine(s); }
        void Y(string s)       { sb.AppendLine("  YOU:  " + s); }
        void YC(string s)      { sb.AppendLine("          " + s); }
        void A(string s)       { sb.AppendLine("  ATC:  " + s); }
        void AC(string s)      { sb.AppendLine("          " + s); }

        // ── Header ──────────────────────────────────────────────────────────
        sb.AppendLine(new string('=', 62));
        sb.AppendLine($"  RT CALLS  —  {cs}  —  {from}  →  {to}");
        sb.AppendLine($"  {type}  |  Level {Level} ft  |  Wind {WindDirection}°/{WindSpeed} kt  |  QNH {Qnh}");
        sb.AppendLine(new string('=', 62));
        L();

        // ── 1. Pre-Departure ─────────────────────────────────────────────────
        sb.AppendLine($"── 1.  PRE-DEPARTURE  —  {from}  INFO  ({TeugeFrq})  " + new string('─', 10));
        L();
        Y($"Teuge Radio, {cs}, radio check on {TeugeFrq}");
        A($"{cs}, Teuge Radio, reading you 5");
        L();
        Y($"Teuge Radio, {cs}, {type}, at the apron,");
        YC($"request departure information,");
        YC($"VFR to {to}, level {Level} ft, request QNH");
        A($"{cs}, Teuge Radio, runway [XX], QNH {Qnh},");
        AC($"wind {WindDirection}°/{WindSpeed} kt, information [A/B/C],");
        AC($"report ready for departure");
        L();
        Y($"Ready for departure runway [XX], {cs}");
        A($"{cs}, cleared take-off runway [XX], wind {WindDirection}°/{WindSpeed} kt");
        L();
        Y($"Teuge Radio, {cs}, airborne, changing to Dutch Mil Info");
        A($"{cs}, Teuge Radio, frequency change approved, good day");
        L();

        // ── 2. En Route — Dutch Mil Info ─────────────────────────────────────
        sb.AppendLine($"── 2.  EN ROUTE  —  DUTCH MIL INFO  ({DutchMilFrq})  " + new string('─', 8));
        L();

        Y($"Dutch Mil Info, {cs}, {type},");
        YC($"departed {from} at {legs[0].Eta}, destination {to},");
        YC($"level {Level} ft QNH {Qnh}, VFR,");
        YC($"estimating {legs[1].WaypointName} at {legs[1].Eta}");
        A($"{cs}, Dutch Mil Info, pass your message");
        L();

        // Position reports at intermediate waypoints
        for (int i = 1; i < legs.Count - 1; i++)
        {
            var wp   = legs[i].WaypointName;
            var eta  = legs[i].Eta;
            bool last = (i == legs.Count - 2);

            if (last)
            {
                Y($"Dutch Mil Info, {cs},");
                YC($"overhead {wp} at {eta},");
                YC($"approaching {to}, changing to {to} frequency");
                A($"{cs}, Dutch Mil Info, freecall, good day");
            }
            else
            {
                var nextWp  = legs[i + 1].WaypointName;
                var nextEta = legs[i + 1].Eta;
                Y($"Dutch Mil Info, {cs},");
                YC($"overhead {wp} at {eta}, level {Level} ft,");
                YC($"estimating {nextWp} at {nextEta}");
                A($"{cs}, Dutch Mil Info, roger");
            }
            L();
        }

        // No intermediate waypoints — just the freq change
        if (legs.Count == 2)
        {
            Y($"Dutch Mil Info, {cs}, approaching {to}, changing frequency");
            A($"{cs}, Dutch Mil Info, freecall, good day");
            L();
        }

        // ── 3. Arrival ───────────────────────────────────────────────────────
        sb.AppendLine($"── 3.  ARRIVAL  —  {to} RADIO  " + new string('─', 22));
        L();

        var prevWp = legs[^2].WaypointName;
        Y($"{to} Radio, {cs}, {type},");
        YC($"{prevWp}, inbound, {Level} ft,");
        YC($"request airfield information and QNH");
        A($"{cs}, {to} Radio, runway [XX], QNH [QNH],");
        AC($"wind [DIR]/[SPD], join [circuit leg]");
        L();
        Y($"Joining [circuit leg], {cs}");
        A($"{cs}, roger");
        L();
        Y($"Finals runway [XX], {cs}");
        A($"{cs}, cleared to land runway [XX], wind [DIR]/[SPD]");
        L();
        Y($"Clear of the runway, {cs}");
        A($"{cs}, taxi to [parking], good day");
        L();

        sb.AppendLine(new string('─', 62));
        sb.AppendLine($"  [XX] = active runway at time of flight");
        sb.AppendLine($"  Teuge Info: {TeugeFrq}  |  Dutch Mil Info North: {DutchMilFrq}");
        sb.AppendLine(new string('─', 62));

        var title = $"RT Calls — {cs} — {from} → {to}";
        return (title, sb.ToString());
    }

    private bool TryBuildFlightPlan(out FlightPlan plan)
    {
        plan = new FlightPlan();

        if (!int.TryParse(WindDirection, out int wd) || wd < 0 || wd > 360)
        {
            MessageBox.Show("Wind direction must be 0–360°.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (!int.TryParse(WindSpeed, out int ws) || ws < 0)
        {
            MessageBox.Show("Wind speed must be a positive number.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (!int.TryParse(Qnh, out int qnhVal) || qnhVal < 800 || qnhVal > 1100)
        {
            MessageBox.Show("QNH must be between 800 and 1100 hPa.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (!int.TryParse(Tas, out int tasVal) || tasVal <= 0)
        {
            MessageBox.Show("TAS must be a positive number.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (!double.TryParse(MagneticVariation, NumberStyles.Float,
                CultureInfo.InvariantCulture, out double varVal))
        {
            MessageBox.Show("Magnetic variation must be a number.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (!TimeOnly.TryParse(DepartureTime, out var depTime))
        {
            MessageBox.Show("Departure time must be in HH:mm format.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        plan.Registration = Registration;
        plan.AircraftType = AircraftType;
        plan.Pilot = Pilot;
        plan.From = From;
        plan.To = To;
        plan.Alternate = Alternate;
        plan.DepartureTime = depTime;
        plan.WindDirection = wd;
        plan.WindSpeed = ws;
        plan.Qnh = qnhVal;
        plan.TAS = tasVal;
        plan.Level = Level;
        plan.MagneticVariation = varVal;

        if (Waypoints.Count < 2)
        {
            MessageBox.Show("Add at least one destination waypoint on the map.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return true;
    }
}
