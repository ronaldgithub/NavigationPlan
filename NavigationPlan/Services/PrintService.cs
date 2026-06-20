using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NavigationPlan.Models;

namespace NavigationPlan.Services;

public static class PrintService
{
    // A4 landscape at 96 DPI: 297mm x 210mm
    private const double PageWidth  = 1122;
    private const double PageHeight = 794;
    private const double Margin     = 20;

    private static readonly Brush BlueBorder  = new SolidColorBrush(Color.FromRgb(0, 68, 170));
    private static readonly Brush BlueHeader  = new SolidColorBrush(Color.FromRgb(204, 224, 255));
    private static readonly Brush BlackText   = Brushes.Black;

    public static void Print(List<NavLeg> legs, FlightPlan plan, BitmapSource? mapImage = null)
    {
        var pd = new PrintDialog();
        // Default to landscape before showing dialog
        pd.PrintTicket.PageOrientation = PageOrientation.Landscape;
        if (pd.ShowDialog() != true) return;
        // Re-apply landscape in case the dialog changed it
        pd.PrintTicket.PageOrientation = PageOrientation.Landscape;

        var fixedDoc = new FixedDocument();

        // Page 1: nav plan form
        fixedDoc.Pages.Add(BuildFixedPage(BuildNavPage(legs, plan)));

        // Page 2: map chart (if available)
        if (mapImage != null)
            fixedDoc.Pages.Add(BuildFixedPage(BuildMapPage(mapImage, plan)));

        pd.PrintDocument(fixedDoc.DocumentPaginator, "Navigation Plan");
    }

    // ── Page builders ────────────────────────────────────────────────────────

    private static Grid BuildNavPage(List<NavLeg> legs, FlightPlan plan)
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = BuildHeader(plan);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var table = BuildNavTable(legs);
        Grid.SetRow(table, 2);
        root.Children.Add(table);

