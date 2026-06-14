using RSS_II_RGB.Core.Layout;

namespace RSS_II_RGB.Core.Input;

/// <summary>
/// Static Scope II convenience over <see cref="ScancodeResolver"/> — the default
/// keyboard's scan-code → render-index map. The per-device render path builds its
/// own <see cref="ScancodeResolver"/> from the connected keyboard's profile; this
/// stays as the canonical Scope II reference.
/// </summary>
public static class ScancodeMap
{
    private static readonly ScancodeResolver _scopeII = new(ScopeIILayout.Profile);

    /// <summary>Scope II render index for a (scan code, extended) press, or -1.</summary>
    public static int ToKeyIndex(uint scanCode, bool extended) => _scopeII.ToKeyIndex(scanCode, extended);
}
