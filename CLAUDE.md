# CLAUDE.md

> **Minimal index.** Detailed specs, history, and rules live elsewhere — this
> file points to them.

## Rule 0 — Minimalism

This file follows the **minimalism principle**. Anything that needs more than
one line of context belongs in a referenced doc, not here. Before treating
this file as the source of truth, always check:

- [`docs/`](docs/) — architecture, format specs, policies
- Sub-tree `CLAUDE.md` files (e.g. [`tools/CLAUDE.md`](tools/CLAUDE.md))
- `README.md` in each folder

If a rule grows past one or two lines, move it to a referenced doc and leave a
one-line pointer here.

## Project

Strix Scope II RGB display. 

## Stack

- **App / UI**: Avalonia UI 12 on .NET 10, published as Native AOT trimmed binary
- **Tests**: xUnit 3 (C#)

## Mandatory rules

1. **Language**: code, comments, UI strings in English. 
2. **Single instance**: UI uses a Mutex.
3. **Async everywhere**: all I/O, IPC, alerts are async.
4. **Platform abstraction**: any OS-specific call (P/Invoke, registry, OS commands) goes through an interface in the `Core` project. `Core` itself contains no platform-specific code.
5. **AOT-safe C#**: no reflection-based APIs. Source generators only (`[JsonSerializable]`, `[ObservableProperty]`, etc.).
6. **Logs**: `%LOCALAPPDATA%\RSS_II_RGB\Logs\`. 4-file rotation, 8 MB max each. Root `Logs/` has only subfolders, no loose files.
7. **Magic strings**: one centralised file per project, well-commented.
8. **Vendor deps**: cloned into [`vendor/`](vendor/), never git submodules. `vendor/<name>/` is **read-only**

## Workflow rules

- **Build verification**: after code changes, fully rebuild and inspect the actual build output before claiming success.
- **Refactoring**: when asked to refactor/rename, change the code (move files, update imports, rename classes) — not just docs.
- **Debugging**: verify fixes against actual memory layout / data structure; if the first attempt fails, re-examine fundamental assumptions before iterating.
- **PRs**: before `gh pr create`, run `git status` and `git log --oneline -5`; resolve any divergence.

## Pointers

put any pointer file here (e.g. `docs/`, `vendor/`) and link to it from the relevant rule above.