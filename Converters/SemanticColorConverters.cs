using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WpfColor = System.Windows.Media.Color;

namespace CortexDNA.Converters
{
    /// <summary>
    /// Converts a usage % (0-100) to a muted semantic brush.
    /// Low  (&lt;60)  → Muted Teal   (#10B981) — calm, not neon
    /// Mid  (60-80) → Amber        (#F59E0B) — warm, visible
    /// High (&gt;80)  → Rose/Red     (#F43F5E) — alert, not violent
    /// </summary>
    public class UsageToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush LowBrush  = new SolidColorBrush(WpfColor.FromRgb(0x10, 0xB9, 0x81)); // Emerald-500
        private static readonly SolidColorBrush MidBrush  = new SolidColorBrush(WpfColor.FromRgb(0xF5, 0x9E, 0x0B)); // Amber-500
        private static readonly SolidColorBrush HighBrush = new SolidColorBrush(WpfColor.FromRgb(0xF4, 0x3F, 0x5E)); // Rose-500

        static UsageToColorConverter()
        {
            LowBrush.Freeze();
            MidBrush.Freeze();
            HighBrush.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
                return d >= 80 ? HighBrush : d >= 60 ? MidBrush : LowBrush;

            if (value is float f)
                return f >= 80 ? HighBrush : f >= 60 ? MidBrush : LowBrush;

            // Try parse string (e.g. "64.3 %" or "64.3")
            if (value is string s)
            {
                s = s.Replace("%", "").Trim();
                if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
                    return parsed >= 80 ? HighBrush : parsed >= 60 ? MidBrush : LowBrush;
            }

            return LowBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Converts a temperature value (°C) to a semantic Color.
    /// Cool  (&lt;60°C)  → Cyan/Green  (#00E5CC)
    /// Warm  (60-80°C) → Orange      (#FFA040)
    /// Hot   (&gt;80°C)  → Crimson     (#FF3B5C)
    /// </summary>
    public class TempToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush CoolBrush = new SolidColorBrush(WpfColor.FromRgb(0x10, 0xB9, 0x81)); // Emerald-500
        private static readonly SolidColorBrush WarmBrush = new SolidColorBrush(WpfColor.FromRgb(0xF5, 0x9E, 0x0B)); // Amber-500
        private static readonly SolidColorBrush HotBrush  = new SolidColorBrush(WpfColor.FromRgb(0xF4, 0x3F, 0x5E)); // Rose-500

        static TempToColorConverter()
        {
            CoolBrush.Freeze();
            WarmBrush.Freeze();
            HotBrush.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double temp = 0;

            if (value is double d) temp = d;
            else if (value is float f) temp = f;
            else if (value is string s)
            {
                // Strip " °C", "°C", " C" etc.
                s = s.Replace("°C", "").Replace("°", "").Replace("C", "").Trim();
                double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out temp);
            }

            return temp >= 80 ? HotBrush : temp >= 60 ? WarmBrush : CoolBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
