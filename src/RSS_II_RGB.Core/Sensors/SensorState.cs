using RSS_II_RGB.Core.Ipc;

namespace RSS_II_RGB.Core.Sensors;

/// <summary>
/// Latest sensor readings, written by the sensor feed pump and read by sensor-
/// driven effect layers. Thread-safe (updates ~10-60 Hz, reads ~40 Hz).
/// Temperatures are NaN until the first sample arrives.
/// </summary>
public sealed class SensorState
{
    private readonly object _gate = new();
    private double _cpuTemp = double.NaN;
    private double _gpuTemp = double.NaN;
    private double[] _audioBands = Array.Empty<double>();

    public void Apply(SensorSample sample)
    {
        lock (_gate)
        {
            switch (sample.Kind)
            {
                case SensorKind.CpuTemp:
                    if (sample.Values.Length > 0) _cpuTemp = sample.Values[0];
                    break;
                case SensorKind.GpuTemp:
                    if (sample.Values.Length > 0) _gpuTemp = sample.Values[0];
                    break;
                case SensorKind.AudioBands:
                    _audioBands = sample.Values;
                    break;
            }
        }
    }

    public double CpuTemp
    {
        get { lock (_gate) { return _cpuTemp; } }
    }

    public double GpuTemp
    {
        get { lock (_gate) { return _gpuTemp; } }
    }

    /// <summary>Copy the latest audio band magnitudes into <paramref name="dest"/>; returns the count copied.</summary>
    public int CopyAudioBands(Span<double> dest)
    {
        lock (_gate)
        {
            int n = Math.Min(dest.Length, _audioBands.Length);
            _audioBands.AsSpan(0, n).CopyTo(dest);
            return n;
        }
    }
}
