using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CortexDNA.ViewModels;
namespace CortexDNA.Controls;

public partial class HardwareDashboardControl : System.Windows.Controls.UserControl
{
    private CancellationTokenSource? _feedbackLifetime;
    public HardwareDashboardControl()
    {
        InitializeComponent();
        Loaded += (_, _) => { _feedbackLifetime?.Dispose(); _feedbackLifetime = new(); };
        Unloaded += (_, _) =>
        {
            _feedbackLifetime?.Cancel(); _feedbackLifetime?.Dispose(); _feedbackLifetime = null;
            TxtCpuCopyFeedback.Visibility = Visibility.Collapsed; TxtGpuCopyFeedback.Visibility = Visibility.Collapsed;
        };
    }
    private async Task CopyAsync(string text, System.Windows.Controls.TextBlock feedback)
    {
        if (string.IsNullOrEmpty(text) || _feedbackLifetime == null) return;
        var token = _feedbackLifetime.Token;
        try
        {
            System.Windows.Clipboard.SetText(text); feedback.Visibility = Visibility.Visible;
            await Task.Delay(2000, token);
            if (!token.IsCancellationRequested && IsLoaded) feedback.Visibility = Visibility.Collapsed;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { CortexDNA.Core.Logger.Log(ex); }
    }
    private async void CopyCpuInfo_Click(object sender, RoutedEventArgs e)
    { if (DataContext is HardwareViewModel vm) await CopyAsync(vm.CpuName, TxtCpuCopyFeedback); }
    private async void CopyGpuInfo_Click(object sender, RoutedEventArgs e)
    { if (DataContext is HardwareViewModel vm) await CopyAsync(vm.GpuName, TxtGpuCopyFeedback); }
}
