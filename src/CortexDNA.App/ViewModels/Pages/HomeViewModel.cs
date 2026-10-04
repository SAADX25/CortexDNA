using CortexDNA.Navigation;
namespace CortexDNA.ViewModels.Pages;

public sealed class HomeViewModel : PageViewModel
{
    public HomeViewModel(MainViewModel shell) : base(PageId.Home, "Your system, at a glance", "Live telemetry and clear, deliberate actions.", shell) { }

}
