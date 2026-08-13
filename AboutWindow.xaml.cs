using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace CortexDNA
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            LoadVersionInfo();
        }

        private void LoadVersionInfo()
        {
            try
            {
                var assembly = Assembly.GetEntryAssembly();
                var fullVersion = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "v2.0.0";

                // Split at the plus sign to separate the master version from the git commit hash
                if (fullVersion.Contains('+'))
                {
                    var parts = fullVersion.Split('+');
                    TxtMainVersion.Text = $"Version: {parts[0]}";
                    
                    // Show a shortened version of the hash to keep it subtle and clean
                    string hash = parts[1];
                    if (hash.Length > 8) hash = hash.Substring(0, 8);
                    
                    TxtSubVersion.Text = $"(Build: {hash})";
                }
                else
                {
                    TxtMainVersion.Text = $"Version: {fullVersion}";
                    TxtSubVersion.Text = string.Empty;
                    TxtSubVersion.Visibility = Visibility.Collapsed;
                }
            }
            catch
            {
                TxtMainVersion.Text = "Version: v2.0.0";
                TxtSubVersion.Visibility = Visibility.Collapsed;
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            // Open the GitHub link in the default browser safely
            try
            {
                using (var process = Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                }))
                { }
                e.Handled = true;
            }
            catch { }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}
