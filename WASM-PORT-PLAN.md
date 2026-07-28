# OpenRA → Browser (WebAssembly) Port Plan

Goal: the **authentic Red Alert** — real sprites, real gameplay, our lunar mechanics —
running in a web browser from static files, ultimately hosted/linked from the project's
GitHub Pages site. This is a from-scratch port: the one third-party effort known to have
worked (April 2026) never published its source, and the public `openra-wasm-port` GitLab
repo is an empty stub (verified: plain desktop OpenRA, zero wasm code, dead since
July 2024). We reuse their *lessons* (published in their write-up), not their code.

**Honest scope:** this is a multi-month engineering effort with real unknowns. Every
phase below ends in a CI-verified, demonstrable milestone — no phase is "done" on my
say-so. The build machine is GitHub Actions (`.github/workflows/wasm-port.yml`), since
the dev machine has no .NET SDK; the loop is write → push → read compiler/runtime
verdict → iterate.

## Why this is plausible at all (from the 2026-07-28 audit of THIS codebase)

- **Native code is quarantined.** All P/Invoke lives in `OpenRA.Platforms.Default`
  (SDL2, FreeType) + `OpenRA.WindowsLauncher`. The engine core is pure managed C#.
- **`OpenRA.Game` has exactly one native NuGet: `OpenRA-Eluant` (Lua).** Everything
  else in Game/Mods.Common (Linguini, SharpZipLib, Mono.NAT, NVorbis, MP3Sharp…) is
  managed and should run under browser-wasm as-is.
- **The renderer already speaks GLES3.** `GLProfile.Embedded` is first-class, shaders
  use a runtime-substituted `#version`, and "core features = shared set of GL 3.2 and
  GLES 3" (OpenGL.cs). WebGL2 ≈ GLES 3.0, so `IGraphicsContext` → WebGL2 is a mapping,
  not a rewrite.
- **The platform contract is small.** `IPlatform` = CreateWindow + CreateSound +
  CreateFont; `IGraphicsContext` is a compact GL-shaped interface; `ISoundEngine` is
  ~14 methods that map naturally onto WebAudio.
- **.NET 10 browser-wasm is mature** (JSImport/JSExport interop, `Microsoft.NET.Sdk.WebAssembly`).

## Known hard parts (eyes open)

1. **Game-loop inversion.** The browser owns the frame; `Game.Run`'s blocking loop must
   be driven by `requestAnimationFrame` callbacks instead. Structural, touches core.
2. **Threading.** .NET wasm threading is still limited (no blocking the main thread,
   worker-pool model, Firefox gaps). Every `Thread`/blocking-wait in the engine needs
   an audit; sim likely becomes single-threaded-cooperative first.
3. **Lua.** Replace native Eluant with managed **MoonSharp** behind an Eluant-shaped
   shim (the approach the 2026 port proved works). Until then: strip Lua (skirmish
   works without mission scripts).
4. **VFS over HTTP.** Mods/content must be fetched (fetch → MemoryStream packages)
   and fully staged before engine init.
5. **Fonts.** FreeType is native; replace with browser Canvas2D glyph rasterization
   behind `IFont`, or a managed rasterizer.
6. **Content licensing for hosting.** The original RA assets are EA freeware (2008)
   and OpenRA auto-downloads them; serving them from our own static host needs a
   licensing check before public deploy.
7. **Multiplayer is out of scope** until everything else works (would need WebRTC).

## Phases (each = a CI-verified milestone)

- **W0 — Harness & probe (NOW).** `wasm-port.yml` workflow + `OpenRA.WasmProbe`
  (browser-wasm app referencing `OpenRA.Game`). Milestone: CI publishes a wasm bundle
  and the probe executes OpenRA math. *Verdict pending — this proves/denies that the
  managed closure of OpenRA.Game is wasm-publishable at all.*
- **W1 — Dependency quarantine.** Feature-flag Eluant (and any other blocker W0
  surfaces) out of the browser build; probe grows to instantiate MiniYAML, the mod
  manifest loader, and an in-memory VFS. Milestone: mod rules parse in the browser.
- **W2 — `OpenRA.Platforms.Browser`.** Implement `IPlatform`/`IPlatformWindow`/
  `IGraphicsContext` over WebGL2 via `[JSImport]`; canvas input pump; stub sound.
  Milestone: engine clears a frame + draws a textured quad in-browser.
- **W3 — Boot to menu.** VFS-over-HTTP for `mods/spaceage` + `mods/ra` rules, game-loop
  inversion (rAF), Canvas2D fonts. Milestone: OpenRA main menu renders in a browser tab.
- **W4 — Playable skirmish.** Input, WebAudio sound, performance pass (AOT where it
  pays), content pipeline for RA freeware assets. Milestone: a lunar skirmish is
  playable start-to-finish in the browser.
- **W5 — Ship.** Host the bundle (Pages if size/licensing allow, else a static CDN
  linked from Pages), loading UX, save settings to localStorage.

## Status log

- **2026-07-28:** Audit done. W0 harness + probe committed. Site/roadmap will only
  claim what CI has proven.
- **2026-07-28 (later): W0 PROVEN.** First probe run green: `dotnet publish` of
  OpenRA.Game's full managed closure for browser-wasm succeeded (Emscripten
  wasm-ld linked dotnet.native.wasm; wasm-opt ran; bundle produced). Native
  Eluant did not block publish — DllImport resolution is a runtime concern.
- **2026-07-28 (later): W1 started.** Probe now parses SpaceAge-flavoured rules
  via `MiniYaml.FromString` and executes under Node in CI (`noderun.mjs` +
  "Execute probe under Node" step) — closing W0's publish-vs-run gap. Also fixed
  by CI iteration this session: 4 trait compile errors, IDE0005, the
  GetVariableObservers override lint, spaceage YAML whitespace lint, and
  fluent-key mod titles. Verdict of the first execution run: pending.
