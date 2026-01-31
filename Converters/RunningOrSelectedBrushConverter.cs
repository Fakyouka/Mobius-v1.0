using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Mobius.Converters
{
    // Теперь рамка зелёная ТОЛЬКО когда приложение реально запущено.
    // SelectedItem больше не красит зелёным.
    public class RunningOrSelectedBrushConverter : IMultiValueConverter
    {
        private static readonly Brush Green = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2BFF88"));
        private static readonly Brush Default = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#222A33"));

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            bool isRunning = values != null && values.Length > 0 && values[0] is bool b0 && b0;
            return isRunning ? Green : Default;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
