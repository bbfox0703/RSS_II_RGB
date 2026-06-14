using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RSS_II_RGB.Core.Layout;
using RSS_II_RGB.Core.Rendering;

namespace RSS_II_RGB.App;

/// <summary>
/// The zone editor: select keys on the visual keyboard, assign an effect + colour
/// to the selection, and layer those zones on top of the global effect.
/// </summary>
internal sealed partial class ZoneEditorViewModel : ObservableObject
{
    private const double CellStride = 32;

    /// <summary>Drag-sensitive catch margin around the keys (so you can box-select
    /// starting outside Esc's top-left corner).</summary>
    public const double EdgePad = 22;

    private readonly KeyboardController _controller;
    private readonly List<Zone> _zones = new();

    public ObservableCollection<KeyVM> Keys { get; } = new();
    public ObservableCollection<string> ZoneSummaries { get; } = new();

    // Reactive is global-only for now, so it isn't offered per zone.
    public EffectChoice[] ZoneEffects { get; } =
    {
        EffectChoice.Solid, EffectChoice.Breathing, EffectChoice.Rainbow,
        EffectChoice.Wave, EffectChoice.Off,
    };

    [ObservableProperty]
    private EffectChoice _zoneEffect = EffectChoice.Solid;

    [ObservableProperty]
    private Color _zoneColor = Colors.Cyan;

    // The keys occupy CanvasWidth x CanvasHeight; the host adds EdgePad all round.
    public double CanvasWidth => ScopeIILayout.Cols * CellStride;
    public double CanvasHeight => ScopeIILayout.Rows * CellStride;
    public double HostWidth => CanvasWidth + 2 * EdgePad;
    public double HostHeight => CanvasHeight + 2 * EdgePad;
    public Thickness KeysMargin => new(EdgePad);

    public ZoneEditorViewModel(KeyboardController controller)
    {
        _controller = controller;
        foreach (LedKey key in ScopeIILayout.Keys)
        {
            Keys.Add(new KeyVM(key.Index, key.Name, key.Col * CellStride, key.Row * CellStride));
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

        _zones.Add(new Zone(selected.ToArray(), ZoneEffect, new Rgb(ZoneColor.R, ZoneColor.G, ZoneColor.B)));
        _controller.SetZones(_zones.ToArray());
        ZoneSummaries.Add($"{ZoneEffect} on {selected.Count} key(s)");
        ClearSelection();
    }

    [RelayCommand]
    private void ClearZones()
    {
        _zones.Clear();
        ZoneSummaries.Clear();
        _controller.SetZones(Array.Empty<Zone>());
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

    private void ClearSelection()
    {
        foreach (KeyVM k in Keys)
        {
            k.IsSelected = false;
        }
    }
}
