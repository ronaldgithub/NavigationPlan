using System.Windows;

namespace NavigationPlan.Views;

public partial class RtCallsDialog : Window
{
    public RtCallsDialog(string title, string callsText)
    {
        InitializeComponent();
        Title = title;
        CallsTextBox.Text = callsText;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(CallsTextBox.Text);
    }
}
