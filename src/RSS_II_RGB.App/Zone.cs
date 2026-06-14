using RSS_II_RGB.Core.Rendering;

namespace RSS_II_RGB.App;

/// <summary>
/// A user-defined group of keys with its own effect — the Synapse-style override
/// layered on top of the global effect.
/// </summary>
internal sealed record Zone(IReadOnlyList<int> KeyIndices, EffectChoice Effect, Rgb Color);
