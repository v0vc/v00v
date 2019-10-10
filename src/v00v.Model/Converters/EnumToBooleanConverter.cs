using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace v00v.Model.Converters
{
    public class EnumToBooleanConverter : IValueConverter
    {
        #region Static and Readonly Fields

        public static readonly EnumToBooleanConverter Instance = new EnumToBooleanConverter();

        #endregion

        #region Methods

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.Equals(parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null)
            {
                return (bool)value ? parameter : new Binding(null);
            }

            return new Binding(null);
        }

        #endregion
    }
}
