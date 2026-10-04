using CortexDNA.Navigation;
namespace CortexDNA.ViewModels.Pages;

public sealed class ToolsViewModel : PageViewModel
{

    public System.ComponentModel.ICollectionView Tools { get; }
    private string _query = string.Empty;
    public string Query { get => _query; set { if (SetProperty(ref _query, value)) { Tools.Refresh(); OnPropertyChanged(nameof(HasTools)); } } }
    public bool HasTools => !Tools.IsEmpty;
    public ToolsViewModel(MainViewModel shell) : base(PageId.Tools, "Windows tools", "Open familiar Windows utilities with their existing privilege requirements.", shell)
    { Tools = System.Windows.Data.CollectionViewSource.GetDefaultView(shell.ToolLauncher.Tools); Tools.Filter = item => item is CortexDNA.Services.ToolItem tool && (tool.Name + " " + tool.Keywords).Contains(Query.Trim(), StringComparison.OrdinalIgnoreCase); }
    public override void Dispose() { Tools.Filter = null; base.Dispose(); }
}
