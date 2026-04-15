using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace CortexDNA
{
    public partial class CleanConfirmationWindow : Window
    {
        public CleanConfirmationWindow(int fileCount, double totalSizeMB, List<MainWindow.CleanupLocationItem> locations)
        {
            InitializeComponent();
            
            // Allow dragging the window
            this.MouseDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); };

            // Update Text
            string sizeText = totalSizeMB > 1024 
                ? $"{totalSizeMB / 1024.0:F2} GB" 
                : $"{totalSizeMB:F0} MB";

            TxtSummary.Text = $"Found {fileCount:N0} files taking up {sizeText}";
            ListLocations.ItemsSource = locations;
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