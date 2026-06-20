# NavigationPlan — Claude Code Context

## Project
WPF .NET 8 desktop app for VFR flight navigation planning. Built for a student pilot at Flight School Teuge (EHTE, Teuge, Netherlands).

## Tech Stack
- **WPF .NET 8** (`net8.0-windows`, `<UseWPF>true</UseWPF>`)
- **Mapsui.Wpf 4.1.9** — interactive OpenStreetMap map
- **CommunityToolkit.Mvvm 8.4.0** — MVVM (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`)
- **NetTopologySuite** — route polyline geometry (transitive Mapsui dependency)

## Project Structure
```
NavigationPlan/
├── NavigationPlan.sln
└── NavigationPlan/
    ├── Models/
    │   ├── Waypoint.cs          # lat/lon, name, IsFixed, Index
    │   ├── NavLeg.cs            # one row in the nav plan table (ObservableObject)
    │   └── FlightPlan.cs        # aircraft + flight + wind + QNH data container
    ├── Services/
    │   ├── NavigationCalculator.cs  # all aviation math (static, pure)
    │   └── PrintService.cs          # WPF FixedDocument print (A4 landscape)
    ├── ViewModels/
    │   └── MainViewModel.cs     # all state, commands, save/open logic
    ├── Views/
    │   └── InputDialog.xaml     # waypoint name prompt dialog
    ├── Themes/
    │   └── DarkTheme.xaml       # dark colour palette ResourceDictionary
    ├── MainWindow.xaml          # 3-panel layout (left inputs | map | bottom DataGrid)
    ├── MainWindow.xaml.cs       # Mapsui map lifecycle, click/right-click handlers
    ├── App.xaml                 # merges DarkTheme.xaml
    └── pic/
        └── NavPlan.jpg          # reference scan of the Flight School Teuge paper form
```

## Layout
- **Left panel** (~280 px): Aircraft, Flight, Wind & Pressure, Performance input groups + action buttons
- **Right panel** (fills rest): Mapsui `MapControl` with OpenStreetMap tiles
- **Bottom panel** (250 px, full width): `DataGrid` with nav plan legs

## Nav Plan Table Columns
`Waypoint | Level | TAS | TT | TH | WCA | MH | GS | Dist | Time | ETA | ATA | Remarks`

ATA and Remarks are user-editable in-flight.

## Key Calculations (`NavigationCalculator.cs`)
- **TT** (True Track): forward azimuth via `atan2`
- **Dist**: Haversine in nautical miles
- **WCA** (Wind Correction Angle): `asin(WS/TAS * sin(windTrack − TT))`
- **GS** (Ground Speed): `TAS * cos(WCA) + WS * cos(angle)`
- **TH** = TT + WCA, **MH** = TH − magnetic variation
- **Time**: `(Dist / GS) * 60` rounded to whole minutes
- **ETA**: departure time + cumulative leg times (`TimeOnly.AddMinutes`)
- Magnetic variation default: **2.5° East** (Netherlands)

## Map Interaction (`MainWindow.xaml.cs`)
- Default view: Netherlands bounding box (zoomed on first `SizeChanged`)
- EHTE Teuge start pin is fixed (blue, cannot be removed)
- **Left-click** empty map → `InputDialog` → adds numbered waypoint pin (red)
- **Right-click** pin → confirm → removes waypoint (EHTE protected)
- Route drawn as blue polyline via `GeometryFeature` + `VectorStyle`

## Save / Open
- **Save .txt**: saves form fields, waypoints (with coordinates), and the full nav plan table
- **Open…**: parses a saved `.txt` and restores all fields + waypoints on the map
- Saved files (`NavPlan_*.txt`) are excluded from git via `.gitignore`

## Print
`PrintService.Print` builds a WPF `FixedDocument` (A4 landscape, 1122×794 px) replicating the NavPlan.jpg form layout. Elements are measured and arranged before printing.

## Build & Run
```
cd NavigationPlan
dotnet run
```
Or open `NavigationPlan.slnx` in Visual Studio 2022+.
