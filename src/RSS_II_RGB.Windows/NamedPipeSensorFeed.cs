using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using RSS_II_RGB.Core;
using RSS_II_RGB.Core.Ipc;

namespace RSS_II_RGB.Windows;

/// <summary>
/// Reads <see cref="SensorSample"/> JSON lines from the SensorsHost over a named
/// pipe, reconnecting if the helper restarts. The reflection-free
/// <see cref="SensorJsonContext"/> keeps this AOT-safe in the main app.
/// </summary>
public sealed class NamedPipeSensorFeed : ISensorFeed
{
    public async IAsyncEnumerable<SensorSample> ReadAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeClientStream? client = await ConnectAsync(ct);
            if (client is null)
            {
                if (!await DelayAsync(1000, ct)) yield break;
                continue;
            }

            using (client)
            using (var reader = new StreamReader(client, Encoding.UTF8))
            {
                while (!ct.IsCancellationRequested)
                {
                    LineResult result = await ReadLineAsync(reader, ct);
                    if (result.Eof)
                    {
                        break;
                    }
                    if (result.Sample is SensorSample sample)
                    {
                        yield return sample;
                    }
                }
            }

            if (!await DelayAsync(1000, ct)) yield break; // reconnect backoff
        }
    }

    private static async Task<NamedPipeClientStream?> ConnectAsync(CancellationToken ct)
    {
        var client = new NamedPipeClientStream(".", CoreConstants.SensorPipeName,
            PipeDirection.In, PipeOptions.Asynchronous);
        try
        {
            await client.ConnectAsync(2000, ct);
            return client;
        }
        catch
        {
            client.Dispose();
            return null;
        }
    }

    private readonly record struct LineResult(SensorSample? Sample, bool Eof);

    private static async Task<LineResult> ReadLineAsync(StreamReader reader, CancellationToken ct)
    {
        string? line;
        try
        {
            line = await reader.ReadLineAsync(ct);
        }
        catch
        {
            return new LineResult(null, Eof: true);
        }

        if (line is null)
        {
            return new LineResult(null, Eof: true);
        }

        try
        {
            return new LineResult(JsonSerializer.Deserialize(line, SensorJsonContext.Default.SensorSample), Eof: false);
        }
        catch
        {
            return new LineResult(null, Eof: false); // skip a bad line
        }
    }

    private static async Task<bool> DelayAsync(int ms, CancellationToken ct)
    {
        try
        {
            await Task.Delay(ms, ct);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
