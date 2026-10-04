using CortexDNA.Navigation;
namespace CortexDNA.ViewModels.Pages;

public sealed class StorageViewModel : PageViewModel
{
    public StorageViewModel(MainViewModel shell) : base(PageId.Storage, "Storage overview", "Drive capacity and available space. Your personal files are not scanned.", shell) { }

}
