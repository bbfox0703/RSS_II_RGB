namespace RSS_II_RGB.Core.Input;

/// <summary>
/// A physical key transition observed by the platform keyboard hook.
/// <see cref="KeyIndex"/> is the render index (see ScopeIILayout), or -1 if the
/// pressed key has no LED.
/// </summary>
public readonly record struct KeyEvent(int KeyIndex, byte KeyId, bool IsDown, long TimestampMs);
