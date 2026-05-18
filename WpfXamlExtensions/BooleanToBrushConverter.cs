using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;

namespace WpfXamlExtensions
{
    public class BooleanToBrushConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var (trueBrush, falseBrush) = ParseParameter(parameter);

            return value is bool b && b ? trueBrush : falseBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        private static (Brush trueBrush, Brush falseBrush) ParseParameter(object parameter)
        {
            var raw = parameter as string;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return (Brushes.Green, Brushes.Red);
            }

            var parts = raw.Split('|');
            var trueBrush = parts.Length > 0 ? ParseBrush(parts[0]) : Brushes.Green;
            var falseBrush = parts.Length > 1 ? ParseBrush(parts[1]) : Brushes.Red;
            return (trueBrush, falseBrush);
        }

        private static Brush ParseBrush(string s)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(s);
                return new SolidColorBrush(color);
            }
            catch
            {
                return Brushes.Gray;
            }
        }
    }
}