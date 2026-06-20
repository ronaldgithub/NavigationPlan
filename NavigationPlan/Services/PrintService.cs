using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using NavigationPlan.Models;

namespace NavigationPlan.Services;

public static class PrintService
{
    // A4 landscape at 96 DPI: 297mm x 210mm
    private const double PageWidth = 1122;
    private const double PageHeight = 794;
    private const double Margin = 20;

    private static readonly Brush BorderBrush = new SolidColorBrush(Color.FromRgb(0, 68, 170));
    private static readonly Brush HeaderBrush = new SolidColorBrush(Color.FromRgb(204, 224, 255));
    private static readonly Brush TextBrush = Brushes.Black;

    public static void Print(List<NavLeg> legs, FlightPlan plan)
    {
        var pd = new PrintDialog();
        if (pd.ShowDialog() != true) return;

        var fixedDoc = new FixedDocument();
        var pageContent = new PageContent();
        var fixedPage = new FixedPage { Width = PageWidth, Height = PageHeight, Background = Brushes.White };

        var content = BuildPage(legs, plan);
        content.Width  = PageWidth  - 2 * Margin;
        content.Height = PageHeight - 2 * Margin;
        // Must measure and arrange before adding to FixedPage
        content.Measure(new System.Windows.Size(content.Width, content.Height));
        content.Arrange(new System.Windows.Rect(0, 0, content.Width, content.Height));
        content.UpdateLayout();
        FixedPage.SetLeft(content, Margin);
        FixedPage.SetTop(content, Margin);

        fixedPage.Children.Add(content);
        ((System.Windows.Markup.IAddChild)pageContent).AddChild(fixedPage);
        fixedDoc.Pages.Add(pageContent);

        pd.PrintDocument(fixedDoc.DocumentPaginator, "Navigation Plan");
    }

    private static Grid BuildPage(List<NavLeg> legs, FlightPlan plan)
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Header
        var header = BuildHeader(plan);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        // Nav table
        var table = BuildNavTable(legs, plan);
        Grid.SetRow(table, 2);
        root.Children.Add(table);

        // Footer
        var footer = BuildFooter();
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);

        return root;
    }

    private static Grid BuildHeader(FlightPlan plan)
    {
        var grid = new Grid();
        for (int i = 0; i < 5; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = i < 4 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });

        // Col 0: Reg/Type/Pilot
        var col0 = MakeTextBlock($"Reg : {plan.Registration}\nType: {plan.AircraftType}\nPilot: {plan.Pilot}");
        Grid.SetColumn(col0, 0);
        grid.Children.Add(col0);

        // Col 1: From/To/Alternate
        var col1 = MakeTextBlock($"From : {plan.From}\nTo : {plan.To}\nAlternate : {plan.Alternate}");
        Grid.SetColumn(col1, 1);
        grid.Children.Add(col1);

        // Col 2: Start up / Shut Down / Bloc Time
        var col2 = MakeTextBlock("Start up : ________\nShut Down : ________\nBloc Time : ________");
        Grid.SetColumn(col2, 2);
        grid.Children.Add(col2);

        // Col 3: Take Off / Landing / Flight Time
        var col3 = MakeTextBlock("Take Off : ________\nLanding : ________\nFlight Time : ________");
        Grid.SetColumn(col3, 3);
        grid.Children.Add(col3);

        // Col 4: Branding
        var branding = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
        branding.Children.Add(new TextBlock
        {
            Text = "Flight School Teuge",
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 68, 170))
        });
        Grid.SetColumn(branding, 4);
        grid.Children.Add(branding);

        // Border around whole header
        var border = new Border
        {
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1),
            Child = grid,
            Padding = new Thickness(4)
        };

        var wrapper = new Grid();
        wrapper.Children.Add(border);
        return wrapper;
    }

    private static TextBlock MakeTextBlock(string text) => new()
    {
        Text = text,
        FontSize = 9,
        Foreground = TextBrush,
        Margin = new Thickness(4, 2, 4, 2),
        TextWrapping = TextWrapping.NoWrap
    };

    private static Grid BuildNavTable(List<NavLeg> legs, FlightPlan plan)
    {
        string[] headers = { "Waypoint", "Level", "TAS", "TT", "TH", "WCA", "MH", "GS", "Dist", "Time", "ETA", "ATA", "Remarks" };
        double[] widths = { 130, 45, 40, 40, 40, 45, 40, 40, 45, 40, 45, 45, 0 }; // 0 = star

        var grid = new Grid();
        foreach (var w in widths)
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = w > 0 ? new GridLength(w) : new GridLength(1, GridUnitType.Star)
            });

        int totalRows = Math.Max(legs.Count, 15) + 1; // +1 for header
        for (int r = 0; r < totalRows; r++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Header row
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = MakeCell(headers[c], isHeader: true);
            Grid.SetRow(cell, 0);
            Grid.SetColumn(cell, c);
            grid.Children.Add(cell);
        }

        // Data rows
        for (int r = 0; r < totalRows - 1; r++)
        {
            NavLeg? leg = r < legs.Count ? legs[r] : null;
            string[] values = leg == null
                ? Enumerable.Repeat(string.Empty, headers.Length).ToArray()
                : new[]
                {
                    leg.WaypointName,
                    leg.Level,
                    leg.Tas > 0 ? leg.Tas.ToString() : string.Empty,
                    r > 0 ? leg.TrueTrack.ToString("000") : string.Empty,
                    r > 0 ? leg.TrueHeading.ToString("000") : string.Empty,
                    r > 0 ? leg.Wca : string.Empty,
                    r > 0 ? leg.MagneticHeading.ToString("000") : string.Empty,
                    leg.GroundSpeed > 0 ? leg.GroundSpeed.ToString() : string.Empty,
                    r > 0 ? leg.DistanceNm.ToString("F1") : string.Empty,
                    r > 0 ? leg.TimeMin.ToString() : string.Empty,
                    leg.Eta,
                    leg.Ata,
                    leg.Remarks
                };

            for (int c = 0; c < headers.Length; c++)
            {
                var cell = MakeCell(values[c], isHeader: false);
                Grid.SetRow(cell, r + 1);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }

        return grid;
    }

    private static Border MakeCell(string text, bool isHeader)
    {
        return new Border
        {
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(0, 0, 1, 1),
            Background = isHeader ? HeaderBrush : Brushes.White,
            Child = new TextBlock
            {
                Text = text,
                FontSize = 8,
                FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal,
                Foreground = TextBrush,
                Margin = new Thickness(2, 1, 2, 1),
                TextWrapping = TextWrapping.NoWrap
            }
        };
    }

    private static StackPanel BuildFooter()
    {
        return new StackPanel
        {
            Margin = new Thickness(0, 4, 0, 0),
            Children =
            {
                new TextBlock { Text = "Frequencies :", FontSize = 9, Foreground = TextBrush },
                new TextBlock { Text = "Versie 01 RD 07032020", FontSize = 7, Foreground = Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 2, 0, 0) }
            }
        };
    }
}
