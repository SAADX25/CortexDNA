using CortexDNA.Navigation;
namespace CortexDNA.ViewModels.Pages;

public sealed class HardwarePageViewModel : PageViewModel
{
    public HardwarePageViewModel(MainViewModel shell) : base(PageId.Hardware, "Inside your system", "Live processor, graphics, memory, network and drive readings.", shell) { }

}
