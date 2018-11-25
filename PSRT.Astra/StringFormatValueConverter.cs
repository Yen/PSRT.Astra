using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace PSRT.Astra
{
    public class StringFormatValueConverter : IMultiValueConverter
    {
        public static StringFormatValueConverter Instance = new StringFormatValueConverter();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 0)
                return string.Empty;

            if (values[0] == DependencyProperty.UnsetValue)
                return null;

            var formatString = values[0] as string;
            if (formatString == null)
                throw new ArgumentException("Value must be a string", nameof(values));

            return string.Format(formatString, values.Skip(1).ToArray());
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
