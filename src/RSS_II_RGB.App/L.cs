using RSS_II_RGB.Core.Localization;

namespace RSS_II_RGB.App;

/// <summary>
/// UI string table. The language is chosen once at startup from the OS and never
/// changes during a session, so each string is just a language switch — no
/// ResourceManager, satellite assemblies, or reflection (AOT-safe). Brand names
/// (e.g. "Strix Scope II RGB") are intentionally not translated.
/// </summary>
internal static class L
{
    /// <summary>Set once at startup (see <see cref="Program"/>).</summary>
    public static AppLanguage Language { get; set; } = AppLanguage.English;

    private static bool Zh => Language == AppLanguage.TraditionalChinese;

    private static string S(string en, string zh) => Zh ? zh : en;

    // ----- Main window -----
    public static string EffectLabel => S("Effect", "效果");
    public static string OverlaysLabel => S("Overlays", "疊加層");
    public static string ReactiveOverlay => S("Reactive — keypress flare + ripple", "反應 — 按鍵閃光 + 漣漪");
    public static string AudioOverlay => S("Audio — frequency spectrum", "音訊 — 頻譜");
    public static string PriorityHint =>
        S("Reactive sits above audio; system metrics sit above both.",
          "反應位於音訊之上；系統指標位於兩者之上。");
    public static string BrightnessFormat => S("Brightness: {0:0}%", "亮度：{0:0}%");
    public static string AudioSensitivityFormat => S("Audio sensitivity: {0:0.0}×", "音訊靈敏度：{0:0.0}×");
    public static string ColourLabel => S("Colour", "顏色");
    public static string ColorRed => S("Red", "紅");
    public static string ColorGreen => S("Green", "綠");
    public static string ColorBlue => S("Blue", "藍");
    public static string ColorCyan => S("Cyan", "青");
    public static string ColorMagenta => S("Magenta", "洋紅");
    public static string ColorWhite => S("White", "白");
    public static string ZoneEditorButton => S("Zone editor…", "區域編輯器…");
    public static string StartWithWindows =>
        S("Start with Windows (minimised to tray)", "開機自動啟動（最小化到系統匣）");
    public static string SystemMetricsHeader => S("System metrics (overlay)", "系統指標（疊加）");
    public static string ShowMetricBars => S("Show metric bars on the keyboard", "在鍵盤上顯示指標列");
    public static string LayoutLabel => S("Layout", "版面");
    public static string UtilisationThresholds =>
        S("Utilisation % thresholds (→ 1 / 2 / 3 / 4 cells)", "使用率 % 門檻（→ 1 / 2 / 3 / 4 格）");
    public static string GpuTempThresholds => S("GPU temperature °C thresholds", "GPU 溫度 °C 門檻");
    public static string MetricMappingHint =>
        S("CPU%→first group, Mem%→second, GPU%→third, GPU temp→last.",
          "CPU%→第一組，記憶體%→第二組，GPU%→第三組，GPU 溫度→最後。");
    public static string MainTip =>
        S("Tip: tick 'Reactive' and type — each key flares and ripples on top of your effect.",
          "提示：勾選「反應」並打字 — 每個按鍵會在你的效果之上閃光並擴散漣漪。");

    // ----- Zone editor -----
    public static string ZoneEditorTitle => S("Zone Editor", "區域編輯器");
    public static string ZoneEffectLabel => S("Zone effect", "區域效果");
    public static string AudioZoneModeTooltip => S("Audio zone mode", "音訊區域模式");
    public static string AssignToSelection => S("Assign to selection", "套用到選取");
    public static string SelectAll => S("Select all", "全選");
    public static string ClearSelection => S("Clear selection", "清除選取");
    public static string RemoveAllZones => S("Remove all zones", "移除所有區域");
    public static string ZoneEditorHint =>
        S("Click keys to toggle, or drag a box to select a range. Then pick an effect + colour and Assign.",
          "點擊按鍵切換，或拖曳方框選取範圍。接著選擇效果 + 顏色並套用。");
    public static string ZonesHeader => S("Zones", "區域");

    // ----- Status (main view model) -----
    public static string StatusConnecting => S("Connecting…", "連線中…");
    public static string StatusConnectedFormat =>
        S("Connected — Scope II RX, firmware {0}", "已連線 — Scope II RX，韌體 {0}");
    public static string StatusNotFound =>
        S("Keyboard not found. Close Armoury Crate / OpenRGB, then restart.",
          "找不到鍵盤。請先關閉 Armoury Crate / OpenRGB，再重新啟動。");

    // ----- Tray menu -----
    public static string TrayShow => S("Show", "顯示");
    public static string TrayExit => S("Exit", "結束");

    // ----- Zone row summary -----
    public static string ZoneSummary(string label, int count) =>
        Zh ? $"{label}（{count} 鍵）" : $"{label} on {count} key(s)";

    // ----- Enum display names -----
    public static string EffectName(EffectChoice e) => e switch
    {
        EffectChoice.Off => S("Off", "關閉"),
        EffectChoice.Solid => S("Solid", "純色"),
        EffectChoice.Breathing => S("Breathing", "呼吸"),
        EffectChoice.Rainbow => S("Rainbow", "彩虹"),
        EffectChoice.Wave => S("Wave", "波浪"),
        EffectChoice.Reactive => S("Reactive", "反應"),
        EffectChoice.CpuTemp => S("CPU temp", "CPU 溫度"),
        EffectChoice.GpuTemp => S("GPU temp", "GPU 溫度"),
        EffectChoice.Audio => S("Audio", "音訊"),
        _ => e.ToString(),
    };

    public static string MetricLayoutName(MetricLayoutChoice m) => m switch
    {
        MetricLayoutChoice.FunctionRow => S("Function row", "功能列"),
        MetricLayoutChoice.Numpad => S("Numpad", "數字鍵盤"),
        MetricLayoutChoice.Diagonal => S("Diagonal", "對角線"),
        _ => m.ToString(),
    };

    public static string AudioModeName(AudioZoneMode a) => a switch
    {
        AudioZoneMode.Spectrum => S("Spectrum", "頻譜"),
        AudioZoneMode.SolidColor => S("Solid colour", "單色"),
        AudioZoneMode.SolidRainbow => S("Solid rainbow", "彩虹"),
        _ => a.ToString(),
    };
}
