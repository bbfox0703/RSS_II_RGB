using RSS_II_RGB.Core.Rendering;

namespace RSS_II_RGB.Core.Animation;

/// <summary>
/// A pre-baked keyboard animation: a sequence of low-resolution RGB frames on a
/// fixed Cols×Rows grid (row-major), each with its own display delay. Produced
/// offline by the non-AOT GifBakeHost helper (GIF decode + crop + downsample) and
/// played back by the GifLayer. The on-disk form is a tiny, dependency-free binary
/// blob so the AOT main app never links an image library (CLAUDE.md rule 5).
///
/// Layout: magic "KBAN", version, cols, rows, frame count (u16), then per frame a
/// delay (u16 ms) followed by cols*rows RGB triples. Multi-byte values are
/// little-endian (BinaryWriter's native encoding).
/// </summary>
public sealed class KbAnim
{
    public const byte CurrentVersion = 1;

    private readonly int[] _frameEndMs; // cumulative end time (exclusive) of each frame

    public KbAnim(int cols, int rows, IReadOnlyList<Frame> frames)
    {
        if (cols <= 0 || rows <= 0)
        {
            throw new ArgumentException("Grid dimensions must be positive.");
        }
        if (frames.Count == 0)
        {
            throw new ArgumentException("An animation needs at least one frame.", nameof(frames));
        }

        Cols = cols;
        Rows = rows;
        Frames = frames;

        _frameEndMs = new int[frames.Count];
        int acc = 0;
        for (int i = 0; i < frames.Count; i++)
        {
            if (frames[i].Pixels.Length != cols * rows)
            {
                throw new ArgumentException($"Frame {i} has {frames[i].Pixels.Length} pixels, expected {cols * rows}.");
            }
            // Guarantee forward progress even if a frame declares a zero delay.
            acc += Math.Max(1, (int)frames[i].DelayMs);
            _frameEndMs[i] = acc;
        }
        TotalDurationMs = acc;
    }

    /// <summary>Grid width (columns), matching the keyboard matrix column count.</summary>
    public int Cols { get; }

    /// <summary>Grid height (rows), matching the keyboard matrix row count.</summary>
    public int Rows { get; }

    public IReadOnlyList<Frame> Frames { get; }

    /// <summary>Sum of all frame delays; the loop period.</summary>
    public int TotalDurationMs { get; }

    /// <summary>One frame: a Cols*Rows RGB grid (row-major: row*Cols + col) plus its display delay.</summary>
    public sealed class Frame
    {
        public Frame(ushort delayMs, Rgb[] pixels)
        {
            DelayMs = delayMs;
            Pixels = pixels;
        }

        public ushort DelayMs { get; }
        public Rgb[] Pixels { get; }
    }

    /// <summary>The frame visible at <paramref name="elapsed"/>, looping over the whole animation.</summary>
    public Frame FrameAt(TimeSpan elapsed)
    {
        if (Frames.Count == 1 || TotalDurationMs <= 0)
        {
            return Frames[0];
        }

        int t = (int)(((long)elapsed.TotalMilliseconds % TotalDurationMs + TotalDurationMs) % TotalDurationMs);
        for (int i = 0; i < _frameEndMs.Length; i++)
        {
            if (t < _frameEndMs[i])
            {
                return Frames[i];
            }
        }
        return Frames[^1];
    }

    public void Save(string path)
    {
        using FileStream fs = File.Create(path);
        Save(fs);
    }

    public void Save(Stream stream)
    {
        using var w = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        w.Write((byte)'K');
        w.Write((byte)'B');
        w.Write((byte)'A');
        w.Write((byte)'N');
        w.Write(CurrentVersion);
        w.Write((byte)Cols);
        w.Write((byte)Rows);
        w.Write((ushort)Frames.Count);
        foreach (Frame frame in Frames)
        {
            w.Write(frame.DelayMs);
            foreach (Rgb px in frame.Pixels)
            {
                w.Write(px.R);
                w.Write(px.G);
                w.Write(px.B);
            }
        }
    }

    public static KbAnim Load(string path)
    {
        using FileStream fs = File.OpenRead(path);
        return Load(fs);
    }

    public static KbAnim Load(Stream stream)
    {
        using var r = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        if (r.ReadByte() != 'K' || r.ReadByte() != 'B' || r.ReadByte() != 'A' || r.ReadByte() != 'N')
        {
            throw new InvalidDataException("Not a .kbanim file (bad magic).");
        }
        byte version = r.ReadByte();
        if (version != CurrentVersion)
        {
            throw new InvalidDataException($"Unsupported .kbanim version {version}.");
        }
        int cols = r.ReadByte();
        int rows = r.ReadByte();
        int count = r.ReadUInt16();
        int pixelsPerFrame = cols * rows;

        var frames = new Frame[count];
        for (int i = 0; i < count; i++)
        {
            ushort delay = r.ReadUInt16();
            var pixels = new Rgb[pixelsPerFrame];
            for (int p = 0; p < pixelsPerFrame; p++)
            {
                byte red = r.ReadByte();
                byte green = r.ReadByte();
                byte blue = r.ReadByte();
                pixels[p] = new Rgb(red, green, blue);
            }
            frames[i] = new Frame(delay, pixels);
        }
        return new KbAnim(cols, rows, frames);
    }
}
