using System.Windows;
namespace CortexDNA;

public partial class ConfirmationWindow : Window
{
    public ConfirmationWindow(string title, string message) { InitializeComponent(); Title = title; Heading.Text = title; Explanation.Text = message; }
    private void Confirm_Click(object sender, RoutedEventArgs args) => DialogResult = true;
}
