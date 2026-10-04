using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WpfColor = System.Windows.Media.Color;

namespace CortexDNA.Converters
{
    internal static class SemanticTheme
    {
        public static bool IsLight()
        {
            if (System.Windows.Application.Current?.Resources["AppBackgroundColor"] is WpfColor c)
                return c.R > 0xC0 && c.G > 0xC0 && c.B > 0xC0;
            return false;
        }
    }

    /// <summary>
    /// Converts a usage % (0-100) to a muted semantic brush.
    /// Low  (&lt;60)  → calm green
    /// Mid  (60-80) → Amber
    /// High (&gt;80)  → Rose/Red
    /// Greens are darker in light theme so values stay readable on white cards.
    /// </summary>
    public class UsageToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush LowDark  = Freeze(WpfColor.FromRgb(0x22, 0xC5, 0x5E));
        private static readonly SolidColorBrush LowLight = Freeze(WpfColor.FromRgb(0x15, 0x80, 0x3D));
        private static readonly SolidColorBrush MidBrush  = Freeze(WpfColor.FromRgb(0xD9, 0x77, 0x06));
        private static readonly SolidColorBrush HighBrush = Freeze(WpfColor.FromRgb(0xE1, 0x1D, 0x48));

        private static SolidColorBrush Freeze(WpfColor color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double n = 0;
            if (value is double d) n = d;
            else if (value is float f) n = f;
            else if (value is string s)
            {
                s = s.Replace("%", "").Trim();
                double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out n);
            }

            if (n >= 80) return HighBrush;
            if (n >= 60) return MidBrush;
            return SemanticTheme.IsLight() ? LowLight : LowDark;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Converts a temperature / metric value to a semantic Color.
    /// Cool  (&lt;60°C)  → green
    /// Warm  (60-80°C) → orange
    /// Hot   (&gt;80°C)  → crimson
    /// </summary>
    public class TempToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush CoolDark  = Freeze(WpfColor.FromRgb(0x22, 0xC5, 0x5E));
        private static readonly SolidColorBrush CoolLight = Freeze(WpfColor.FromRgb(0x15, 0x80, 0x3D));
        private static readonly SolidColorBrush WarmBrush = Freeze(WpfColor.FromRgb(0xD9, 0x77, 0x06));
        private static readonly SolidColorBrush HotBrush  = Freeze(WpfColor.FromRgb(0xE1, 0x1D, 0x48));

        private static SolidColorBrush Freeze(WpfColor color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double temp = 0;

            if (value is double d) temp = d;
            else if (value is float f) temp = f;
            else if (value is string s)
            {
                s = s.Replace("°C", "").Replace("°", "").Replace("C", "").Trim();
                double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out temp);
            }

            if (temp >= 80) return HotBrush;
            if (temp >= 60) return WarmBrush;
            return SemanticTheme.IsLight() ? CoolLight : CoolDark;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
