using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Centerly.App.Converters;

/// <summary>null → Visible, otherwise Collapsed; <see cref="Invert"/> flips it.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value is null) ^ Invert ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
