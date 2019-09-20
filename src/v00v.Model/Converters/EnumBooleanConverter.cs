using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace v00v.Model.Converters
{
    public class EnumBooleanConverter : IValueConverter
    {
        #region Methods

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value.Equals(parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value.Equals(true) ? parameter : new Binding(null);
        }

        #endregion
    }
}
