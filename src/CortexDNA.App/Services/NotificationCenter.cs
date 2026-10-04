using System.Collections.ObjectModel;
using CortexDNA.Core;
using CortexDNA.UI;
namespace CortexDNA.Services;

public sealed record NotificationItem(string Message, UiState State, DateTime CreatedAt);
public sealed class NotificationCenter : ObservableObject
{
    private readonly ObservableCollection<NotificationItem> _items = new();
    public ReadOnlyObservableCollection<NotificationItem> Items { get; }
    public NotificationCenter() { Items = new(_items); DismissCommand = new RelayCommand<NotificationItem>(Dismiss); }
    public RelayCommand<NotificationItem> DismissCommand { get; }
    public void Publish(string message, UiState state = UiState.Success)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (_items.LastOrDefault()?.Message == message) return;
        if (_items.Count == 20) _items.RemoveAt(0);
        _items.Add(new(message, state, DateTime.Now));
    }
    public void Dismiss(NotificationItem? item) { if (item != null) _items.Remove(item); }
}
