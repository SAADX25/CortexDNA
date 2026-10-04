using CortexDNA.ViewModels.Pages;
namespace CortexDNA.Navigation;

public sealed class NavigationService : INavigationService
{
    private readonly IReadOnlyDictionary<PageId, PageViewModel> _pages;
    private bool _disposed;
    public NavigationStore Store { get; }
    public NavigationService(NavigationStore store, IEnumerable<PageViewModel> pages)
    {
        Store = store ?? throw new ArgumentNullException(nameof(store));
        _pages = pages.ToDictionary(page => page.Id);
    }
    public bool Navigate(PageId page)
    {
        if (_disposed || !_pages.TryGetValue(page, out var next)) return false;
        if (ReferenceEquals(Store.CurrentPage, next)) return false;
        Store.CurrentPage?.OnNavigatedFrom();
        Store.CurrentPage = next;
        next.OnNavigatedTo();
        return true;
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Store.CurrentPage?.OnNavigatedFrom();
        Store.CurrentPage = null;
        foreach (var page in _pages.Values) page.Dispose();
    }
}
