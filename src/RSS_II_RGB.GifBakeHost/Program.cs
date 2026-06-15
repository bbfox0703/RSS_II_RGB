using System.Text.Json;
using ImageMagick;
using RSS_II_RGB.Core;
using RSS_II_RGB.Core.Animation;
using RSS_II_RGB.Core.Ipc;
using RSS_II_RGB.Core.Rendering;

namespace RSS_II_RGB.GifBakeHost;

// Non-AOT helper: one-shot GIF -> .kbanim bake. Reads a GifBakeRequest JSON file,
// decodes/crops/downsamples with Magick.NET, writes the baked frames, and prints a
// GifBakeResult JSON line to stdout. Magick.NET stays OUT of the AOT main app.
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            // Preview mode: extract the first frame as a PNG the Avalonia crop window
            // can always load (fallback if Avalonia can't decode the GIF directly).
            if (args.Length >= 3 && args[0] == "--preview")
            {
                return WritePreview(args[1], args[2]);
            }
            if (args.Length < 1)
            {
                return Fail("Usage: GifBakeHost <request.json> | --preview <gif> <png>");
            }
            return Bake(args[0]);
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    private static int Bake(string requestPath)
    {
        string json = File.ReadAllText(requestPath);
        GifBakeRequest? req = JsonSerializer.Deserialize(json, GifBakeJsonContext.Default.GifBakeRequest);
        if (req is null)
        {
            return Fail("Could not parse the bake request.");
        }

        int cols = req.Cols > 0 ? req.Cols : CoreConstants.MatrixCols;
        int rows = req.Rows > 0 ? req.Rows : CoreConstants.MatrixRows;

        byte[]? gammaLut = BuildGammaLut(req.Gamma);

        using var collection = new MagickImageCollection(req.SourceGifPath);
        collection.Coalesce(); // each frame becomes a full, absolute canvas

        var frames = new List<KbAnim.Frame>(collection.Count);
        int totalMs = 0;

        foreach (IMagickImage<byte> image in collection)
        {
            image.Crop(ClampCrop(req, image));
            image.ResetPage();
            image.FilterType = FilterType.Triangle;
            image.Resize(new MagickGeometry((uint)cols, (uint)rows) { IgnoreAspectRatio = true });

            // Flatten any GIF transparency onto black so unlit areas read as "off".
            image.BackgroundColor = MagickColors.Black;
            image.Alpha(AlphaOption.Remove);

            var pixels = new Rgb[cols * rows];
            using (IPixelCollection<byte> px = image.GetPixels())
            {
                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < cols; x++)
                    {
                        IMagickColor<byte>? c = px.GetPixel(x, y).ToColor();
                        byte r = c?.R ?? 0;
                        byte g = c?.G ?? 0;
                        byte b = c?.B ?? 0;
                        if (gammaLut is not null)
                        {
                            r = gammaLut[r];
                            g = gammaLut[g];
                            b = gammaLut[b];
                        }
                        pixels[y * cols + x] = new Rgb(r, g, b);
                    }
                }
            }

            int ms = DelayMs(image);
            totalMs += ms;
            frames.Add(new KbAnim.Frame((ushort)Math.Clamp(ms, 1, ushort.MaxValue), pixels));
        }

        if (frames.Count == 0)
        {
            return Fail("The GIF contained no frames.");
        }

        var anim = new KbAnim(cols, rows, frames);
        anim.Save(req.OutputPath);

        return Ok(new GifBakeResult { Success = true, FrameCount = frames.Count, DurationMs = totalMs });
    }

    private static int WritePreview(string gifPath, string pngPath)
    {
        using var collection = new MagickImageCollection(gifPath);
        collection.Coalesce();
        if (collection.Count == 0)
        {
            return Fail("The GIF contained no frames.");
        }
        collection[0].Write(pngPath, MagickFormat.Png);
        return Ok(new GifBakeResult { Success = true, FrameCount = collection.Count });
    }

    /// <summary>Clamp the requested crop rectangle to the image so a stale rect can't throw.</summary>
    private static MagickGeometry ClampCrop(GifBakeRequest req, IMagickImage<byte> image)
    {
        int iw = (int)image.Width;
        int ih = (int)image.Height;
        int x = Math.Clamp(req.CropX, 0, Math.Max(0, iw - 1));
        int y = Math.Clamp(req.CropY, 0, Math.Max(0, ih - 1));
        int w = Math.Clamp(req.CropWidth <= 0 ? iw : req.CropWidth, 1, iw - x);
        int h = Math.Clamp(req.CropHeight <= 0 ? ih : req.CropHeight, 1, ih - y);
        return new MagickGeometry(x, y, (uint)w, (uint)h);
    }

    /// <summary>One frame's display time in milliseconds.</summary>
    private static int DelayMs(IMagickImage<byte> image)
    {
        int ticks = image.AnimationTicksPerSecond;
        uint delay = image.AnimationDelay;
        int ms = ticks > 0
            ? (int)Math.Round(delay * 1000.0 / ticks)
            : (int)(delay * 10); // assume centiseconds (the GIF default of 100 ticks/sec)
        return ms <= 0 ? 100 : ms; // 0-delay frames render at the conventional ~100 ms
    }

    private static byte[]? BuildGammaLut(double gamma)
    {
        if (Math.Abs(gamma - 1.0) < 0.001 || gamma <= 0)
        {
            return null;
        }
        var lut = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            lut[i] = (byte)Math.Clamp(Math.Round(255.0 * Math.Pow(i / 255.0, gamma)), 0, 255);
        }
        return lut;
    }

    private static int Ok(GifBakeResult result)
    {
        Console.Out.WriteLine(JsonSerializer.Serialize(result, GifBakeJsonContext.Default.GifBakeResult));
        return 0;
    }

    private static int Fail(string error)
    {
        var result = new GifBakeResult { Success = false, Error = error };
        Console.Out.WriteLine(JsonSerializer.Serialize(result, GifBakeJsonContext.Default.GifBakeResult));
        return 1;
    }
}
