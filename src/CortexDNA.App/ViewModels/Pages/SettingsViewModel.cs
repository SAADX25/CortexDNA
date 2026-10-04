using CortexDNA.Navigation;
namespace CortexDNA.ViewModels.Pages;

public sealed class SettingsViewModel : PageViewModel
{
    public SettingsViewModel(MainViewModel shell) : base(PageId.Settings, "Make it yours", "Appearance, motion and familiar application preferences.", shell) { }

}
