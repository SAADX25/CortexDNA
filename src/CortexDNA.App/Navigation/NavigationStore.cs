using CortexDNA.Core;
using CortexDNA.ViewModels.Pages;
namespace CortexDNA.Navigation;

public sealed class NavigationStore : ObservableObject
{
    private PageViewModel? _currentPage;
    public PageViewModel? CurrentPage { get => _currentPage; internal set => SetProperty(ref _currentPage, value); }
}
