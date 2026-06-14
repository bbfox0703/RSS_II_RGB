using RSS_II_RGB.Core.Ipc;

namespace RSS_II_RGB.SensorsHost;

/// <summary>Placeholder oscillating CPU/GPU temps until LibreHardwareMonitor is wired in.</summary>
internal sealed class SyntheticTempProvider : ISensorProvider
{
    private double _t;

    public void Start()
    {
    }

    public IEnumerable<SensorSample> Poll()
    {
        long now = Environment.TickCount64;
        double cpu = 55 + 25 * Math.Sin(_t);             // 30..80 °C
        double gpu = 50 + 20 * Math.Sin(_t * 0.7 + 1.0); // 30..70 °C
        _t += 0.15;

        yield return new SensorSample(SensorKind.CpuTemp, new[] { cpu }, now);
        yield return new SensorSample(SensorKind.GpuTemp, new[] { gpu }, now);
    }

    public void Dispose()
    {
    }
}
