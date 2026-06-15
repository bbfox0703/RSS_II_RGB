using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using RSS_II_RGB.Core.Ipc;

namespace RSS_II_RGB.App;

/// <summary>The result of a GIF import attempt.</summary>
internal sealed record GifImportOutcome
{
    public bool Cancelled { get; init; }
    public string? Error { get; init; }
    public string? AnimPath { get; init; }
    public string? SourcePath { get; init; }
    public int[] Crop { get; init; } = Array.Empty<int>();
    public int FrameCount { get; init; }

    public bool Success => !Cancelled && Error is null && AnimPath is not null;

    public static GifImportOutcome WasCancelled => new() { Cancelled = true };
    public static GifImportOutcome Failed(string error) => new() { Error = error };
}

/// <summary>
/// Orchestrates importing a GIF as a keyboard animation: pick a file, choose a 4:1
/// crop, then bake it to a .kbanim via the non-AOT GifBakeHost helper. The image
/// library lives only in the helper, so the AOT app never decodes a GIF itself.
/// </summary>
internal static class GifImportService
{
    public static async Task<GifImportOutcome> ImportAsync(Window owner, string? lastSource, int[]? lastCrop,
                                                           Action<string>? onStatus = null)
    {
        IReadOnlyList<IStorageFile> files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = L.GifPickTitle,
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("GIF") { Patterns = new[] { "*.gif" } } },
        });
        if (files.Count == 0)
        {
            return GifImportOutcome.WasCancelled;
        }
        string? gifPath = files[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(gifPath))
        {
            return GifImportOutcome.Failed("The selected file has no local path.");
        }

        (Bitmap bitmap, PixelSize size)? preview = await LoadFirstFrameAsync(gifPath);
        if (preview is null)
        {
            return GifImportOutcome.Failed("Could not read the GIF.");
        }

        // Re-seed the crop only when re-cropping the same source.
        PixelRect? initial = null;
        if (lastCrop is { Length: 4 } && string.Equals(lastSource, gifPath, StringComparison.OrdinalIgnoreCase))
        {
            initial = new PixelRect(lastCrop[0], lastCrop[1], lastCrop[2], lastCrop[3]);
        }

        PixelRect? crop;
        using (Bitmap bmp = preview.Value.bitmap)
        {
            crop = await new GifCropWindow(bmp, preview.Value.size, initial).ShowDialog<PixelRect?>(owner);
        }
        if (crop is null)
        {
            return GifImportOutcome.WasCancelled;
        }

        // Baking can take a moment on large GIFs; let the UI show progress. The
        // previously imported animation keeps playing until the new one is ready.
        onStatus?.Invoke(L.GifProcessing);

        string outPath = OutputPath();
        var request = new GifBakeRequest
        {
            SourceGifPath = gifPath,
            OutputPath = outPath,
            CropX = crop.Value.X,
            CropY = crop.Value.Y,
            CropWidth = crop.Value.Width,
            CropHeight = crop.Value.Height,
        };

        GifBakeResult result = await RunBakeAsync(request);
        if (!result.Success)
        {
            return GifImportOutcome.Failed(result.Error ?? "Bake failed.");
        }

        return new GifImportOutcome
        {
            AnimPath = outPath,
            SourcePath = gifPath,
            Crop = new[] { crop.Value.X, crop.Value.Y, crop.Value.Width, crop.Value.Height },
            FrameCount = result.FrameCount,
        };
    }

    private static string OutputPath()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RSS_II_RGB", "anims");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "current.kbanim");
    }

    // Try Avalonia's built-in (Skia) decoder for the first frame; fall back to the
    // helper extracting a PNG if Avalonia can't open the GIF directly.
    private static async Task<(Bitmap bitmap, PixelSize size)?> LoadFirstFrameAsync(string gifPath)
    {
        try
        {
            var bmp = new Bitmap(gifPath);
            return (bmp, bmp.PixelSize);
        }
        catch
        {
            // Fall through to the helper preview.
        }

        string? exe = LocateHelper();
        if (exe is null)
        {
            return null;
        }

        string png = Path.Combine(Path.GetTempPath(), $"rss_gifprev_{Guid.NewGuid():N}.png");
        try
        {
            var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true };
            psi.ArgumentList.Add("--preview");
            psi.ArgumentList.Add(gifPath);
            psi.ArgumentList.Add(png);
            using (Process? proc = Process.Start(psi))
            {
                if (proc is null)
                {
                    return null;
                }
                await proc.WaitForExitAsync();
            }
            if (!File.Exists(png))
            {
                return null;
            }
            var bmp = new Bitmap(png); // reads fully into memory; the temp file can go
            return (bmp, bmp.PixelSize);
        }
        catch
        {
            return null;
        }
        finally
        {
            try { File.Delete(png); } catch { /* best effort */ }
        }
    }

    private static async Task<GifBakeResult> RunBakeAsync(GifBakeRequest request)
    {
        string? exe = LocateHelper();
        if (exe is null)
        {
            return new GifBakeResult { Success = false, Error = "GifBakeHost.exe not found." };
        }

        string reqPath = Path.Combine(Path.GetTempPath(), $"rss_gifbake_{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(reqPath,
            JsonSerializer.Serialize(request, GifBakeJsonContext.Default.GifBakeRequest));
        try
        {
            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            };
            psi.ArgumentList.Add(reqPath);

            using Process? proc = Process.Start(psi);
            if (proc is null)
            {
                return new GifBakeResult { Success = false, Error = "Could not start GifBakeHost." };
            }
            string stdout = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();

            return ParseResult(stdout) ?? new GifBakeResult { Success = false, Error = "No result from GifBakeHost." };
        }
        catch (Exception ex)
        {
            return new GifBakeResult { Success = false, Error = ex.Message };
        }
        finally
        {
            try { File.Delete(reqPath); } catch { /* best effort */ }
        }
    }

    private static GifBakeResult? ParseResult(string stdout)
    {
        foreach (string line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Reverse())
        {
            try
            {
                GifBakeResult? r = JsonSerializer.Deserialize(line, GifBakeJsonContext.Default.GifBakeResult);
                if (r is not null)
                {
                    return r;
                }
            }
            catch
            {
                // not the JSON line; keep looking
            }
        }
        return null;
    }

    private static string? LocateHelper()
    {
        string baseDir = AppContext.BaseDirectory;
        string[] candidates =
        {
            // Deployed: the full helper is copied into a gifbakehost\ subfolder.
            Path.Combine(baseDir, "gifbakehost", "RSS_II_RGB.GifBakeHost.exe"),
            // Dev fallback: the sibling project's bin (shared bin/<cfg>/<tfm> layout).
            Path.Combine(baseDir.Replace("RSS_II_RGB.App", "RSS_II_RGB.GifBakeHost"), "RSS_II_RGB.GifBakeHost.exe"),
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
}
