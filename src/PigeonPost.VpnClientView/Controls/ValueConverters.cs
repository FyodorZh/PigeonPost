using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using PigeonPost.Vpn;

namespace PigeonPost.VpnClientView.Controls;

public sealed class LogLevelToBackgroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            VpnLogLevel.Warning => new SolidColorBrush(Color.FromRgb(230, 81, 0)),
            VpnLogLevel.Error => new SolidColorBrush(Color.FromRgb(183, 28, 28)),
            _ => new SolidColorBrush(Color.FromRgb(27, 94, 32))
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
