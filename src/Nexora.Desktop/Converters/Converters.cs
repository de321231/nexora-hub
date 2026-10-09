using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Nexora.Shared.Models;

namespace Nexora.Desktop.Converters;

/// <summary>True -> Visible, False -> Collapsed.</summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Visible ? false : true;
}

/// <summary>Null -> Collapsed, sonst Visible.</summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value == null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Lizenzstatus in lokalisierte Bezeichnung.</summary>
public class LicenseStatusTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value is LicenseStatus l ? l : LicenseStatus.Pending;
        return status switch
        {
            LicenseStatus.Active => "Aktiv",
            LicenseStatus.Pending => "Ausstehend",
            LicenseStatus.Expired => "Abgelaufen",
            LicenseStatus.Suspended => "Gesperrt",
            LicenseStatus.Revoked => "Widerrufen",
            _ => status.ToString()
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Lizenzstatus in Statusfarbe (Brush-Suche über Application.Current).</summary>
public class LicenseStatusBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is LicenseStatus s
            ? s switch
            {
                LicenseStatus.Active => "SuccessBrush",
                LicenseStatus.Pending => "WarningBrush",
                LicenseStatus.Expired => "TextMutedBrush",
                LicenseStatus.Suspended or LicenseStatus.Revoked => "ErrorBrush",
                _ => "TextMutedBrush"
            }
            : "TextMutedBrush";
        return Application.Current?.TryFindResource(key) ?? System.Windows.Media.Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Chat-Rolle (User/Assistant/System) für Anzeige.</summary>
public class ChatRoleTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ChatRole r
            ? r switch
            {
                ChatRole.User => "Du",
                ChatRole.Assistant => "Nexora AI",
                ChatRole.System => "System",
                _ => r.ToString()
            }
            : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Dezimalstundenformat für Kosten.</summary>
public class CostTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not decimal d || d <= 0) return "–";
        return d.ToString("$0.0000", culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>True -> "~" (geschätzt), False -> "".</summary>
public class BoolToEstimatedTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "~" : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}