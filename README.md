# RSS_II_RGB — Strix Scope II RGB

A lightweight, focused RGB controller for the **ASUS ROG Strix Scope II RX**
keyboard — a simpler alternative to Armoury Crate / OpenRGB. It treats the
keyboard as a 107‑pixel display: a software render loop composites a stack of
effect layers and streams each frame to the keyboard over the vendor HID
"Direct" command.

> Built and hardware-verified on a Strix Scope II **RX** (VID `0x0B05`, PID
> `0x1AB5`), ANSI/US layout, firmware `01.00.13`.

## Features

- **Software effects:** Solid, Breathing, Rainbow, and a smooth **Wave** the
  firmware itself can't render — all driven by the host, not firmware presets.
- **Reactive lighting:** press a key and it flares + fades, with an expanding
  **ripple** across the board (global low‑level keyboard hook).
- **Audio reactive:** a frequency **spectrum** across the keys (WASAPI loopback
  + FFT, logarithmic bands so bass/mid/treble each span ~⅓ of the width), with a
  sensitivity control and per‑frame smoothing.
- **System‑metric overlay:** CPU %, memory %, GPU % and GPU temperature shown as
  1–4 lit cells (green→red) on a chosen key group — laid over whatever effect is
  running. **No administrator rights required.**
- **Zone editor:** select keys (click or drag‑box) and assign per‑zone effects —
  including audio zones in Spectrum / SolidColor / SolidRainbow modes — layered
  on top of the global effect.
- **Persistence:** your effect, colour, brightness, zones, and metric settings
  are saved and restored on next launch.

## Requirements

- Windows 10/11 (x64) and the .NET 10 runtime/SDK.
- An ASUS ROG Strix Scope II **RX** keyboard.
- **Close Armoury Crate / OpenRGB first** — only one app can own the keyboard's
  control interface at a time.
- GPU **utilisation and temperature** are read via NVIDIA **NVML** (`nvml.dll`,
  installed with the driver). On non‑NVIDIA systems those two metrics simply show
  no data; CPU %, memory %, and all lighting still work.

## Build & run

```sh
# Run (debug)
dotnet run --project src/RSS_II_RGB.App

# Run the test suite
dotnet test RSS_II_RGB.slnx

# Publish a Native AOT, trimmed build (~18 MB self-contained exe)
dotnet publish src/RSS_II_RGB.App -r win-x64 -c Release
```

The build also compiles the `SensorsHost` helper and copies it into a
`sensorshost\` subfolder next to the app, where the app launches it from.

## Solution layout

| Project | Role |
| --- | --- |
| `src/RSS_II_RGB.Core` | Platform‑agnostic engine: layout, framebuffer, Direct‑packet builder, layered compositor, effects, render loop, and all interfaces. No OS calls. |
| `src/RSS_II_RGB.Windows` | The only project that calls native OS APIs — raw Win32 HID (`hid.dll`/`setupapi.dll`) and the `WH_KEYBOARD_LL` hook (`user32.dll`), plus the rotating file log. All `[LibraryImport]`, AOT‑safe. |
| `src/RSS_II_RGB.App` | Avalonia 11 host (single‑instance, MVVM, compiled bindings), published as Native AOT. |
| `src/RSS_II_RGB.SensorsHost` | A separate **non‑AOT** helper that hosts the reflection‑heavy sensor/audio libraries (NAudio for audio; NVML/Win32 for metrics) and streams samples to the app over a named pipe. |
| `tests/RSS_II_RGB.Core.Tests` | xUnit 3 unit tests for the pure logic. |

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the design rationale.

## Files & configuration

- **Settings:** `%LOCALAPPDATA%\RSS_II_RGB\settings.json` (effect, colour,
  brightness, audio sensitivity, zones, and metric overlay config).
- **Logs:** `%LOCALAPPDATA%\RSS_II_RGB\Logs\` — 4‑file rotation, 8 MB each.

## Notes & limitations

- The render loop streams ~32 FPS; `Task.Delay` timer granularity caps it below
  the device's measured ~42 FPS ceiling. Smooth in practice.
- Reactive keypress effects (fade/ripple) are currently global, not per‑zone.
- The zone editor's keyboard is a uniform grid, not a pixel‑accurate shape.
- `vendor/openrgb` is a read‑only reference clone (the Direct/HID protocol came
  from its TUF‑keyboard driver); recreate it with `vendor/update_vendors.ps1`.
  It is **not** committed — see [CLAUDE.md](CLAUDE.md) rule 8.