        var footer = BuildFooter();
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);

        return root;
    }

    private static Grid BuildMapPage(BitmapSource mapImage, FlightPlan plan)
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Title bar
        var title = new Border
        {
            BorderBrush = BlueBorder,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 4, 6, 4),
            Child = new TextBlock
            {
                Text = $"Navigation Chart  —  {plan.From}  →  {plan.To}" +
                       (string.IsNullOrWhiteSpace(plan.Alternate) ? "" : $"  (Alt: {plan.Alternate})") +
                       $"     Wind: {plan.WindDirection:000}° / {plan.WindSpeed} kt     QNH: {plan.Qnh} hPa",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 68, 170))
            }
        };
        Grid.SetRow(title, 0);
        root.Children.Add(title);

        // Map image stretched to fill remaining space
        var img = new Image
        {
            Source = mapImage,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment   = VerticalAlignment.Stretch
        };
        Grid.SetRow(img, 2);
        root.Children.Add(img);

        return root;
    }

    // ── Header ────────────────────────────────────────────────────────────────

    private static UIElement BuildHeader(FlightPlan plan)
    {
        // Use fixed column widths so "Flight School Teuge" is never clipped
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var col0 = MakeTextBlock($"Reg : {plan.Registration}\nType: {plan.AircraftType}\nPilot: {plan.Pilot}");
        var col1 = MakeTextBlock($"From : {plan.From}\nTo : {plan.To}\nAlternate : {plan.Alternate}");
        var col2 = MakeTextBlock("Start up : ________\nShut Down : ________\nBloc Time : ________");
        var col3 = MakeTextBlock("Take Off : ________\nLanding : ________\nFlight Time : ________");
        var col4 = new TextBlock
        {
            Text = "Flight School Teuge",
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 68, 170)),
            Margin = new Thickness(8, 2, 4, 2),
            VerticalAlignment = VerticalAlignment.Center
        };

        Grid.SetColumn(col0, 0); grid.Children.Add(col0);
        Grid.SetColumn(col1, 1); grid.Children.Add(col1);
        Grid.SetColumn(col2, 2); grid.Children.Add(col2);
        Grid.SetColumn(col3, 3); grid.Children.Add(col3);
        Grid.SetColumn(col4, 4); grid.Children.Add(col4);

        return new Border
        {
            BorderBrush = BlueBorder,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4),
            Child = grid
        };
    }

    // ── Nav table ─────────────────────────────────────────────────────────────

    private static Grid BuildNavTable(List<NavLeg> legs)
    {
        string[] headers = { "Waypoint", "Freq", "Level", "TAS", "TT", "TH", "WCA", "MH", "GS", "Dist", "Time", "ETA", "ATA", "Remarks" };
        double[]  widths  = { 115,        65,     45,      40,    40,   40,   40,    40,   40,   45,     40,     45,    45,    0 };

        var grid = new Grid();
        foreach (var w in widths)
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = w > 0 ? new GridLength(w) : new GridLength(1, GridUnitType.Star)
            });

        int totalRows = Math.Max(legs.Count, 15) + 1;
        for (int r = 0; r < totalRows; r++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (int c = 0; c < headers.Length; c++)
        {
            var cell = MakeCell(headers[c], isHeader: true);
            Grid.SetRow(cell, 0); Grid.SetColumn(cell, c);
            grid.Children.Add(cell);
        }

        for (int r = 0; r < totalRows - 1; r++)
        {
            NavLeg? leg = r < legs.Count ? legs[r] : null;
            string[] values = leg == null
                ? Enumerable.Repeat(string.Empty, headers.Length).ToArray()
                : new[]
                {
                    leg.WaypointName,
                    leg.Frequency,
                    leg.Level,
                    leg.Tas > 0 ? leg.Tas.ToString() : string.Empty,
                    r > 0 ? leg.TrueTrack.ToString("000")       : string.Empty,
                    r > 0 ? leg.TrueHeading.ToString("000")     : string.Empty,
                    r > 0 ? leg.Wca                             : string.Empty,
                    r > 0 ? leg.MagneticHeading.ToString("000") : string.Empty,
                    leg.GroundSpeed > 0 ? leg.GroundSpeed.ToString() : string.Empty,
                    r > 0 ? leg.DistanceNm.ToString("F1")       : string.Empty,
                    r > 0 ? leg.TimeMin.ToString()              : string.Empty,
                    leg.Eta, leg.Ata, leg.Remarks
                };

            for (int c = 0; c < headers.Length; c++)
            {
                var cell = MakeCell(values[c], isHeader: false);
                Grid.SetRow(cell, r + 1); Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }

        return grid;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PageContent BuildFixedPage(Grid pageGrid)
    {
        var fixedPage = new FixedPage { Width = PageWidth, Height = PageHeight, Background = Brushes.White };

        pageGrid.Width  = PageWidth  - 2 * Margin;
        pageGrid.Height = PageHeight - 2 * Margin;
        pageGrid.Measure(new Size(pageGrid.Width, pageGrid.Height));
        pageGrid.Arrange(new Rect(0, 0, pageGrid.Width, pageGrid.Height));
        pageGrid.UpdateLayout();
        FixedPage.SetLeft(pageGrid, Margin);
        FixedPage.SetTop(pageGrid, Margin);

        fixedPage.Children.Add(pageGrid);

        var pageContent = new PageContent();
        ((System.Windows.Markup.IAddChild)pageContent).AddChild(fixedPage);
        return pageContent;
    }

    private static TextBlock MakeTextBlock(string text) => new()
    {
        Text = text,
        FontSize = 11,
        Foreground = BlackText,
        Margin = new Thickness(4, 2, 4, 2),
        TextWrapping = TextWrapping.NoWrap
    };

    private static Border MakeCell(string text, bool isHeader) => new()
    {
        BorderBrush = BlueBorder,
        BorderThickness = new Thickness(0, 0, 1, 1),
        Background = isHeader ? BlueHeader : Brushes.White,
        Child = new TextBlock
        {
            Text = text,
            FontSize = 10,
            FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal,
            Foreground = BlackText,
            Margin = new Thickness(3, 2, 3, 2),
            TextWrapping = TextWrapping.NoWrap
        }
    };

    private static StackPanel BuildFooter() => new()
    {
        Margin = new Thickness(0, 4, 0, 0),
        Children =
        {
            new TextBlock { Text = "Frequencies :", FontSize = 10, Foreground = BlackText },
            new TextBlock
            {
                Text = "Versie 01 RD 07032020", FontSize = 8, Foreground = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 2, 0, 0)
            }
        }
    };
}
