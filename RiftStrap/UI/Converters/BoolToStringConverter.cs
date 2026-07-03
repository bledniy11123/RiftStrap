using System;
using System.Globalization;
using System.Windows.Data;

namespace RiftStrap.UI.Converters
{
    // Maps a bool to one of two strings supplied as ConverterParameter "TrueText|FalseText".
    public class BoolToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var isTrue = value is bool b && b;

            if (parameter is string s)
            {
                var parts = s.Split('|');
                if (parts.Length == 2)
                    return isTrue ? parts[0] : parts[1];
            }

            return isTrue ? "True" : "False";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException(nameof(ConvertBack));
    }
}
