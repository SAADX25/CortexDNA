using CortexDNA.Navigation;
namespace CortexDNA.ViewModels.Pages;

public sealed class SecurityViewModel : PageViewModel
{
    public SecurityViewModel(MainViewModel shell) : base(PageId.Security, "Security overview", "Security posture checks are not available yet.", shell) { }

}
