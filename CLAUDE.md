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
    │   └── MainViewModel.cs     # all state, commands, save/open/RT logic
    ├── Views/
    │   ├── InputDialog.xaml     # waypoint name prompt dialog
    │   └── RtCallsDialog.xaml   # scrollable RT calls display dialog
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
`Waypoint | Freq | Level | TAS | TT | TH | WCA | MH | GS | Dist | Time | ETA | ATA | Remarks`

- **Freq** — radio frequency per waypoint (editable)
- **ATA** and **Remarks** are user-editable in-flight
- Freq, ATA, Remarks are preserved across Calculate and saved/restored via `[NOTES]` section in the `.txt` file

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
- **Save .txt**: saves form fields, waypoints (with coordinates), `[NOTES]` section (Freq/ATA/Remarks per waypoint), and the full nav plan table
- **Open…**: parses a saved `.txt` and restores all fields + waypoints + notes on the map
- Notes are stored in `_pendingNotes` (dict populated by `Open()`, consumed by `Calculate()`)
- Saved files (`NavPlan_*.txt`) are excluded from git via `.gitignore`

## Print
- **Page 1 — Nav plan form**: `PrintService.Print` builds a WPF `FixedDocument` (A4 landscape, 1122×794 px) replicating the NavPlan.jpg form layout
- **Page 2 — Navigation chart** (optional, toggled by "Print chart" checkbox): auto-zooms map to fit all waypoints (`MRect.Grow(10000)`), captures via `RenderTargetBitmap`, then restores original viewport. Title includes Wind and QNH.
- Mapsui viewport save/restore uses `nav.Viewport.CenterX/CenterY/Resolution` and `nav.CenterOnAndZoomTo()`

## RT Calls (`MainViewModel.BuildRtCalls`)
Generates a full ATC radio telephony script for the flight, structured in three sections:
1. **Pre-Departure** — Teuge Info (119.700): radio check, departure info request, take-off clearance, frequency change
2. **En Route** — Dutch Mil Info (132.350): initial contact with ETA, position reports at each intermediate waypoint, freq change approaching destination
3. **Arrival** — destination radio: inbound call, circuit join, finals, vacated

Displayed in `RtCallsDialog` (scrollable, monospace, Copy to Clipboard button). Called from `RtButton_Click` in code-behind.

## Build & Run
```
cd NavigationPlan
dotnet run
```
Or open `NavigationPlan.slnx` in Visual Studio 2022+.

## Mapsui 4.1.9 API Notes
- Viewport properties: `CenterX`, `CenterY`, `Resolution`, `Rotation`, `Width`, `Height` (no `.Center` MPoint)
- Navigator: `CenterOnAndZoomTo(MPoint, double)`, `ZoomToBox(MRect)` — no `NavigateTo`
- `MRect.Grow(n)` expands by `n` metres on all four sides
- `RenderTargetBitmap` captures at WPF logical pixels (96 DPI), not physical pixels — true 1:1 map capture is not achievable
