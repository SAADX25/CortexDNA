using CortexDNA.Navigation;
namespace CortexDNA.ViewModels;

public sealed class NavigationItemViewModel(PageId id, string label, string mark) : ViewModelBase
{
    private bool _isSelected;
    public PageId Id { get; } = id;
    public string Label { get; } = label;
    public string Mark { get; } = mark;
    public bool IsSelected { get => _isSelected; internal set => SetProperty(ref _isSelected, value); }
}
