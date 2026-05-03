using CortexDNA.Core;

namespace CortexDNA.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        public PrivacyViewModel PrivacyVM { get; } = new PrivacyViewModel();
        public HardwareViewModel HardwareVM { get; } = new HardwareViewModel();
    }
}
