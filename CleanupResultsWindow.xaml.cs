using System;
using System.Windows;
using System.Windows.Input;

namespace CortexDNA
{
    public partial class CleanupResultsWindow : Window
    {
        public CleanupResultsWindow(long freedBytes, int fileCount)
        {
            InitializeComponent();
            
            // Allow dragging
            this.MouseDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); };

            // Format Size (Exact Logic)
            string sizeText;
            double bytes = (double)freedBytes;

            if (bytes == 0)
            {
                sizeText = "0 Bytes";
            }
            else if (bytes < 1048576) // Less than 1 MB
            {
                double kb = bytes / 1024.0;
                sizeText = $"{kb:F0} KB";
            }
            else // 1 MB or more
            {
                double mb = bytes / (1024.0 * 1024.0);
                if (mb >= 1024)
                {
                    double gb = mb / 1024.0;
                    sizeText = $"{gb:F2} GB";
                }
                else
                {
                    sizeText = $"{mb:F1} MB";
                }
            }

            TxtFreedSpace.Text = sizeText;
            
            // Format Details
            TxtDetails.Text = $"Removed {fileCount:N0} junk files from selected locations.";
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}