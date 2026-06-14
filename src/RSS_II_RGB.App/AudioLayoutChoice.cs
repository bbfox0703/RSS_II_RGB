namespace RSS_II_RGB.App;

/// <summary>How the global Audio overlay renders. The two are mutually exclusive.</summary>
internal enum AudioLayoutChoice
{
    Spectrum, // per-column frequency spectrum across the whole keyboard
    Bars,     // three horizontal bass / mid / treble bars
}
