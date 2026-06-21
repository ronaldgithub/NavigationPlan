using System.Globalization;
using System.Windows;

namespace NavigationPlan.Views;

public partial class DepartureDialog : Window
{
    public string AirportName => NameBox.Text.Trim().ToUpper();
    public double Latitude    { get; private set; }
    public double Longitude   { get; private set; }

    public DepartureDialog(string name, double lat, double lon)
    {
        InitializeComponent();
        NameBox.Text = name;
        LatBox.Text  = lat.ToString("F6", CultureInfo.InvariantCulture);
        LonBox.Text  = lon.ToString("F6", CultureInfo.InvariantCulture);
        Loaded += (_, _) => NameBox.Focus();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show("Please enter an airport ICAO code.", "Input Required",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!double.TryParse(LatBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat)
            || lat < -90 || lat > 90)
        {
            MessageBox.Show("Latitude must be a decimal number between -90 and 90.", "Input Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!double.TryParse(LonBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon)
            || lon < -180 || lon > 180)
        {
            MessageBox.Show("Longitude must be a decimal number between -180 and 180.", "Input Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Latitude     = lat;
        Longitude    = lon;
        DialogResult = true;
    }
}
