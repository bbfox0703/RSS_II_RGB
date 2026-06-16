using RSS_II_RGB.Core.Layout;
using RSS_II_RGB.Core.Rendering;

namespace RSS_II_RGB.Core.Effects.Layers;

/// <summary>
/// A twinkling night sky. Independent point-lights ("stars") wink on across the
/// masked keys, scintillate with one of several flicker patterns (modelling how
/// the atmosphere makes real starlight shimmer), then fade and die — each reborn
/// somewhere else with a fresh colour and brightness. Many stars live at once.
///
/// Placement rules mirror a real sky: one LED holds at most one star, a new star
/// never lands on a neighbour of a live one, and the farther a key sits from every
/// live star the likelier it is to be chosen — so the field stays sparse and
/// keeps drifting around the keyboard.
///
/// Opaque base layer: unlit keys stay black (the night sky), so it composes like
/// any other base effect, both globally and per zone.
/// </summary>
public sealed class StarlightLayer : IEffectLayer
{
    /// <summary>The flicker styles a star can be born with (its "scintillation").</summary>
    private enum Twinkle
    {
        Steady,      // a calm, planet-like glow with a slow gentle breath
        Scintillate, // irregular shimmer — several incommensurate sines summed
        Sparkle,     // mostly dim with sharp, brief flashes
        Pulse,       // a fixed number of distinct blinks across its life
    }

    private sealed class Star
    {
        public int KeyIndex;
        public int Row;
        public int Col;
        public Rgb Color;      // colour at full brightness
        public float Peak;     // peak intensity, 0..1
        public double Birth;   // engine seconds at spawn
        public double Life;    // total lifetime, seconds
        public Twinkle Pattern;
        public double F1, F2, F3; // flicker frequencies (Hz)
        public double P1, P2, P3; // flicker phases (rad)
        public int Pulses;        // discrete blinks for the Pulse pattern
    }

    // ----- Tuning -----
    // Density sets how many stars share the sky at once (a fraction of the masked
    // key count); the rest shape lifetime, flicker, spacing, and birth cadence.
    private const double Density = 0.12;       // ~12% of the masked keys lit at once
    private const double MinLife = 2.5;        // star lifetime, seconds
    private const double MaxLife = 6.0;
    private const double FadeInFrac = 0.12;    // share of life spent rising in
    private const double FadeOutFrac = 0.30;   // share of life spent dying out
    private const double MinSpawnGap = 0.06;   // seconds between births (staggers them)
    private const double MaxSpawnGap = 0.45;
    private const double MinSeparation = 1.5;  // no star within this grid distance of a live one
    private const double SpreadPower = 2.0;    // spawn weight = (distance to nearest star)^power
    private const float MinPeak = 0.45f;       // dimmest a star's peak can be
    private const double MaxStep = 0.1;        // clamp the per-tick advance (e.g. after a stall)
    private const double Tau = 2 * Math.PI;

    private readonly Random _rng;
    private readonly List<Star> _stars = new();
    private int[]? _keys;       // the masked LED indices, built lazily on first render
    private double _spawnCooldown;

    public StarlightLayer(string id, KeyMask mask, int zOrder = 0, Random? rng = null)
    {
        Id = id;
        Mask = mask;
        ZOrder = zOrder;
        _rng = rng ?? new Random();
    }

    public string Id { get; }
    public int ZOrder { get; }
    public BlendMode Blend => BlendMode.Normal;
    public KeyMask Mask { get; }
    public bool IsComplete => false;

    public void Render(Span<Rgb> target, in EffectContext ctx)
    {
        EnsureKeys(target.Length);
        double now = ctx.Elapsed.TotalSeconds;
        double dt = Math.Clamp(ctx.Delta.TotalSeconds, 0, MaxStep);

        // Cull stars that have lived out their lifetime.
        for (int i = _stars.Count - 1; i >= 0; i--)
        {
            if (now - _stars[i].Birth >= _stars[i].Life)
            {
                _stars.RemoveAt(i);
            }
        }

        // Birth new ones, staggered, up to the target population.
        _spawnCooldown -= dt;
        if (_stars.Count < TargetCount && _spawnCooldown <= 0 && TrySpawn(now, ctx.Layout))
        {
            _spawnCooldown = MinSpawnGap + _rng.NextDouble() * (MaxSpawnGap - MinSpawnGap);
        }

        // Paint. The compositor pre-clears the scratch to black, so unlit keys are
        // already the night sky — we only light the live stars.
        foreach (Star s in _stars)
        {
            float intensity = Intensity(s, now);
            if (intensity > 0f)
            {
                target[s.KeyIndex] = s.Color.Scale(intensity);
            }
        }
    }

    private int TargetCount
        => _keys is { Length: > 0 } ? Math.Max(1, (int)Math.Round(_keys.Length * Density)) : 0;

    private void EnsureKeys(int length)
    {
        if (_keys is not null)
        {
            return;
        }
        var list = new List<int>();
        for (int i = 0; i < length; i++)
        {
            if (Mask.Contains(i))
            {
                list.Add(i);
            }
        }
        _keys = list.ToArray();
    }

