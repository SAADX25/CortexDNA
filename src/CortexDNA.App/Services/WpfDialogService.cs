using System.Windows;
using CortexDNA.Core;
using CortexDNA.Models;
using CortexDNA.UI;
namespace CortexDNA.Services;

public sealed class WpfDialogService(NotificationCenter notifications) : IDialogService
{
    public bool Confirm(string title, string message) => new ConfirmationWindow(title, message)
    { Owner = System.Windows.Application.Current?.MainWindow }.ShowDialog() == true;
    public IReadOnlyList<CleanupLocationItem>? ConfirmCleanup(IReadOnlyList<CleanupLocationItem> locations)
    {
        var dialog = new CleanConfirmationWindow(locations) { Owner = System.Windows.Application.Current?.MainWindow };
        return dialog.ShowDialog() == true ? dialog.SelectedLocations : null;
    }
    public void ShowCleanupResult(CleanupCleanResult result)
    {
        var message = $"Freed {DiskCleanupService.FormatByteSize(result.FreedBytes)} · {result.DeletedFiles} files removed";
        if (result.FailedFiles > 0) message += $" · {result.FailedFiles} skipped";
        Notify(message, result.FailedFiles > 0 ? UiState.Warning : UiState.Success);
        new CleanupResultsWindow(result.FreedBytes, result.DeletedFiles)
        { Owner = System.Windows.Application.Current?.MainWindow }.ShowDialog();
    }
    public void Notify(string message, UiState state) => notifications.Publish(message, state);
}
