using CortexDNA.Core;
using CortexDNA.Models;

namespace CortexDNA.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private AppSection _currentSection = AppSection.Overview;

        public MainViewModel()
        {
            HardwareVM = new HardwareViewModel();
            StartupVM = new StartupViewModel();
            NavigateCommand = new RelayCommand<string>(Navigate);
        }

        public HardwareViewModel HardwareVM { get; }
        public StartupViewModel StartupVM { get; }
        public RelayCommand<string> NavigateCommand { get; }

        public AppSection CurrentSection
        {
            get => _currentSection;
            private set
            {
                if (!SetProperty(ref _currentSection, value)) return;
                OnPropertyChanged(nameof(IsOverviewVisible));
                OnPropertyChanged(nameof(IsStartupVisible));
                OnPropertyChanged(nameof(IsOverviewSelected));
                OnPropertyChanged(nameof(IsStartupSelected));
            }
        }

        public bool IsOverviewVisible => CurrentSection == AppSection.Overview;
        public bool IsStartupVisible => CurrentSection == AppSection.Startup;
        public bool IsOverviewSelected => CurrentSection == AppSection.Overview;
        public bool IsStartupSelected => CurrentSection == AppSection.Startup;

        private void Navigate(string? section)
        {
            if (string.Equals(section, nameof(AppSection.Startup), StringComparison.OrdinalIgnoreCase))
            {
                CurrentSection = AppSection.Startup;
                StartupVM.EnsureLoaded();
                return;
            }

            CurrentSection = AppSection.Overview;
        }
    }
}
