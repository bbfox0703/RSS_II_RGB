using System.Text.Json.Serialization;

namespace RSS_II_RGB.App;

/// <summary>Persisted app state — restored on launch so a setup survives restarts.</summary>
internal sealed class AppSettings
{
    [JsonConverter(typeof(JsonStringEnumConverter<EffectChoice>))]
    public EffectChoice GlobalEffect { get; set; } = EffectChoice.Rainbow;

    public string GlobalColorHex { get; set; } = "00FF66";

    public double BrightnessPercent { get; set; } = 100;

    public double AudioSensitivity { get; set; } = 0.9;

    // System-metric overlay.
    public bool ShowMetrics { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<MetricLayoutChoice>))]
    public MetricLayoutChoice MetricLayout { get; set; } = MetricLayoutChoice.FunctionRow;

    public double[] PercentThresholds { get; set; } = { 30, 60, 90 };

    public double[] TempThresholds { get; set; } = { 55, 65, 75 };

    public List<ZoneSetting> Zones { get; set; } = new();
}

/// <summary>Serializable form of a <see cref="Zone"/>.</summary>
internal sealed class ZoneSetting
{
    public int[] KeyIndices { get; set; } = Array.Empty<int>();

    [JsonConverter(typeof(JsonStringEnumConverter<EffectChoice>))]
    public EffectChoice Effect { get; set; } = EffectChoice.Solid;

    public string ColorHex { get; set; } = "FFFFFF";

    [JsonConverter(typeof(JsonStringEnumConverter<AudioZoneMode>))]
    public AudioZoneMode AudioMode { get; set; } = AudioZoneMode.Spectrum;
}

// Source-generated, reflection-free JSON (CLAUDE.md rule 5).
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal partial class AppSettingsJsonContext : JsonSerializerContext
{
}
