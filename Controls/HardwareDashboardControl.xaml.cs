using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CortexDNA.ViewModels;

namespace CortexDNA.Controls
{
    public partial class HardwareDashboardControl : System.Windows.Controls.UserControl
    {
        public HardwareDashboardControl()
        {
            InitializeComponent();
        }

        private async void CopyCpuInfo_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is HardwareViewModel vm && !string.IsNullOrEmpty(vm.CpuName))
            {
                try
                {
                    System.Windows.Clipboard.SetText(vm.CpuName);
                    
                    if (TxtCpuCopyFeedback != null)
                    {
                        TxtCpuCopyFeedback.Visibility = Visibility.Visible;
                        await Task.Delay(2000);
                        TxtCpuCopyFeedback.Visibility = Visibility.Collapsed;
                    }
                }
                catch { }
            }
        }

        private async void CopyGpuInfo_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is HardwareViewModel vm && !string.IsNullOrEmpty(vm.GpuName))
            {
                try
                {
                    System.Windows.Clipboard.SetText(vm.GpuName);
                    
                    if (TxtGpuCopyFeedback != null)
                    {
                        TxtGpuCopyFeedback.Visibility = Visibility.Visible;
                        await Task.Delay(2000);
                        TxtGpuCopyFeedback.Visibility = Visibility.Collapsed;
                    }
                }
                catch { }
            }
        }
    }
}
