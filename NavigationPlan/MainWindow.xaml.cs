using System.Windows;
using System.Windows.Input;
using BruTile.Predefined;
using BruTile.Web;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;
using Mapsui.UI.Wpf;
using NetTopologySuite.Geometries;
using NavigationPlan.Models;
using NavigationPlan.Services;
using NavigationPlan.ViewModels;
using NavigationPlan.Views;

namespace NavigationPlan;

public partial class MainWindow : Window
{
    private WritableLayer _pinLayer = new();
    private WritableLayer _routeLayer = new();
    private readonly Dictionary<Waypoint, PointFeature> _pinFeatures = new();
    private MainViewModel ViewModel => (MainViewModel)DataContext;
    private AirportLookupService.AirportInfo? _foundAirport;
    private TileLayer _baseTileLayer = OpenStreetMap.CreateTileLayer();

    public MainWindow()
    {
        DataContext = new MainViewModel();
        InitializeComponent();
        // Set Home before WPF layout triggers Mapsui's SizeChanged internally
        MapControl.Map.Home = n => ZoomToNetherlands(n);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        SetupMap();
        ViewModel.Waypoints.CollectionChanged += (_, _) => RefreshMapLayers();
        // Zoom explicitly after all layers are added (overrides any world-level default)
        ZoomToNetherlands(MapControl.Map.Navigator);
        _ = LoadAirportDatabaseAsync();
    }

    private void SetupMap()
    {
        MapControl.Map.Layers.Add(_baseTileLayer);

        _routeLayer = new WritableLayer { Name = "Route" };
        _pinLayer   = new WritableLayer { Name = "Waypoints", IsMapInfoLayer = true };
        MapControl.Map.Layers.Add(_routeLayer);
        MapControl.Map.Layers.Add(_pinLayer);

        MapControl.Info += MapControl_Info;
        MapControl.MouseRightButtonUp += MapControl_MouseRightButtonUp;

        RefreshMapLayers();
    }

    private void MapTypeCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Guard: event fires during XAML init before SetupMap() adds the base layer
        if (MapControl?.Map?.Layers?.Contains(_baseTileLayer) != true) return;

        TileLayer newLayer = MapTypeCombo.SelectedIndex == 1
            ? new TileLayer(new ArcGisTileSource(
                "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer",
                new GlobalSphericalMercator(), null, null))
              { Name = "Satellite" }
            : OpenStreetMap.CreateTileLayer();

