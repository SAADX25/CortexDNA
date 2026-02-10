using System;
using System.Windows;
using System.Windows.Input;

namespace CortexDNA
{
    public partial class CleanConfirmationWindow : Window
    {
        public CleanConfirmationWindow(int fileCount, double totalSizeMB)
        {
            InitializeComponent();
            
            // Allow dragging the window
            this.MouseDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); };

            // Update Text
            string sizeText = totalSizeMB > 1024 
                ? $"{totalSizeMB / 1024.0:F2} GB" 
                : $"{totalSizeMB:F0} MB";

            TxtSummary.Text = $"Found {fileCount:N0} files taking up {sizeText}";
        }

        private void BtnClean_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}