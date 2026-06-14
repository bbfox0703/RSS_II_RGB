# RSS_II_RGB — Strix Scope II RGB

A lightweight, focused RGB controller for the **ASUS ROG Strix Scope II**
keyboard — a simpler alternative to Armoury Crate / OpenRGB. It treats the
keyboard as a 107‑pixel display and renders smooth software effects (including a
**Wave** the firmware itself can't do), reactive keypress lighting, an audio
spectrum, and system‑metric bars — all streamed live to the keyboard.

It runs quietly in the **system tray** and keeps your lighting on in the
background.

## Supported keyboards

| Keyboard | PID | Status |
| --- | --- | --- |
| ROG Strix Scope II **RX** | `0x1AB5` | ✅ Built & hardware-verified (ANSI/US, fw `01.00.13`) |
| ROG Strix Scope II **NX** | `0x1AB3` | ✅ Detected — identical 107‑LED layout, but **untested on hardware** |

Other ASUS / Razer / etc. keyboards are **not** supported (different layouts or a
different vendor protocol entirely). See [Notes & limitations](#notes--limitations)
if you have a second keyboard from another brand connected.

## Download & run

1. Grab the latest `RSS_II_RGB-vX.Y.Z-win-x64.zip` from the
   [**Releases**](../../releases) page and unzip it anywhere.
2. **Close Armoury Crate / OpenRGB first** — only one app can control the
   keyboard at a time.
3. Run `RSS_II_RGB.App.exe`. It's a self-contained build — no .NET install needed.

The app starts in the system tray. **Closing or minimising** the window hides it
to the tray (your lighting keeps running); **right‑click the tray icon → Show**
to bring it back, or **Exit** to quit. Launching the app again simply surfaces the
already‑running window.

## Using the app

- **Effect** — pick a base look for the whole keyboard: Off, Solid, Breathing,
  Rainbow, or Wave. Set its **Colour** and **Brightness**.
- **Overlays** — independent tick‑boxes layered on top of the base effect:
  - **Reactive** — keys flare and ripple as you type.
  - **Audio** — reacts to whatever is playing. Two mutually-exclusive layouts:
    a per-key **frequency spectrum** (sensitivity slider), or **three-region
    bars** — bass / mid / treble as horizontal VU bars across the Z, A and Tab
    rows (the louder a band, the further its row fills), with a separate
    multiplier per region. Lit keys show clearly on top of any base effect.
- **System metrics** — show CPU %, memory %, GPU % and GPU temperature as 1–4 lit
  cells (green→red) on a chosen key group. **No administrator rights needed.**
- **Zone editor** — select keys (click or drag a box) and give them their own
  effect or audio mode, layered on top of the global effect.
- **Start with Windows** — tick this to launch the app automatically when you log
  in; it comes up **minimised to the tray** so your lighting is on from boot.
  (Per-user registry Run key — no admin rights needed.)

Everything you set is **saved** and restored next launch.

The interface language follows Windows: a **Traditional Chinese** display language
shows a Chinese UI, otherwise English.

### Display priority

When more than one thing wants the same key, a fixed top‑to‑bottom order decides
what you see:

```
System metrics  →  Reactive  →  Audio zones  →  other zones  →  Audio  →  base effect
   (top)                                                                    (bottom)
```

So metrics always win, reactive shows over audio, and the base effect fills
anything nothing else is touching.

## Requirements

- Windows 10/11 (x64).
- An ASUS ROG Strix Scope II **RX** (or **NX**) keyboard.
- **Close Armoury Crate / OpenRGB first.**
- GPU **utilisation and temperature** use NVIDIA **NVML** (`nvml.dll`, installed
  with the driver). On non‑NVIDIA systems those two metrics show no data; CPU %,
  memory %, and all lighting still work.

## Notes & limitations

- **Multiple keyboards / other brands:** the app only ever touches the ASUS Scope
  II (vendor `0x0B05`). A keyboard from another brand — e.g. a **Razer**
  controlled by Synapse — is invisible to this app, so the two run side by side
  without conflict.
- **Reactive reacts to *any* keyboard:** the keypress effect uses a global
  system‑wide hook, so typing on a *second* keyboard still triggers flares/ripples
  on the Scope II. (Audio and metrics are unaffected — they read sensors, not
  keys.)
- The render loop streams ~32 FPS; timer granularity caps it below the device's
  ~42 FPS ceiling. Smooth in practice.
- Reactive keypress effects (fade/ripple) are global, not per‑zone.
- The zone editor's keyboard is a uniform grid, not a pixel‑accurate shape.

## Build from source

```sh
# Run (debug)
dotnet run --project src/RSS_II_RGB.App

# Run the test suite
dotnet test RSS_II_RGB.slnx

# Publish a Native AOT, trimmed build (~22 MB self-contained exe)
dotnet publish src/RSS_II_RGB.App -r win-x64 -c Release
```

Requires the **.NET 10 SDK**. The build also compiles the `SensorsHost` helper and
copies it into a `sensorshost\` subfolder next to the app, where the app launches
it from. CI runs only when you push a `vX.Y.Z` tag: it builds, tests, and — if
those pass — publishes a release zip automatically (see
[`.github/workflows`](.github/workflows)). Normal pushes/merges don't trigger it.

### Solution layout

| Project | Role |
| --- | --- |
| `src/RSS_II_RGB.Core` | Platform‑agnostic engine: per‑device `KeyboardProfile`, framebuffer, Direct‑packet builder, layered compositor, effects, render loop, and all interfaces. No OS calls. |
| `src/RSS_II_RGB.Windows` | The only project that calls native OS APIs — raw Win32 HID (`hid.dll`/`setupapi.dll`) and the `WH_KEYBOARD_LL` hook (`user32.dll`), plus the rotating file log. All `[LibraryImport]`, AOT‑safe. |
| `src/RSS_II_RGB.App` | Avalonia 11 host (single‑instance, tray icon, MVVM, compiled bindings), published as Native AOT. |
| `src/RSS_II_RGB.SensorsHost` | A separate **non‑AOT** helper that hosts the reflection‑heavy sensor/audio libraries (NAudio for audio; NVML/Win32 for metrics) and streams samples to the app over a named pipe. |
| `tests/RSS_II_RGB.Core.Tests` | xUnit 3 unit tests for the pure logic. |

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the design rationale.

## Files & configuration

- **Settings:** `%LOCALAPPDATA%\RSS_II_RGB\settings.json` (effect, overlays,
  colour, brightness, audio sensitivity, zones, and metric overlay config).
- **Logs:** `%LOCALAPPDATA%\RSS_II_RGB\Logs\` — 4‑file rotation, 8 MB each.

> `vendor/openrgb` is a read‑only reference clone (the Direct/HID protocol came
> from its TUF‑keyboard driver); recreate it with `vendor/update_vendors.ps1`. It
> is **not** committed — see [CLAUDE.md](CLAUDE.md) rule 8.
