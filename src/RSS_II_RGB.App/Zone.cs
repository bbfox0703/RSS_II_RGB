using System.Globalization;
using RSS_II_RGB.Core.Rendering;

namespace RSS_II_RGB.App;

/// <summary>How an Audio zone renders.</summary>
internal enum AudioZoneMode
{
    Spectrum,     // per-column frequency spectrum
    SolidColor,   // whole zone brightness = volume, fixed colour
    SolidRainbow, // whole zone brightness = volume, cycling rainbow
}

/// <summary>
/// A user-defined group of keys with its own effect — the Synapse-style override
/// layered on top of the global effect.
/// </summary>
internal sealed record Zone(
    IReadOnlyList<int> KeyIndices,
    EffectChoice Effect,
    Rgb Color,
    AudioZoneMode AudioMode = AudioZoneMode.Spectrum);

/// <summary>Conversions between the live <see cref="Zone"/> and its persisted form.</summary>
internal static class ZoneMapping
{
    public static Zone ToZone(ZoneSetting setting)
        => new(setting.KeyIndices, setting.Effect, ParseRgb(setting.ColorHex), setting.AudioMode);

    public static ZoneSetting ToSetting(Zone zone) => new()
    {
        KeyIndices = zone.KeyIndices is int[] arr ? arr : zone.KeyIndices.ToArray(),
        Effect = zone.Effect,
        ColorHex = $"{zone.Color.R:X2}{zone.Color.G:X2}{zone.Color.B:X2}",
        AudioMode = zone.AudioMode,
    };

    public static Rgb ParseRgb(string hex)
    {
        hex = hex.Trim().TrimStart('#');
        if (hex.Length == 6 && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
        {
            return new Rgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        }
        return Rgb.White;
    }
}
