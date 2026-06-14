using System.Runtime.InteropServices;
using RSS_II_RGB.Core.Input;
using RSS_II_RGB.Core.Layout;
using RSS_II_RGB.Windows.Interop;

namespace RSS_II_RGB.Windows;

/// <summary>
/// Global key listener via a WH_KEYBOARD_LL hook. The hook lives on its own
/// thread with a message pump (LL hooks require one), so the UI/engine threads
/// stay free. Only key identity + timing leave this class — never key content.
/// </summary>
public sealed class Win32KeyboardHook : IKeyboardHook
{
    // The unmanaged hook proc must be static; route to the single live instance.
    private static Win32KeyboardHook? _instance;

    private readonly ManualResetEventSlim _installed = new(false);
    private readonly KeyboardProfile _profile;
    private readonly ScancodeResolver _resolver;
    private nint _hook;
    private Thread? _thread;
    private uint _threadId;

    public Win32KeyboardHook(KeyboardProfile profile)
    {
        _profile = profile;
        _resolver = new ScancodeResolver(profile);
    }

    public event Action<KeyEvent>? KeyChanged;

    /// <summary>True once the low-level hook is installed.</summary>
    public bool IsInstalled => _hook != 0;

    public Task StartAsync(CancellationToken ct = default)
    {
        if (_thread is not null)
        {
            return Task.CompletedTask;
        }

        _instance = this;
        _thread = new Thread(MessageLoop) { IsBackground = true, Name = "rss-kbd-hook" };
        _thread.Start();
        _installed.Wait(ct);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        if (_threadId != 0)
        {
            NativeMethods.PostThreadMessageW(_threadId, NativeMethods.WM_QUIT, 0, 0);
            _threadId = 0;
        }
        _thread?.Join(2000);
        _thread = null;
        if (_instance == this)
        {
            _instance = null;
        }
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _installed.Dispose();
    }

    private unsafe void MessageLoop()
    {
        _threadId = NativeMethods.GetCurrentThreadId();
        nint hmod = NativeMethods.GetModuleHandleW(null);
        _hook = NativeMethods.SetWindowsHookExW(
            NativeMethods.WH_KEYBOARD_LL,
            (nint)(delegate* unmanaged<int, nint, nint, nint>)&HookProc,
            hmod, 0);
        _installed.Set();

        if (_hook == 0)
        {
            return;
        }

        // Pump messages so the hook stays alive; WM_QUIT (from StopAsync) ends it.
        while (NativeMethods.GetMessageW(out NativeMethods.MSG _, 0, 0, 0) > 0)
        {
        }

        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = 0;
    }

    [UnmanagedCallersOnly]
    private static nint HookProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            _instance?.Dispatch((int)wParam, lParam);
        }
        return NativeMethods.CallNextHookEx(0, nCode, wParam, lParam);
    }

    private void Dispatch(int message, nint lParam)
    {
        bool down = message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
        bool up = message is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP;
        if (!down && !up)
        {
            return;
        }

        uint scanCode = (uint)Marshal.ReadInt32(lParam, 4);
        uint flags = (uint)Marshal.ReadInt32(lParam, 8);
        bool extended = (flags & NativeMethods.LLKHF_EXTENDED) != 0;

        int index = _resolver.ToKeyIndex(scanCode, extended);
        byte keyId = index >= 0 ? _profile.ByIndex(index).KeyId : (byte)0;

        KeyChanged?.Invoke(new KeyEvent(index, keyId, down, Environment.TickCount64));
    }
}
