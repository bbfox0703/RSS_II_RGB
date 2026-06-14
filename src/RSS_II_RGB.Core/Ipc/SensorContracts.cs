using System.Text.Json.Serialization;

namespace RSS_II_RGB.Core.Ipc;

/// <summary>What a sensor sample measures.</summary>
public enum SensorKind
{
    CpuTemp,
    GpuTemp,
    AudioBands,
    CpuUtil,
    MemUtil,
    GpuUtil,
}

/// <summary>
/// One reading streamed from the (future) non-AOT SensorsHost helper to the main
/// app. <see cref="Values"/> is a single temperature for CpuTemp/GpuTemp, or the
/// FFT band magnitudes for AudioBands.
/// </summary>
public readonly record struct SensorSample(SensorKind Kind, double[] Values, long TimestampMs);

/// <summary>
/// The async stream of sensor samples. Milestone 1 ships only this contract; the
/// helper process and a named-pipe implementation come later, keeping the
/// reflection-heavy sensor/audio libraries out of the AOT main app.
/// </summary>
public interface ISensorFeed
{
    IAsyncEnumerable<SensorSample> ReadAsync(CancellationToken ct = default);
}

/// <summary>Source-generated JSON context for IPC (AOT-safe, no reflection — rule 5).</summary>
[JsonSerializable(typeof(SensorSample))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class SensorJsonContext : JsonSerializerContext
{
}
