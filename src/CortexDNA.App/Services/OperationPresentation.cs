using System.ComponentModel;
using CortexDNA.Core;
using CortexDNA.ViewModels;
namespace CortexDNA.Services;
/// <summary>Observes existing operations; it never schedules work or owns a timer.</summary>
public sealed class OperationPresentation : ObservableObject, IDisposable
{
    private readonly HardwareViewModel _hardware;
    private readonly StartupViewModel _startup;
    public OperationPresentation(HardwareViewModel hardware, StartupViewModel startup)
    { _hardware = hardware; _startup = startup; hardware.PropertyChanged += Changed; startup.PropertyChanged += Changed; }
    public bool IsRunning => _hardware.IsBoosting || _hardware.IsCleaningDisk || _startup.IsLoading;
    public string Message => _hardware.IsBoosting || _hardware.IsCleaningDisk ? _hardware.StatusMessage : _startup.IsLoading ? _startup.StatusMessage : "Ready";
    public double Progress => _hardware.OperationProgress;
    public bool IsIndeterminate => _startup.IsLoading || _hardware.IsBoosting || _hardware.OperationProgress <= 0;
    private void Changed(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is not (nameof(HardwareViewModel.StatusMessage) or nameof(HardwareViewModel.IsBoosting)
            or nameof(HardwareViewModel.IsCleaningDisk) or nameof(HardwareViewModel.OperationProgress)
            or nameof(StartupViewModel.IsLoading) or nameof(StartupViewModel.StatusMessage))) return;
        OnPropertyChanged(nameof(IsRunning)); OnPropertyChanged(nameof(Message)); OnPropertyChanged(nameof(Progress)); OnPropertyChanged(nameof(IsIndeterminate));
    }
    public void Dispose() { _hardware.PropertyChanged -= Changed; _startup.PropertyChanged -= Changed; }
}
