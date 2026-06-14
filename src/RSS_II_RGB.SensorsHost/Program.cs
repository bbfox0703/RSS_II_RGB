using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using RSS_II_RGB.Core;
using RSS_II_RGB.Core.Ipc;

namespace RSS_II_RGB.SensorsHost;

// Non-AOT helper: streams SensorSample JSON lines to the main app over a named
// pipe. Phase A uses a synthetic source; real CPU/GPU temp (LibreHardwareMonitor)
// and audio (WASAPI/FFT) plug in here later. Reflection-heavy deps stay OUT of
// the AOT main app.
internal static class Program
{
    private static async Task Main(string[] args)
    {
        // Exit if the parent app goes away (no orphaned helper holding the pipe).
        if (args.Length > 0 && int.TryParse(args[0], out int parentPid))
        {
            _ = WatchParentAsync(parentPid);
        }

        while (true)
        {
            try
            {
                await ServeOneClientAsync();
            }
            catch
            {
                await Task.Delay(500);
            }
        }
    }

    private static async Task ServeOneClientAsync()
    {
        using var server = new NamedPipeServerStream(
            CoreConstants.SensorPipeName, PipeDirection.Out, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

        await server.WaitForConnectionAsync();

        using var writer = new StreamWriter(server, new UTF8Encoding(false)) { AutoFlush = true };
        var source = new SyntheticSensorSource();

        while (server.IsConnected)
        {
            foreach (SensorSample sample in source.Read())
            {
                string json = JsonSerializer.Serialize(sample, SensorJsonContext.Default.SensorSample);
                await writer.WriteLineAsync(json);
            }
            await Task.Delay(100);
        }
    }

    private static async Task WatchParentAsync(int parentPid)
    {
        try
        {
            Process parent = Process.GetProcessById(parentPid);
            await parent.WaitForExitAsync();
            Environment.Exit(0); // exit only once the parent has actually exited
        }
        catch
        {
            // Couldn't attach to the parent — keep running rather than exit.
        }
    }
}

/// <summary>Placeholder source: oscillating CPU/GPU temperatures so the pipeline can be verified.</summary>
internal sealed class SyntheticSensorSource
{
    private double _t;

    public IEnumerable<SensorSample> Read()
    {
        long now = Environment.TickCount64;
        double cpu = 55 + 25 * Math.Sin(_t);            // 30..80 °C
        double gpu = 50 + 20 * Math.Sin(_t * 0.7 + 1.0); // 30..70 °C
        _t += 0.15;

        yield return new SensorSample(SensorKind.CpuTemp, new[] { cpu }, now);
        yield return new SensorSample(SensorKind.GpuTemp, new[] { gpu }, now);
    }
}
