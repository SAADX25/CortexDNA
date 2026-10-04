using CortexDNA.Navigation;
namespace CortexDNA.ViewModels.Pages;

public sealed class CleanupViewModel : PageViewModel
{

    public IReadOnlyList<CortexDNA.Models.CleanupLocationItem> Locations { get; }
    public CleanupViewModel(MainViewModel shell) : base(PageId.Cleanup, "Make room, carefully", "Review the existing cleanup scan before removing selected files.", shell)
    { Locations = shell.Cleanup.CreateDefaultLocations(); }
}
