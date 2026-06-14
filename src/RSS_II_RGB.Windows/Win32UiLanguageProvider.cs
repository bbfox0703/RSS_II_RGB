using System.Runtime.InteropServices;
using RSS_II_RGB.Core.Localization;

namespace RSS_II_RGB.Windows;

/// <summary>
/// Picks the UI language from the Windows display language
/// (<c>GetUserDefaultUILanguage</c>). Traditional Chinese (Taiwan / Hong Kong /
/// Macau) → Traditional Chinese; everything else falls back to English.
/// Source-generated P/Invoke, AOT-safe.
/// </summary>
public sealed partial class Win32UiLanguageProvider : IUiLanguageProvider
{
    public AppLanguage Detect()
    {
        if (!OperatingSystem.IsWindows())
        {
            return AppLanguage.English;
        }

        ushort langId = GetUserDefaultUILanguage();
        int primary = langId & 0x3FF;   // primary language; Chinese == 0x04
        int sub = langId >> 10;         // Traditional sub-languages: TW=1, HK=3, MO=5
        bool traditional = primary == 0x04 && (sub == 1 || sub == 3 || sub == 5);
        return traditional ? AppLanguage.TraditionalChinese : AppLanguage.English;
    }

    [LibraryImport("kernel32.dll")]
    private static partial ushort GetUserDefaultUILanguage();
}
