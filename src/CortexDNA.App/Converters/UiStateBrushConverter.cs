using System.Globalization;
using System.Windows.Data;
using CortexDNA.UI;
namespace CortexDNA.Converters;

public sealed class UiStateBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string key = value is UiState state ? state switch
        {
            UiState.Success => "SuccessBrush",
            UiState.Warning => "WarningBrush",
            UiState.Error => "ErrorBrush",
            UiState.Disabled => "MutedTextBrush",
            _ => "AccentBrush"
        } : "SecondaryTextBrush";
        return System.Windows.Application.Current?.TryFindResource(key) ?? System.Windows.Media.Brushes.Gray;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
