using System.Windows;

namespace NavigationPlan.Views;

public partial class InputDialog : Window
{
    public string WaypointName => NameBox.Text.Trim();

    public InputDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => NameBox.Focus();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show("Please enter a waypoint name.", "Input Required",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
    }
}
