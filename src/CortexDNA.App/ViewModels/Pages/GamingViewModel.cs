using CortexDNA.Navigation;
namespace CortexDNA.ViewModels.Pages;

public sealed class GamingViewModel : PageViewModel
{
    public GamingViewModel(MainViewModel shell) : base(PageId.Gaming, "Gaming activity", "The existing automatic mode reduces CortexDNA sensor polling while gaming.", shell) { }

}
