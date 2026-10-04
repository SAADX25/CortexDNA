using CortexDNA.Navigation;
namespace CortexDNA.ViewModels.Pages;

public sealed class StartupPageViewModel : PageViewModel
{
    public StartupPageViewModel(MainViewModel shell) : base(PageId.Startup, "Start on your terms", "Review applications that launch with Windows.", shell) { }
    public override void OnNavigatedTo() { base.OnNavigatedTo(); if (IsActive) Shell.StartupVM.EnsureLoaded(); }
}
