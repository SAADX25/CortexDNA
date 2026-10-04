using CortexDNA.Navigation;
namespace CortexDNA.ViewModels.Pages;

public sealed class HealthViewModel : PageViewModel
{
    public HealthViewModel(MainViewModel shell) : base(PageId.Health, "System health", "A view of current telemetry. A full health assessment is not available yet.", shell) { }

}