    // Choose a key for a new star and add it. Keys too close to a live star are
    // ineligible; among the rest, weight ∝ distance-to-nearest-star^SpreadPower so
    // the farther a key sits from every star the more likely it is to be chosen.
    private bool TrySpawn(double now, KeyboardProfile layout)
    {
        int[] keys = _keys!;
        if (keys.Length == 0)
        {
            return false;
        }

        Span<double> weights = stackalloc double[keys.Length];
        double total = 0;
        for (int k = 0; k < keys.Length; k++)
        {
            ref readonly LedKey key = ref layout.ByIndex(keys[k]);
            double nearest = double.PositiveInfinity;
            foreach (Star s in _stars)
            {
                double dr = key.Row - s.Row, dc = key.Col - s.Col;
                double d = Math.Sqrt(dr * dr + dc * dc);
                if (d < nearest)
                {
                    nearest = d;
                }
            }

            double w;
            if (nearest <= MinSeparation)
            {
                w = 0; // occupied key or a neighbour of a live star
            }
            else if (double.IsPositiveInfinity(nearest))
            {
                w = 1; // empty sky → every key equally likely
            }
            else
            {
                w = Math.Pow(nearest, SpreadPower);
            }
            weights[k] = w;
            total += w;
        }

        if (total <= 0)
        {
            return false; // nowhere legal to put one (e.g. a tiny or crowded zone)
        }

        double pick = _rng.NextDouble() * total;
        int chosen = keys[^1];
        for (int k = 0; k < keys.Length; k++)
        {
            pick -= weights[k];
            if (pick <= 0)
            {
                chosen = keys[k];
                break;
            }
        }

        _stars.Add(NewStar(chosen, now, layout));
        return true;
    }

    private Star NewStar(int keyIndex, double now, KeyboardProfile layout)
    {
        ref readonly LedKey key = ref layout.ByIndex(keyIndex);
        double hue = _rng.NextDouble();
        double sat = 0.35 + _rng.NextDouble() * 0.6; // some near-white, some vivid
        var pattern = (Twinkle)_rng.Next(4);
        return new Star
        {
            KeyIndex = keyIndex,
            Row = key.Row,
            Col = key.Col,
            Color = Rgb.FromHsv(hue, sat, 1.0),
            Peak = MinPeak + (float)_rng.NextDouble() * (1f - MinPeak),
            Birth = now,
            Life = MinLife + _rng.NextDouble() * (MaxLife - MinLife),
            Pattern = pattern,
            F1 = Freq(pattern, 0),
            F2 = Freq(pattern, 1),
            F3 = Freq(pattern, 2),
            P1 = _rng.NextDouble() * Tau,
            P2 = _rng.NextDouble() * Tau,
            P3 = _rng.NextDouble() * Tau,
            Pulses = 1 + _rng.Next(5), // 1..5 blinks — a star need not flicker just once
        };
    }

    // Per-pattern flicker frequency bands (Hz), randomised within each band so that
    // even two stars sharing a pattern shimmer differently.
    private double Freq(Twinkle pattern, int which)
    {
        (double lo, double hi) = pattern switch
        {
            Twinkle.Steady => (0.4, 1.2),
            Twinkle.Scintillate => which switch { 0 => (2.0, 4.0), 1 => (4.0, 7.0), _ => (7.0, 11.0) },
            Twinkle.Sparkle => (1.5, 3.5),
            _ => (0.5, 1.5),
        };
        return lo + _rng.NextDouble() * (hi - lo);
    }

    private static float Intensity(Star s, double now)
    {
        double age = now - s.Birth;
        if (age < 0 || age >= s.Life)
        {
            return 0f;
        }
        double age01 = age / s.Life;

        // Birth/death envelope: smoothstep ramps in and out, full in the middle.
        double env;
        if (age01 < FadeInFrac)
        {
            env = Smooth(age01 / FadeInFrac);
        }
        else if (age01 > 1 - FadeOutFrac)
        {
            env = Smooth((1 - age01) / FadeOutFrac);
        }
        else
        {
            env = 1.0;
        }

        double t = age; // seconds since birth
        double twk = s.Pattern switch
        {
            Twinkle.Steady => 0.78 + 0.22 * Osc(s.F1, s.P1, t),
            Twinkle.Scintillate =>
                0.35 + 0.65 * (0.5 + 0.5 * (0.5 * Sin(s.F1, s.P1, t)
                                          + 0.3 * Sin(s.F2, s.P2, t)
                                          + 0.2 * Sin(s.F3, s.P3, t))),
            Twinkle.Sparkle => 0.12 + 0.88 * Math.Pow(Osc(s.F1, s.P1, t), 6),
            _ => Pulse(age01, s.Pulses),
        };

        return (float)Math.Clamp(env * twk * s.Peak, 0.0, 1.0);
    }

    private static double Sin(double f, double p, double t) => Math.Sin(Tau * f * t + p);
    private static double Osc(double f, double p, double t) => 0.5 + 0.5 * Math.Sin(Tau * f * t + p); // 0..1

    private static double Smooth(double x)
    {
        x = Math.Clamp(x, 0, 1);
        return x * x * (3 - 2 * x);
    }

    // A run of N smooth 0→1→0 humps across the star's life — N distinct blinks.
    private static double Pulse(double age01, int pulses)
    {
        double frac = age01 * pulses;
        frac -= Math.Floor(frac);
        double hump = Math.Sin(Math.PI * frac);
        return hump * hump;
    }
}
