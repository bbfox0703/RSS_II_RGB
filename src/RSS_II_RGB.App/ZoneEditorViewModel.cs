using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RSS_II_RGB.Core.Layout;
using RSS_II_RGB.Core.Rendering;

namespace RSS_II_RGB.App;

/// <summary>One row in the zones list, with its own remove command.</summary>
internal sealed partial class ZoneRowVM : ObservableObject
{
    private readonly Action<ZoneRowVM> _remove;

    public ZoneRowVM(string summary, Action<ZoneRowVM> remove)
    {
        Summary = summary;
        _remove = remove;
    }

    public string Summary { get; }

    [RelayCommand]
    private void Remove() => _remove(this);
}

/// <summary>
/// The zone editor: select keys on the visual keyboard, assign an effect + colour
/// to the selection, and layer those zones on top of the global effect. Zones are
/// persisted and restored via <see cref="SettingsService"/>.
/// </summary>
internal sealed partial class ZoneEditorViewModel : ObservableObject
{
    private const double CellStride = 32;

    /// <summary>Drag-sensitive catch margin around the keys (so you can box-select
    /// starting outside Esc's top-left corner).</summary>
    public const double EdgePad = 22;

    private readonly KeyboardController _controller;
    private readonly SettingsService _settings;
    private readonly List<Zone> _zones = new();

    public ObservableCollection<KeyVM> Keys { get; } = new();
    public ObservableCollection<ZoneRowVM> ZoneRows { get; } = new();

    // Reactive is global-only for now, so it isn't offered per zone.
    public EffectChoice[] ZoneEffects { get; } =
    {
        EffectChoice.Solid, EffectChoice.Breathing, EffectChoice.Rainbow,
        EffectChoice.Wave, EffectChoice.Audio, EffectChoice.Off,
    };

    public AudioZoneMode[] AudioModeOptions { get; } =
    {
        AudioZoneMode.Spectrum, AudioZoneMode.SolidColor, AudioZoneMode.SolidRainbow,
    };

    [ObservableProperty]
    private EffectChoice _zoneEffect = EffectChoice.Solid;

    [ObservableProperty]
    private AudioZoneMode _selectedAudioMode = AudioZoneMode.Spectrum;

    [ObservableProperty]
    private Color _zoneColor = Colors.Cyan;

    /// <summary>True when the Audio effect is selected, so the mode picker shows.</summary>
    public bool IsAudioMode => ZoneEffect == EffectChoice.Audio;

    partial void OnZoneEffectChanged(EffectChoice value) => OnPropertyChanged(nameof(IsAudioMode));

    // The keys occupy CanvasWidth x CanvasHeight; the host adds EdgePad all round.
    public double CanvasWidth => _controller.Profile.Cols * CellStride;
    public double CanvasHeight => _controller.Profile.Rows * CellStride;
    public double HostWidth => CanvasWidth + 2 * EdgePad;
    public double HostHeight => CanvasHeight + 2 * EdgePad;
    public Thickness KeysMargin => new(EdgePad);

    public ZoneEditorViewModel(KeyboardController controller, SettingsService settings)
    {
        _controller = controller;
        _settings = settings;

        foreach (LedKey key in _controller.Profile.Keys)
        {
            Keys.Add(new KeyVM(key.Index, key.Name, key.Col * CellStride, key.Row * CellStride));
        }

        // Reflect any restored zones.
        foreach (Zone zone in _controller.Zones)
        {
            _zones.Add(zone);
            AddRow(zone);
        }
    }

    [RelayCommand]
    private void AssignZone()
    {
        var selected = new List<int>();
        foreach (KeyVM k in Keys)
        {
            if (k.IsSelected)
            {
                selected.Add(k.Index);
            }
        }
        if (selected.Count == 0)
        {
            return;
        }

        var zone = new Zone(selected.ToArray(), ZoneEffect,
                            new Rgb(ZoneColor.R, ZoneColor.G, ZoneColor.B), SelectedAudioMode);
        _zones.Add(zone);
        AddRow(zone);
        Push();
        ClearSelection();
    }

    [RelayCommand]
    private void ClearZones()
    {
        _zones.Clear();
        ZoneRows.Clear();
        Push();
        ClearSelection();
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (KeyVM k in Keys)
        {
            k.IsSelected = true;
        }
    }

    [RelayCommand]
    private void SelectNone() => ClearSelection();

    private void AddRow(Zone zone)
    {
        string label = zone.Effect == EffectChoice.Audio ? $"Audio · {zone.AudioMode}" : zone.Effect.ToString();
        ZoneRows.Add(new ZoneRowVM($"{label} on {zone.KeyIndices.Count} key(s)", RemoveRow));
    }

    private void RemoveRow(ZoneRowVM row)
    {
        int index = ZoneRows.IndexOf(row);
        if (index < 0)
        {
            return;
        }
        ZoneRows.RemoveAt(index);
        _zones.RemoveAt(index);
        Push();
    }

    private void Push()
    {
        _controller.SetZones(_zones.ToArray());
        _settings.Settings.Zones = _zones.Select(ZoneMapping.ToSetting).ToList();
        _settings.Save();
    }

    private void ClearSelection()
    {
        foreach (KeyVM k in Keys)
        {
            k.IsSelected = false;
        }
    }
}
