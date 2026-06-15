using System.Text.Json.Serialization;

namespace RSS_II_RGB.Core.Ipc;

/// <summary>
/// One-shot request handed to the non-AOT GifBakeHost helper: decode
/// <see cref="SourceGifPath"/>, crop to the given source-pixel rectangle,
/// downsample every frame to <see cref="Cols"/>×<see cref="Rows"/>, and write a
/// .kbanim to <see cref="OutputPath"/>. The reflection-heavy image library
/// (Magick.NET) lives only in the helper, never in the AOT main app (rule 5).
/// </summary>
public sealed record GifBakeRequest
{
    public string SourceGifPath { get; init; } = "";
    public string OutputPath { get; init; } = "";

    // Crop rectangle in source-image pixels.
    public int CropX { get; init; }
    public int CropY { get; init; }
    public int CropWidth { get; init; }
    public int CropHeight { get; init; }

    // Target grid (defaults to the keyboard matrix geometry).
    public int Cols { get; init; } = CoreConstants.MatrixCols;
    public int Rows { get; init; } = CoreConstants.MatrixRows;

    /// <summary>Gamma applied to each channel at bake time (1.0 = none).</summary>
    public double Gamma { get; init; } = 1.0;
}

/// <summary>Result printed by GifBakeHost to stdout as a single JSON line.</summary>
public sealed record GifBakeResult
{
    public bool Success { get; init; }
    public int FrameCount { get; init; }
    public int DurationMs { get; init; }
    public string? Error { get; init; }
}

/// <summary>Source-generated JSON context for the bake IPC (AOT-safe — rule 5).</summary>
[JsonSerializable(typeof(GifBakeRequest))]
[JsonSerializable(typeof(GifBakeResult))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class GifBakeJsonContext : JsonSerializerContext
{
}
