using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace v00v.Model.Converters
{
    public class EnumToBooleanConverter : IValueConverter
    {
        #region Static and Readonly Fields

        public static readonly EnumToBooleanConverter s_instance = new();

        #endregion

        #region Methods

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.Equals(parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null ? (bool)value ? parameter : new Binding(null!) : new Binding(null!);
        }

        #endregion
    }
}
