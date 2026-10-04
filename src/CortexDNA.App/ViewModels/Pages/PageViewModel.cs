using CortexDNA.Navigation;
namespace CortexDNA.ViewModels.Pages;

public abstract class PageViewModel : ViewModelBase, INavigationAware, IDisposable
{
    private bool _isActive;
    protected bool IsDisposed { get; private set; }
    protected PageViewModel(PageId id, string title, string description, MainViewModel shell)
    { Id = id; Title = title; Description = description; Shell = shell; }
    public PageId Id { get; }
    public string Title { get; }
    public string Description { get; }
    public MainViewModel Shell { get; }
    public HardwareViewModel Hardware => Shell.HardwareVM;
    public bool IsActive { get => _isActive; private set => SetProperty(ref _isActive, value); }
    public virtual void OnNavigatedTo() { if (!IsDisposed) IsActive = true; }
    public virtual void OnNavigatedFrom() => IsActive = false;
    public virtual void Dispose() { if (IsDisposed) return; if (IsActive) OnNavigatedFrom(); IsDisposed = true; }
}