        MapControl.Map.Layers.Remove(_baseTileLayer);
        MapControl.Map.Layers.Insert(0, newLayer);
        _baseTileLayer = newLayer;
    }

    private static void ZoomToNetherlands(Mapsui.Navigator n)
    {
        var sw = SphericalMercator.FromLonLat(2.8, 50.4);
        var ne = SphericalMercator.FromLonLat(8.0, 53.8);
        n.ZoomToBox(new MRect(sw.x, sw.y, ne.x, ne.y));
    }

    private async void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Media.Imaging.BitmapSource? mapBitmap = null;

        if (PrintChartCheckBox.IsChecked == true)
        {
            var nav             = MapControl.Map.Navigator;
            var savedCenterX    = nav.Viewport.CenterX;
            var savedCenterY    = nav.Viewport.CenterY;
            var savedResolution = nav.Viewport.Resolution;

            var waypoints = ViewModel.Waypoints.ToList();
            if (waypoints.Count >= 2)
            {
                var xs = waypoints.Select(w => SphericalMercator.FromLonLat(w.Longitude, w.Latitude).x);
                var ys = waypoints.Select(w => SphericalMercator.FromLonLat(w.Longitude, w.Latitude).y);
                var box = new MRect(xs.Min(), ys.Min(), xs.Max(), ys.Max()).Grow(10000);
                nav.ZoomToBox(box);
                await Task.Delay(700);
            }

            var dpi = 96.0;
            var w   = (int)MapControl.ActualWidth;
            var h   = (int)MapControl.ActualHeight;
            if (w > 0 && h > 0)
            {
                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
                    w, h, dpi, dpi, System.Windows.Media.PixelFormats.Pbgra32);
                rtb.Render(MapControl);
                rtb.Freeze();
                mapBitmap = rtb;
            }

            nav.CenterOnAndZoomTo(new MPoint(savedCenterX, savedCenterY), savedResolution);
        }

        ViewModel.PrintWithMap(mapBitmap);
    }

    private void RtButton_Click(object sender, RoutedEventArgs e)
    {
        var result = ViewModel.BuildRtCalls();
        if (result == null) return;
        var dlg = new RtCallsDialog(result.Value.Title, result.Value.Text) { Owner = this };
        dlg.ShowDialog();
    }

    private async Task LoadAirportDatabaseAsync()
    {
        AirportResultLabel.Text = "Loading airport database…";
        try
        {
            await AirportLookupService.EnsureLoadedAsync();
            AirportResultLabel.Text = string.Empty;
        }
        catch
        {
            AirportResultLabel.Text = "Airport database unavailable (check internet connection)";
        }
    }

    private void AirportSearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) _ = FindAirportAsync();
    }

    private void FindAirportButton_Click(object sender, RoutedEventArgs e)
        => _ = FindAirportAsync();

    private async Task FindAirportAsync()
    {
        var icao = AirportSearchBox.Text.Trim();
        if (string.IsNullOrEmpty(icao)) return;

        if (!AirportLookupService.IsLoaded)
        {
            AirportResultLabel.Text = "Still loading database, please wait…";
            try { await AirportLookupService.EnsureLoadedAsync(); }
            catch
            {
                AirportResultLabel.Text = "Airport database unavailable.";
                return;
            }
        }

        var ap = AirportLookupService.Lookup(icao);
        if (ap == null)
        {
            _foundAirport = null;
            AirportResultLabel.Text = $"'{icao}' not found in database.";
            AirportResultPanel.Visibility = System.Windows.Visibility.Visible;
            SetDepFromFindButton.Visibility = System.Windows.Visibility.Collapsed;
            return;
        }

        _foundAirport = ap;
        var p = SphericalMercator.FromLonLat(ap.Lon, ap.Lat);
        MapControl.Map.Navigator.CenterOnAndZoomTo(new MPoint(p.x, p.y), 10);
        AirportResultLabel.Text = ap.Name;
        AirportResultPanel.Visibility = System.Windows.Visibility.Visible;
        SetDepFromFindButton.Visibility = System.Windows.Visibility.Visible;
    }

    private void SetDepFromFind_Click(object sender, RoutedEventArgs e)
    {
        if (_foundAirport == null) return;
        ViewModel.SetDeparture(_foundAirport.Ident, _foundAirport.Lat, _foundAirport.Lon);
        SetDepFromFindButton.Visibility = System.Windows.Visibility.Collapsed;
        AirportResultLabel.Text = $"{_foundAirport.Ident} set as departure";
    }

    private void DepAirportButton_Click(object sender, RoutedEventArgs e)
    {
        var current = ViewModel.Waypoints.FirstOrDefault(w => w.IsFixed);
        var dlg = new Views.DepartureDialog(
            current?.Name      ?? "EHTE",
            current?.Latitude  ?? 51.995833,
            current?.Longitude ?? 6.840556)
        { Owner = this };

        if (dlg.ShowDialog() != true) return;
        ViewModel.SetDeparture(dlg.AirportName, dlg.Latitude, dlg.Longitude);
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        var waypoints = ViewModel.Open();
        if (waypoints == null) return;
        ViewModel.LoadWaypoints(waypoints);
        // Map auto-refreshes via CollectionChanged; also zoom to route extent
        if (waypoints.Count >= 2)
        {
            var xs = waypoints.Select(w => SphericalMercator.FromLonLat(w.Longitude, w.Latitude).x);
            var ys = waypoints.Select(w => SphericalMercator.FromLonLat(w.Longitude, w.Latitude).y);
            var box = new MRect(xs.Min(), ys.Min(), xs.Max(), ys.Max()).Grow(50000);
            MapControl.Map.Navigator.ZoomToBox(box);
        }
    }

    private void MapControl_Info(object? sender, MapInfoEventArgs e)
    {
        // Only handle clicks on empty map (no feature hit)
        if (e.MapInfo?.Feature != null) return;
        if (e.MapInfo?.WorldPosition == null) return;

        var world  = e.MapInfo.WorldPosition;
        var lonLat = SphericalMercator.ToLonLat(world.X, world.Y);

        var dialog = new InputDialog { Owner = this };
        if (dialog.ShowDialog() != true) return;

        ViewModel.AddWaypoint(lonLat.lat, lonLat.lon, dialog.WaypointName);
    }

    private void MapControl_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var screenPos = e.GetPosition(MapControl);
        var mapInfo   = MapControl.GetMapInfo(new MPoint(screenPos.X, screenPos.Y));
        if (mapInfo?.Feature == null) return;

        var wp = mapInfo.Feature["WaypointRef"] as Waypoint;
        if (wp == null || wp.IsFixed) return;

        var result = MessageBox.Show(
            $"Remove waypoint \"{wp.Name}\"?",
            "Remove Waypoint",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
            ViewModel.RemoveWaypoint(wp);
    }

    private void RefreshMapLayers()
    {
        _pinLayer.Clear();
        _pinFeatures.Clear();

        foreach (var wp in ViewModel.Waypoints)
        {
            var p = SphericalMercator.FromLonLat(wp.Longitude, wp.Latitude);
            var feature = new PointFeature(new MPoint(p.x, p.y));
            feature["WaypointRef"] = wp;

            var fillColor = wp.IsFixed
                ? new Mapsui.Styles.Color(0, 120, 212)
                : new Mapsui.Styles.Color(220, 60, 60);

            feature.Styles.Add(new SymbolStyle
            {
                Fill       = new Mapsui.Styles.Brush(fillColor),
                Outline    = new Mapsui.Styles.Pen(Mapsui.Styles.Color.White, 1.5f),
                SymbolScale = 0.6,
                SymbolType  = SymbolType.Ellipse
            });
            feature.Styles.Add(new LabelStyle
            {
                Text = wp.Index.ToString(),
                ForeColor = Mapsui.Styles.Color.White,
                BackColor = new Mapsui.Styles.Brush(Mapsui.Styles.Color.Transparent),
                HorizontalAlignment = LabelStyle.HorizontalAlignmentEnum.Center,
                VerticalAlignment   = LabelStyle.VerticalAlignmentEnum.Center,
                Font = new Mapsui.Styles.Font { Size = 10, Bold = true }
            });
            // Name label below the pin
            feature.Styles.Add(new LabelStyle
            {
                Text = wp.Name,
                ForeColor = Mapsui.Styles.Color.White,
                BackColor = new Mapsui.Styles.Brush(new Mapsui.Styles.Color(0, 0, 0, 160)),
                HorizontalAlignment = LabelStyle.HorizontalAlignmentEnum.Center,
                VerticalAlignment   = LabelStyle.VerticalAlignmentEnum.Top,
                Offset = new Offset(0, 18),
                Font   = new Mapsui.Styles.Font { Size = 9 }
            });

            _pinFeatures[wp] = feature;
            _pinLayer.Add(feature);
        }

        _pinLayer.DataHasChanged();

        // Route polyline
        _routeLayer.Clear();
        if (ViewModel.Waypoints.Count >= 2)
        {
            var coords = ViewModel.Waypoints
                .Select(wp => SphericalMercator.FromLonLat(wp.Longitude, wp.Latitude))
                .Select(p => new Coordinate(p.x, p.y))
                .ToArray();

            var factory     = new GeometryFactory();
            var lineString  = factory.CreateLineString(coords);
            var routeFeature = new GeometryFeature(lineString);
            routeFeature.Styles.Add(new VectorStyle
            {
                Line = new Mapsui.Styles.Pen(new Mapsui.Styles.Color(0, 120, 212), 2f)
            });
            _routeLayer.Add(routeFeature);
        }

        _routeLayer.DataHasChanged();
    }
}
