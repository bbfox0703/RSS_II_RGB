using System.Diagnostics;
using RSS_II_RGB.Core.Ipc;
using RSS_II_RGB.Core.Logging;
using RSS_II_RGB.Core.Sensors;
using RSS_II_RGB.Windows;

namespace RSS_II_RGB.App;

/// <summary>
/// Launches the SensorsHost helper process and pumps its sensor samples into the
/// shared <see cref="SensorState"/>. If the helper can't be found, sensor effects
/// simply show no data (dim) — the app keeps working.
/// </summary>
internal sealed class SensorService : IAsyncDisposable
{
    private readonly SensorState _state;
    private readonly ILogSink _log;
    private Process? _helper;
    private CancellationTokenSource? _cts;
    private Task? _pump;

    public SensorService(SensorState state, ILogSink log)
    {
        _state = state;
        _log = log;
    }

    public void Start()
    {
        string? exe = LocateHelper();
        if (exe is null)
        {
            _log.Log(LogLevel.Warning, "SensorsHost.exe not found; temperature/audio effects disabled.");
            return;
        }

        try
        {
            var psi = new ProcessStartInfo(exe, Environment.ProcessId.ToString())
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            _helper = Process.Start(psi);
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Error, "Failed to start SensorsHost.", ex);
            return;
        }

        _cts = new CancellationTokenSource();
        _pump = Task.Run(() => PumpAsync(new NamedPipeSensorFeed(), _cts.Token));
        _log.Log(LogLevel.Info, "SensorsHost started.");
    }

    private async Task PumpAsync(ISensorFeed feed, CancellationToken ct)
    {
        bool first = true;
        try
        {
            await foreach (SensorSample sample in feed.ReadAsync(ct))
            {
                _state.Apply(sample);
                if (first)
                {
                    first = false;
                    _log.Log(LogLevel.Info, "Sensor feed connected.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Error, "Sensor pump stopped.", ex);
        }
    }

    private static string? LocateHelper()
    {
        string baseDir = AppContext.BaseDirectory;
        string[] candidates =
        {
            // Deployed: the full helper is copied into a sensorshost\ subfolder.
            Path.Combine(baseDir, "sensorshost", "RSS_II_RGB.SensorsHost.exe"),
            // Dev fallback: the sibling project's bin (App and SensorsHost share the bin/<cfg>/<tfm> layout).
            Path.Combine(baseDir.Replace("RSS_II_RGB.App", "RSS_II_RGB.SensorsHost"), "RSS_II_RGB.SensorsHost.exe"),
        };
        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        if (_pump is not null)
        {
            try { await _pump.ConfigureAwait(false); } catch { /* shutting down */ }
        }
        try
        {
            if (_helper is { HasExited: false })
            {
                _helper.Kill();
            }
        }
        catch
        {
            // helper already gone
        }
        _helper?.Dispose();
        _cts?.Dispose();
    }
}
