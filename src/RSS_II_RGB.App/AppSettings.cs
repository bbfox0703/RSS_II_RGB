using System.Text.Json.Serialization;

namespace RSS_II_RGB.App;

/// <summary>Persisted app state — restored on launch so a setup survives restarts.</summary>
internal sealed class AppSettings
{
    [JsonConverter(typeof(JsonStringEnumConverter<EffectChoice>))]
    public EffectChoice GlobalEffect { get; set; } = EffectChoice.Rainbow;

    public string GlobalColorHex { get; set; } = "00FF66";

    public double BrightnessPercent { get; set; } = 100;

    // Independent display overlays, layered on top of the base effect.
    public bool EnableReactive { get; set; }

    public bool EnableAudio { get; set; }

    // Starlight overlay (twinkling stars above the base effect, below other overlays).
    public bool EnableStarlight { get; set; }

    public double AudioSensitivity { get; set; } = 0.9;

    // Global audio overlay: spectrum vs three-region bars, and the per-region bars multipliers.
    [JsonConverter(typeof(JsonStringEnumConverter<AudioLayoutChoice>))]
    public AudioLayoutChoice AudioLayout { get; set; } = AudioLayoutChoice.Spectrum;

    public double AudioBarsBassMultiplier { get; set; } = 0.9;

    public double AudioBarsMidMultiplier { get; set; } = 1.0;

    public double AudioBarsTrebleMultiplier { get; set; } = 2.0;

    // System-metric overlay.
    public bool ShowMetrics { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<MetricLayoutChoice>))]
    public MetricLayoutChoice MetricLayout { get; set; } = MetricLayoutChoice.FunctionRow;

    public double[] PercentThresholds { get; set; } = { 30, 60, 90 };

    public double[] TempThresholds { get; set; } = { 55, 65, 75 };

    // GIF animation effect: the baked .kbanim plus the source GIF + crop rectangle
    // (source pixels: [x, y, width, height]) so the import can be re-opened/re-edited.
    public string? GifAnimPath { get; set; }

    public string? GifSourcePath { get; set; }

    public int[] GifCrop { get; set; } = Array.Empty<int>();

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
