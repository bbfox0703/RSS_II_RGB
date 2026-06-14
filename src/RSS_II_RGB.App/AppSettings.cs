using System.Text.Json.Serialization;

namespace RSS_II_RGB.App;

/// <summary>Persisted app state — restored on launch so a setup survives restarts.</summary>
internal sealed class AppSettings
{
    [JsonConverter(typeof(JsonStringEnumConverter<EffectChoice>))]
    public EffectChoice GlobalEffect { get; set; } = EffectChoice.Rainbow;

    public string GlobalColorHex { get; set; } = "00FF66";

    public double BrightnessPercent { get; set; } = 100;

    public List<ZoneSetting> Zones { get; set; } = new();
}

/// <summary>Serializable form of a <see cref="Zone"/>.</summary>
internal sealed class ZoneSetting
{
    public int[] KeyIndices { get; set; } = Array.Empty<int>();

    [JsonConverter(typeof(JsonStringEnumConverter<EffectChoice>))]
    public EffectChoice Effect { get; set; } = EffectChoice.Solid;

    public string ColorHex { get; set; } = "FFFFFF";
}

// Source-generated, reflection-free JSON (CLAUDE.md rule 5).
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal partial class AppSettingsJsonContext : JsonSerializerContext
{
}
