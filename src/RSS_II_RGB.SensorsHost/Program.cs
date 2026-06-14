using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using RSS_II_RGB.Core;
using RSS_II_RGB.Core.Ipc;

namespace RSS_II_RGB.SensorsHost;

// Non-AOT helper: streams SensorSample JSON lines to the main app over a named
// pipe. Reflection-heavy deps (NAudio now, LibreHardwareMonitor later) stay OUT
// of the AOT main app.
internal static class Program
{
    private static readonly ISensorProvider[] Providers =
    {
        new SyntheticTempProvider(),
        new AudioProvider(),
    };

    private static async Task Main(string[] args)
    {
        // Exit if the parent app goes away (no orphaned helper holding the pipe).
        if (args.Length > 0 && int.TryParse(args[0], out int parentPid))
        {
            _ = WatchParentAsync(parentPid);
        }

        foreach (ISensorProvider provider in Providers)
        {
            provider.Start();
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

        while (server.IsConnected)
        {
            foreach (ISensorProvider provider in Providers)
            {
                foreach (SensorSample sample in provider.Poll())
                {
                    string json = JsonSerializer.Serialize(sample, SensorJsonContext.Default.SensorSample);
                    await writer.WriteLineAsync(json);
                }
            }
            await Task.Delay(33); // ~30 Hz
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
