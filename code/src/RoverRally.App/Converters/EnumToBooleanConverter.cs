using System;
using System.Globalization;
using System.Windows.Data;

namespace RoverRally.App.Converters
{
    /// <summary>
    /// Lets a RadioButton's IsChecked bind two-way against one value of an
    /// enum property - Convert reports whether the bound enum equals the
    /// ConverterParameter, ConvertBack reports that parameter's value back
    /// when the button is checked. Standard WPF MVVM plumbing, not view
    /// logic, so it belongs beside the views it serves rather than in any
    /// code-behind (#73).
    /// </summary>
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            return value.ToString() == parameter.ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked && parameter != null)
            {
                return Enum.Parse(targetType, parameter.ToString()!);
            }

            return Binding.DoNothing;
        }
    }
}
