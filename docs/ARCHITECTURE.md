# Architecture

## The core idea: the keyboard is a 107‑pixel display

The keyboard's firmware effects are a fixed menu, and some don't even work on
this device (hardware Wave is dead). So instead of driving firmware modes, the
app renders **everything in software**:

1. A render loop ticks ~40×/sec.
2. It composites a stack of **effect layers** into a 107‑LED frame buffer.
3. It serialises that frame into the vendor HID **Direct** command and streams
   it to the keyboard.

This was validated by a proof of concept (`tools/ScopeIIProbe`) before any
production code: the device sustains ~42 FPS of Direct writes, which is the
enabler for software‑driven effects — including the wave the firmware can't do,
plus reactive, audio, and metric effects the firmware will never have.

## Layered compositor

A frame is built by a `Compositor` from an ordered list of `IEffectLayer`s:

- Each layer has a **generator** (`Render`), a **key mask** (which LEDs it
  applies to), a **blend mode** (Normal / Additive / Multiply / Max), and a
  **z‑order**.
- The compositor renders each layer (z ascending) into a scratch buffer, then
  blends it into the frame using the layer's mask + blend.

One abstraction expresses both "one global effect over all keys" (a single
layer, mask = all) and "different effects on different key groups" (multiple
masked layers — the Synapse‑style zone editor). The metric overlay and reactive
overlays are simply higher‑z layers that touch only their own keys.

`KeyboardController` assigns z‑orders from **fixed priority bands** so the stack
reads top‑down regardless of how many zones exist:

| z band | layer | notes |
| --- | --- | --- |
| 500 000 | System metrics | top — always visible |
| 30 000 | Reactive overlay | global keypress flare + ripple (Additive) |
| 20 000+ | Audio zones | zone spectrum/volume |
| 10 000+ | Other zones | static zone effects |
| 1 000 | Audio overlay | global spectrum (Additive) |
| 0 | Base effect | main‑UI effect over every key |

Reactive and the global Audio overlay are independent main‑UI toggles, not effect
modes — they composite **Additive** so idle/silent frames stay transparent and the
layers below show through. Reactive outranks Audio (keypress flares win over a
spectrum); both stay below the metric bars.

Effects in `Core/Effects/Layers`: Solid, Breathing, Rainbow, Wave (by column),
KeypressFade, Ripple, TempIndicator, AudioReactive (spectrum), AudioVolume
(loudness), AudioBars (bass/mid/treble VU bars on three key rows), MetricOverlay
(CPU/Mem/GPU bars).

## Render loop & threading

Three threads:

- **UI** (Avalonia) — selection/status only; never touches the device.
- **Engine** (`RenderEngine.RunAsync`, one long‑running task) — drains queued key
  events, composites, and writes the frame. The synchronous Win32 device write is
  the natural throttle; a `Task.Delay` tops up to the target period.
- **Hook** (`Win32KeyboardHook`) — a dedicated thread with a `GetMessage` pump
  (required for `WH_KEYBOARD_LL`). Key events are handed to the engine through a
  lock‑free `ConcurrentQueue`.

Effect changes are applied by swapping the compositor's layer set on the engine
thread (a volatile "pending layers" reference), so the compositor is only ever
mutated by one thread.

## Platform abstraction (CLAUDE.md rule 4)

`Core` defines interfaces and contains **no** OS‑specific code:

- `IKeyboardDevice` / `IKeyboardDeviceFactory` — open, write a frame, apply a
  firmware effect, read firmware info.
- `IKeyboardHook` — global key events.
- `ILogSink` — logging.
- `ISensorFeed` — the IPC sensor stream contract.

`Windows` is the only project that names native libraries, implementing those
interfaces with raw Win32:

- **HID:** `SetupAPI` enumeration filtered by usage page `0xFF00` + the `mi_01`
  interface, then `CreateFile`/`WriteFile`/`ReadFile`. (Raw `ReadFile` keeps the
  leading report‑id byte, unlike hidapi — read offsets are `+1`.)
- **Hook:** `SetWindowsHookEx(WH_KEYBOARD_LL)` with an `[UnmanagedCallersOnly]`
  proc, scan‑code → LED mapping in `Core/Input/ScancodeMap`.

## AOT design (rule 5)

The app publishes as **Native AOT, trimmed, with zero IL/trim warnings**:

- All P/Invoke uses source‑generated `[LibraryImport]`; native structs are read
  via blittable layouts, not reflection marshalling.
- MVVM uses CommunityToolkit source generators; Avalonia uses **compiled XAML
  bindings** (`x:DataType`); settings use `System.Text.Json` source generation.

The reflection‑heavy sensor/audio libraries can't be AOT‑trimmed, so they live in
a **separate non‑AOT process**, `SensorsHost`, kept out of the main app entirely.

## Sensors: the IPC seam

`SensorsHost` is a small console process the app launches (with a parent‑PID
watchdog so it never orphans). It hosts pluggable providers:

- `SystemMetricsProvider` — CPU % (`GetSystemTimes`), memory % (`GlobalMemory
  StatusEx`), GPU % and GPU temperature (NVML). No admin required.
- `AudioProvider` — WASAPI loopback capture + FFT → 24 logarithmic frequency
  bands, with per‑band auto‑gain, an RMS silence gate, and fast‑attack /
  slow‑release temporal smoothing.

It serialises `SensorSample` JSON lines over a named pipe
(`\\.\pipe\RSS_II_RGB.sensors`). `Windows/NamedPipeSensorFeed` reads and
reconnects; `App/SensorService` pumps the samples into a thread‑safe
`Core/Sensors/SensorState` that the sensor effect layers read each frame.

```
SensorsHost (non-AOT)  --named pipe-->  App (AOT)  -->  SensorState  -->  effect layers  -->  frame
   NAudio, NVML, Win32                   NamedPipeSensorFeed
```

## Persistence

`App/SettingsService` loads/saves `AppSettings` to
`%LOCALAPPDATA%\RSS_II_RGB\settings.json` via a source‑generated JSON context
(enums as readable strings). The controller restores the global effect, audio
sensitivity, zones, and metric overlay config on launch; every UI change saves.

## Per-device layout (`KeyboardProfile`)

Everything device-specific — LED count, matrix geometry, the ordered key table
(render order + key ids), and the scan-code → index map — lives in a
`Core/Layout/KeyboardProfile`. The render path is driven entirely by a profile
instance: the engine threads it through `EffectContext.Layout`, the device sizes
its frame buffer and Direct packets from it, and the hook resolves presses with a
per-profile `ScancodeResolver`. The Direct command itself (15 LEDs/packet,
65-byte report, `0xC0 0x81`) is shared across the whole ASUS TUF keyboard family,
so adding a keyboard is data: define its `KeyboardProfile` and map its PID to it
in the device factory. `ScopeIILayout.Profile` is the one concrete profile today
(used by both Scope II RX and the layout-identical NX).

## Reference

The Direct/HID protocol, key‑id ↔ name map, and the 6×24 matrix were ported from
the read‑only `vendor/openrgb` TUF‑keyboard driver and baked into
`Core/Layout/ScopeIILayout.cs` as committed data (no runtime dependency on
`vendor/`).
