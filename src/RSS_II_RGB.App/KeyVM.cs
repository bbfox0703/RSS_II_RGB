using CommunityToolkit.Mvvm.ComponentModel;

namespace RSS_II_RGB.App;

/// <summary>One selectable key in the visual keyboard, positioned by its grid cell.</summary>
internal sealed partial class KeyVM : ObservableObject
{
    public int Index { get; }
    public string Name { get; }
    public string ShortLabel { get; }
    public double X { get; }
    public double Y { get; }

    [ObservableProperty]
    private bool _isSelected;

    public KeyVM(int index, string name, double x, double y)
    {
        Index = index;
        Name = name;
        ShortLabel = MakeLabel(name);
        X = x;
        Y = y;
    }

    private static string MakeLabel(string name) => name switch
    {
        "ESCAPE" => "Esc",
        "BACK_TICK" => "`",
        "TAB" => "Tab",
        "CAPS_LOCK" => "Cap",
        "LEFT_SHIFT" or "RIGHT_SHIFT" => "Sh",
        "LEFT_CONTROL" or "RIGHT_CONTROL" => "Ct",
        "LEFT_ALT" or "RIGHT_ALT" => "Al",
        "LEFT_WINDOWS" => "Win",
        "MENU" => "Mn",
        "RIGHT_FUNCTION" => "Fn",
        "SPACE" => "—",
        "BACKSPACE" => "Bk",
        "ANSI_ENTER" or "NUMPAD_ENTER" => "Ent",
        "ANSI_BACK_SLASH" => "\\",
        "FORWARD_SLASH" => "/",
        "SEMICOLON" => ";",
        "QUOTE" => "'",
        "COMMA" => ",",
        "PERIOD" => ".",
        "MINUS" => "-",
        "EQUALS" => "=",
        "LEFT_BRACKET" => "[",
        "RIGHT_BRACKET" => "]",
        "PRINT_SCREEN" => "Prt",
        "SCROLL_LOCK" => "Scr",
        "PAUSE_BREAK" => "Pau",
        "INSERT" => "Ins",
        "DELETE" => "Del",
        "HOME" => "Hm",
        "END" => "End",
        "PAGE_UP" => "PgU",
        "PAGE_DOWN" => "PgD",
        "UP_ARROW" => "↑",
        "DOWN_ARROW" => "↓",
        "LEFT_ARROW" => "←",
        "RIGHT_ARROW" => "→",
        "NUMPAD_LOCK" => "Num",
        "NUMPAD_DIVIDE" => "/",
        "NUMPAD_TIMES" => "*",
        "NUMPAD_MINUS" => "-",
        "NUMPAD_PLUS" => "+",
        "NUMPAD_PERIOD" => ".",
        "Logo" => "◆",
        _ => name.StartsWith("NUMPAD_", StringComparison.Ordinal) ? name["NUMPAD_".Length..] : name,
    };
}
