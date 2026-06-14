namespace RSS_II_RGB.Core.Rendering;

/// <summary>A 24-bit RGB colour. Immutable, blittable value type.</summary>
public readonly record struct Rgb(byte R, byte G, byte B)
{
    public static readonly Rgb Black = new(0, 0, 0);
    public static readonly Rgb White = new(255, 255, 255);

    /// <summary>Scale every channel by <paramref name="k"/> (clamped to 0..255).</summary>
    public Rgb Scale(float k) => new(ScaleChannel(R, k), ScaleChannel(G, k), ScaleChannel(B, k));

    /// <summary>Linear interpolation between two colours; <paramref name="t"/> clamped to 0..1.</summary>
    public static Rgb Lerp(Rgb a, Rgb b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Rgb(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    /// <summary>HSV (all components 0..1) to RGB. Used by rainbow/wave generators.</summary>
    public static Rgb FromHsv(double h, double s, double v)
    {
        h -= Math.Floor(h); // wrap into [0,1)
        double r = 0, g = 0, b = 0;
        int i = (int)(h * 6) % 6;
        double f = h * 6 - Math.Floor(h * 6);
        double p = v * (1 - s);
        double q = v * (1 - f * s);
        double t = v * (1 - (1 - f) * s);
        switch (i)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            case 5: r = v; g = p; b = q; break;
        }
        return new Rgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    private static byte ScaleChannel(byte c, float k)
    {
        int v = (int)MathF.Round(c * k);
        return (byte)Math.Clamp(v, 0, 255);
    }
}
