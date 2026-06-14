using System.Globalization;
using Avalonia.Data.Converters;

namespace RSS_II_RGB.App;

/// <summary>
/// Shows the localized display name for the effect / layout / audio-mode enums in
/// a ComboBox, while the bound value stays the enum. Reflection-free (a switch in
/// <see cref="L"/>), so AOT-safe.
/// </summary>
internal sealed class EnumDisplayConverter : IValueConverter
{
    public static readonly EnumDisplayConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        EffectChoice e => L.EffectName(e),
        MetricLayoutChoice m => L.MetricLayoutName(m),
        AudioZoneMode a => L.AudioModeName(a),
        _ => value?.ToString() ?? string.Empty,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
