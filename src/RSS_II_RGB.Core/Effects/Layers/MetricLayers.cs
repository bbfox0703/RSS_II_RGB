using RSS_II_RGB.Core.Rendering;
using RSS_II_RGB.Core.Sensors;

namespace RSS_II_RGB.Core.Effects.Layers;

/// <summary>Which system metric a bar shows.</summary>
public enum MetricSource
{
    CpuUtil,
    MemUtil,
    GpuUtil,
    GpuTemp,
}

/// <summary>One metric shown as a bar: an ordered set of LED indices (cell 0 lights first).</summary>
public readonly record struct MetricBar(MetricSource Source, int[] CellIndices);

/// <summary>
/// Overlays system-metric bars on their assigned keys, on top of the global effect.
/// Each metric lights 1–4 cells by a bucketed value (more cells = higher), coloured
/// green→red by level; unlit cells of the group are dimmed so the bar is readable.
/// The mask covers only the metric keys, so the rest of the keyboard is untouched.
/// </summary>
public sealed class MetricOverlayLayer : IEffectLayer
{
    private static readonly Rgb Unlit = new(10, 10, 10);
    private static readonly Rgb NoData = new(40, 40, 40);

    private readonly SensorState _state;
    private readonly IReadOnlyList<MetricBar> _bars;
    private readonly double[] _percentThresholds; // [t1, t2, t3]
    private readonly double[] _tempThresholds;

    public MetricOverlayLayer(string id, SensorState state, IReadOnlyList<MetricBar> bars,
                              double[] percentThresholds, double[] tempThresholds, int zOrder = 500)
    {
        Id = id;
        _state = state;
        _bars = bars;
        _percentThresholds = percentThresholds;
        _tempThresholds = tempThresholds;
        ZOrder = zOrder;

        var indices = new List<int>();
        foreach (MetricBar bar in bars)
        {
            indices.AddRange(bar.CellIndices);
        }
        Mask = KeyMask.FromIndices(indices.ToArray());
    }

    public string Id { get; }
    public int ZOrder { get; }
    public BlendMode Blend => BlendMode.Normal;
    public KeyMask Mask { get; }
    public bool IsComplete => false;

    public void Render(Span<Rgb> target, in EffectContext ctx)
    {
        foreach (MetricBar bar in _bars)
        {
            int[] cells = bar.CellIndices;
            if (cells.Length == 0)
            {
                continue;
            }

            double value = Read(bar.Source);
            if (double.IsNaN(value))
            {
                foreach (int cell in cells)
                {
                    target[cell] = NoData;
                }
                continue;
            }

            bool isTemp = bar.Source == MetricSource.GpuTemp;
            int lit = Math.Min(Cells(value, isTemp ? _tempThresholds : _percentThresholds), cells.Length);
            Rgb color = LevelColor(lit);

            for (int i = 0; i < cells.Length; i++)
            {
                target[cells[i]] = i < lit ? color : Unlit;
            }
        }
    }

    private double Read(MetricSource source) => source switch
    {
        MetricSource.CpuUtil => _state.CpuUtil,
        MetricSource.MemUtil => _state.MemUtil,
        MetricSource.GpuUtil => _state.GpuUtil,
        MetricSource.GpuTemp => _state.GpuTemp,
        _ => double.NaN,
    };

    // value -> 1..4 cells by three ascending thresholds.
    private static int Cells(double value, double[] thresholds)
    {
        if (value <= thresholds[0]) return 1;
        if (value <= thresholds[1]) return 2;
        if (value <= thresholds[2]) return 3;
        return 4;
    }

    // 1 cell = green, 4 cells = red.
    private static Rgb LevelColor(int lit)
    {
        double t = (lit - 1) / 3.0;            // 0..1
        double hue = (1 - t) * (120.0 / 360.0); // 120° green -> 0° red
        return Rgb.FromHsv(hue, 1, 1);
    }
}
