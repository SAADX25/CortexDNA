using CortexDNA.Models;
namespace CortexDNA.Services;

public interface IDialogService
{
    bool Confirm(string title, string message);
    IReadOnlyList<CleanupLocationItem>? ConfirmCleanup(IReadOnlyList<CleanupLocationItem> locations);
    void ShowCleanupResult(CleanupCleanResult result);
    void Notify(string message, CortexDNA.UI.UiState state);
}
