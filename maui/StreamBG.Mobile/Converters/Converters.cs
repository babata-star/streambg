using System.Globalization;

namespace StreamBG.Mobile.Converters;

public class TabBgConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var selected = value is int i ? i : 0;
        var tab = parameter is string s && int.TryParse(s, out var p) ? p : 0;
        return selected == tab ? Color.FromArgb("#a970ff") : Color.FromArgb("#18181b");
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class EqualConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() == parameter?.ToString();
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class InitialConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && s.Length > 0 ? s[0].ToString().ToUpper() : "?";
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class NotNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s ? !string.IsNullOrWhiteSpace(s) : value is not null;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string param) return string.Empty;
        var parts = param.Split('|');
        var flag = value is bool b && b;
        return flag ? (parts.Length > 0 ? parts[0] : "") : (parts.Length > 1 ? parts[1] : "");
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class InvertBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;
}

public class GreaterThanZeroConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            int i    => i > 0,
            double d => d > 0,
            float f  => f > 0,
            long l   => l > 0,
            _        => false
        };
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// Converts integer seconds to "m:ss" display (e.g. 30 → "0:30", 125 → "2:05")
public class SecondsToTimeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var secs = value switch
        {
            int i    => i,
            double d => (int)d,
            float f  => (int)f,
            long l   => (int)l,
            _        => 0
        };
        return $"{secs / 60}:{secs % 60:D2}";
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// Returns true when the Clip.Status string equals "Ready".
// Pass ConverterParameter="invert" to flip the result (true when NOT Ready).
public class ClipReadyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isReady = value is string s && s == "Ready";
        return parameter?.ToString() == "invert" ? !isReady : isReady;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
