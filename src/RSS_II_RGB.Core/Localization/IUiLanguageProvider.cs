namespace RSS_II_RGB.Core.Localization;

/// <summary>
/// Reports the UI language to display, derived from the OS (CLAUDE.md rule 4 —
/// the OS call lives in the Windows project, Core stays platform-agnostic).
/// </summary>
public interface IUiLanguageProvider
{
    AppLanguage Detect();
}
