namespace CortexDNA.Navigation;

public enum PageId { Home, Health, Cleanup, Storage, Startup, Hardware, Gaming, Security, Tools, Settings, Diagnostics }
public interface INavigationAware { void OnNavigatedTo(); void OnNavigatedFrom(); }
public interface INavigationService : IDisposable { bool Navigate(PageId page); NavigationStore Store { get; } }
