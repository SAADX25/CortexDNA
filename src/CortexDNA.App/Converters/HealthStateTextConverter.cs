using System.Globalization;
using System.Windows.Data;
using CortexDNA.Core.Health;
namespace CortexDNA.Converters;

public sealed class HealthStateTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        HealthState.Healthy => "Healthy",
        HealthState.Attention => "Attention",
        HealthState.OptimizationAvailable => "Optimization available",
        HealthState.Error => "Confirmed problem",
        _ => "Unavailable"
    };
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => System.Windows.Data.Binding.DoNothing;
}
