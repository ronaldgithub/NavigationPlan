# NavigationPlan — Claude Code Context

## Project
WPF .NET 8 desktop app for VFR flight navigation planning. Built for a student pilot at Flight School Teuge (EHTE, Teuge, Netherlands).

## Tech Stack
- **WPF .NET 8** (`net8.0-windows`, `<UseWPF>true</UseWPF>`)
- **Mapsui.Wpf 4.1.9** — interactive OpenStreetMap / ESRI satellite map
- **CommunityToolkit.Mvvm 8.4.0** — MVVM (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`)
- **NetTopologySuite** — route polyline geometry (transitive Mapsui dependency)
- **BruTile 5.0.6** — tile source abstraction (transitive Mapsui dependency); `ArcGisTileSource` used for satellite view

## Project Structure
```
NavigationPlan/
├── NavigationPlan.sln
├── make_icon.ps1                # PowerShell script that generates NavigationPlan.ico
└── NavigationPlan/
    ├── NavigationPlan.ico       # App icon (compass rose + airplane, multi-size)
    ├── Models/
    │   ├── Waypoint.cs          # lat/lon, name, IsFixed (departure), IsDestination, Index
    │   ├── NavLeg.cs            # one row in the nav plan table (ObservableObject); IsBuffer flags synthetic rows
    │   └── FlightPlan.cs        # aircraft + flight + wind + QNH data container
    ├── Services/
    │   ├── NavigationCalculator.cs   # all aviation math (static, pure)
    │   ├── AirportLookupService.cs   # downloads/caches OurAirports CSV, ICAO lookup
    │   └── PrintService.cs           # WPF FixedDocument print (A4 landscape)
    ├── ViewModels/
    │   └── MainViewModel.cs     # all state, commands, save/open/RT logic
    ├── Views/
    │   ├── InputDialog.xaml     # waypoint name prompt dialog
    │   ├── DepartureDialog.xaml # set departure airport name + lat/lon
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

- **Left panel** (~280 px): Aircraft, Flight, Wind & Pressure, Performance input groups; Find airport search bar; Map type selector; action buttons
- **Right panel** (fills rest): Mapsui `MapControl` with OpenStreetMap or ESRI satellite tiles
- **Bottom panel** (250 px, full width): `DataGrid` with nav plan legs

## Nav Plan Table Columns
`Waypoint | Freq | Level | TAS | TT | WCA | TH | VAR | MH | DEV | CH | GS | Dist | Time | ETA | ATA | Remarks`

- **Freq** — radio frequency per waypoint (editable)
- **VAR** — magnetic variation applied to that leg (from Aircraft Performance input, default 2.5°E; positive = East)
- **DEV** — compass deviation applied to that leg (from Aircraft Performance input, default 0°; positive = East)
- **CH** (Compass Heading) — `MH − Deviation`; the heading the pilot actually flies. With the default 0° deviation, CH = MH
- **ATA** and **Remarks** are user-editable in-flight
- Freq, ATA, Remarks are preserved across Calculate and saved/restored via `[NOTES]` section in the `.txt` file

### Buffer rows

`NavigationCalculator.Calculate` always inserts two synthetic 5-minute rows (no distance/heading of their own, `NavLeg.IsBuffer = true`) as soon as there are ≥2 waypoints:

- **"First Waypoint"** — right after the departure row; accounts for climb-out/orientation time before tracking the first leg
- **"Entering Circuit"** — right before the final (destination) row; accounts for circuit-joining time before landing

Both simply add 5 minutes to the running `cumulativeMinutes`, pushing every subsequent ETA back. `MainViewModel.BuildRtCalls` filters out `IsBuffer` rows (`NavLegs.Where(l => !l.IsBuffer)`) since they aren't real position-report waypoints.

## Key Calculations (`NavigationCalculator.cs`)

- **TT** (True Track): forward azimuth via `atan2`
- **Dist**: Haversine in nautical miles
- **WCA** (Wind Correction Angle): `asin(WS/TAS * sin(windDir − TT))`, where `windDir` is the direction the wind blows FROM
- **GS** (Ground Speed): `TAS * cos(WCA) − WS * cos(windDir − TT)`
- **TH** = TT + WCA, **MH** = TH − magnetic variation
- **Time**: `(Dist / GS) * 60` rounded to whole minutes
- **ETA**: departure time + cumulative leg times (`TimeOnly.AddMinutes`)
- Magnetic variation default: **2.5° East** (Netherlands)

## Map Interaction (`MainWindow.xaml.cs`)

- Default view: Netherlands bounding box (zoomed on first `SizeChanged`)
- Departure pin is fixed (blue, cannot be removed); default is **EHTE** Teuge Airport (ARP N52°14′41″ E006°02′48″)
- Destination pin is fixed (green, cannot be removed) once set; not present by default — set via Find → Set as Dest. or Dest. Airport… button
- **Left-click** empty map → `InputDialog` → adds numbered waypoint pin (red), inserted just before the destination pin if one exists
- **Right-click** pin → confirm → removes waypoint (departure and destination pins protected)
- Route drawn as blue polyline via `GeometryFeature` + `VectorStyle`
- **Find airport** search bar — type ICAO, press Enter → map zooms to airport; "Set as Dep." / "Set as Dest." buttons set it as the fixed departure/destination
- **Map type** ComboBox — Street (OpenStreetMap) or Satellite (ESRI World Imagery via `ArcGisTileSource`)
- **Dep. Airport…** / **Dest. Airport…** buttons → `DepartureDialog` (shared, parameterized `title`) to manually set name + coordinates

## Departure & Destination Airports

- Departure default: **EHTE** (Teuge, Netherlands) at 52.244722°N, 6.046667°E
- Destination has no default; must be set explicitly so the nav plan always ends on a real airport instead of the last clicked pin
- Changeable at runtime via: Find → Set as Dep./Set as Dest., or the Dep./Dest. Airport… buttons (manual lat/lon entry)
- `MainViewModel.SetDeparture(name, lat, lon)` replaces `Waypoints[0]` (`IsFixed`); `SetDestination(name, lat, lon)` replaces/appends the `IsDestination` waypoint, always kept last via `AddWaypoint` inserting new pins before it; `From`/`To` sync automatically
- Clear command preserves both the current departure and destination airports

## Default Form Values (`MainViewModel.cs`)

Pre-filled on startup and restored by the **Clear** command:

- Reg: **PH-RGJ**, Type: **Sonaca**, Pilot: **R. de Groot**
- From: **EHTE**, Dep. Time: **10:00**
- Wind: **270°/15 kt**, QNH: **1013 hPa**
- TAS: **90 kt**, Level: **1500 ft**, Var: **2.5°E**, Dev: **0°**

## Airport Lookup (`AirportLookupService.cs`)

- On first run: downloads `airports.csv` (~6 MB, ~85 000 airports) from OurAirports and caches to `%LOCALAPPDATA%\NavigationPlan\airports.csv`
- Subsequent launches load from cache (fast)
- `Lookup(icao)` returns `AirportInfo(Ident, Name, Lat, Lon)` by dictionary lookup
- Download starts in background on `Window_Loaded`; status shown in result label below the Find box

## Save / Open

- **Save .txt**: saves form fields, waypoints (with coordinates, `fixed`/`destination`/plain flag), `[NOTES]` section (Freq/ATA/Remarks per waypoint), and the full nav plan table
- **Open…**: parses a saved `.txt` and restores all fields + waypoints + notes on the map
- Notes are stored in `_pendingNotes` (dict populated by `Open()`, consumed by `Calculate()`)
- Saved files (`NavPlan_*.txt`) are excluded from git via `.gitignore`

## Print

- **Page 1 — Nav plan form**: `PrintService.Print` builds a WPF `FixedDocument` (A4 landscape, 1122×794 px) replicating the NavPlan.jpg form layout
- **Page 2 — Navigation chart** (optional, toggled by "Print chart" checkbox): auto-zooms map to fit all waypoints (`MRect.Grow(10000)`), captures via `RenderTargetBitmap`, then restores original viewport. Title includes Wind and QNH.
- Mapsui viewport save/restore uses `nav.Viewport.CenterX/CenterY/Resolution` and `nav.CenterOnAndZoomTo()`

## RT Calls (`MainViewModel.BuildRtCalls`)

Generates a full ATC radio telephony script for the flight, structured in three sections:

1. **Pre-Departure** — `{from}` Radio (`{depFrq}`): radio check, departure info request, take-off clearance, frequency change. `depFrq` comes from `NavLegs[0].Frequency`; defaults to `[DEP FREQ]` if not set.
2. **En Route** — Dutch Mil Info (132.350): initial contact with ETA, position reports at each intermediate waypoint, freq change approaching destination
3. **Arrival** — destination radio: inbound call, circuit join, finals, vacated

Displayed in `RtCallsDialog` (scrollable, monospace, Copy to Clipboard button). Called from `RtButton_Click` in code-behind.

## App Icon (`make_icon.ps1`)

- Generated by `make_icon.ps1` (PowerShell + `System.Drawing`)
- Design: dark navy gradient circle, gold outer ring, 8-pointed compass rose (white N/S/E/W + gold diagonals), gold north-hat triangle, white top-down airplane silhouette, gold center dot
- Sizes: 16, 24, 32, 48, 256 px (PNG-compressed within ICO)
- Set as `ApplicationIcon` in `.csproj` and `Icon="NavigationPlan.ico"` in `MainWindow.xaml`

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
- Satellite layer: `new TileLayer(new ArcGisTileSource(url, new GlobalSphericalMercator(), null, null))`
- `MapControl.Map.Layers[0]` is the base tile layer; swap it by `Remove` + `Insert(0, newLayer)`
- `SelectionChanged` on a ComboBox fires during XAML init before the map is ready — guard with `MapControl?.Map?.Layers?.Contains(_baseTileLayer) != true`
