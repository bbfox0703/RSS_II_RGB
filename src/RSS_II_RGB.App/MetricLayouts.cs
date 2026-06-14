using RSS_II_RGB.Core.Effects.Layers;
using RSS_II_RGB.Core.Layout;

namespace RSS_II_RGB.App;

/// <summary>Which keys the system-metric bars use.</summary>
internal enum MetricLayoutChoice
{
    FunctionRow, // F-row + PrtSc cluster
    Numpad,
    Diagonal,    // diagonal runs in the alpha block
}

/// <summary>Builds the metric bars (metric -> ordered key cells) for each fixed layout.</summary>
internal static class MetricLayouts
{
    public static IReadOnlyList<MetricBar> Build(MetricLayoutChoice choice) => choice switch
    {
        MetricLayoutChoice.FunctionRow => new[]
        {
            Bar(MetricSource.CpuUtil, 0x18, 0x20, 0x28, 0x30), // F1-F4
            Bar(MetricSource.MemUtil, 0x40, 0x48, 0x50, 0x58), // F5-F8
            Bar(MetricSource.GpuUtil, 0x60, 0x68, 0x70, 0x78), // F9-F12
            Bar(MetricSource.GpuTemp, 0x80, 0x88, 0x90),       // PrtSc, ScrLk, Pause
        },
        MetricLayoutChoice.Numpad => new[]
        {
            Bar(MetricSource.CpuUtil, 0x99, 0xA1, 0xA9), // NumLk, /, *
            Bar(MetricSource.MemUtil, 0x9A, 0xA2, 0xAA), // 7, 8, 9
            Bar(MetricSource.GpuUtil, 0x9B, 0xA3, 0xAB), // 4, 5, 6
            Bar(MetricSource.GpuTemp, 0x9C, 0xA4, 0xAC), // 1, 2, 3
        },
        MetricLayoutChoice.Diagonal => new[]
        {
            Bar(MetricSource.CpuUtil, 0x44, 0x43, 0x42, 0x41), // M, J, U, 7
            Bar(MetricSource.MemUtil, 0x4C, 0x4B, 0x4A, 0x49), // ',', K, I, 8
            Bar(MetricSource.GpuUtil, 0x54, 0x53, 0x52, 0x51), // '.', L, O, 9
            Bar(MetricSource.GpuTemp, 0x5C, 0x5B, 0x5A, 0x59), // '/', ';', P, 0
        },
        _ => Array.Empty<MetricBar>(),
    };

    private static MetricBar Bar(MetricSource source, params byte[] keyIds)
    {
        var indices = new List<int>(keyIds.Length);
        foreach (byte keyId in keyIds)
        {
            int index = ScopeIILayout.IndexForKeyId(keyId);
            if (index >= 0)
            {
                indices.Add(index);
            }
        }
        return new MetricBar(source, indices.ToArray());
    }
}
