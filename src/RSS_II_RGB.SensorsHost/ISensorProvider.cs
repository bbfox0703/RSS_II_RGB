using RSS_II_RGB.Core.Ipc;

namespace RSS_II_RGB.SensorsHost;

/// <summary>A source of sensor samples (temperatures, audio bands, …) streamed to the app.</summary>
internal interface ISensorProvider : IDisposable
{
    void Start();

    /// <summary>The latest sample(s) to emit this tick.</summary>
    IEnumerable<SensorSample> Poll();
}
