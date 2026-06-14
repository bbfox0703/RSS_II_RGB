namespace RSS_II_RGB.Core.Input;

/// <summary>
/// Global physical-key listener. The platform seam for input (CLAUDE.md rule 4);
/// the Windows implementation uses a low-level keyboard hook. Only key identity
/// and timing are exposed — never key content (privacy: it must not be a keylogger).
/// </summary>
public interface IKeyboardHook : IAsyncDisposable
{
    /// <summary>Raised on the hook thread for every key transition.</summary>
    event Action<KeyEvent>? KeyChanged;

    Task StartAsync(CancellationToken ct = default);

    Task StopAsync(CancellationToken ct = default);
}
