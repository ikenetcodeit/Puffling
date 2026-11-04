# Repository instructions

Puffling is a tiny animated desktop pet for Windows 10/11, written in C# on .NET 8.
`src/Puffling.Core` holds the character model, software renderer, animation, behavior/AI,
movement, gesture detection, settings, and reminder planning — it has **no Windows
dependency**, so it (and its tests) can run anywhere `dotnet` runs. `src/Puffling.App` is the
Win32 shell: the transparent click-through overlay window, tray icon, settings UI, and the
Microsoft Graph reminders client. Keep that boundary: anything that doesn't need a real window,
a real clock, or real OS APIs belongs in `Puffling.Core` so it stays testable.

## Build, test, and preview

- Run the test suite before considering a change done: `dotnet run --project tests/Puffling.Tests -c Release`.
- If a `Puffling.exe` instance is already running when you're about to build or test, it locks
  `Puffling.Core.dll`/`Puffling.dll` and the build fails with a file-copy error. Stop it first
  (e.g. `taskkill /IM Puffling.exe /F`), run the build/tests, then start the app back up when
  you're done (e.g. `dotnet run --project src/Puffling.App -c Release`) so the user isn't left
  without their companion.
- `./build.ps1` runs the tests and then publishes `dist/<runtime>/Puffling.exe` (add
  `-SelfContained` to bundle the runtime, or `-Runtime win-arm64` for ARM).
- Regenerate preview art with `dotnet run --project tools/Puffling.Preview -- preview expressions`
  (see that project for other preview subcommands). Use `tools/fix_gif_disposal.py` if a
  regenerated GIF smears between frames.
- Manual, hands-on testing on real Windows (tray behavior, fullscreen hide, multi-monitor,
  mixed-DPI) is still required for anything touching `Puffling.App` — the automated suite covers
  `Puffling.Core` logic (gestures, rendering bounds, movement, settings, a simulated
  multi-monitor session) but not the live Win32 shell.

## Conventions

- Settings are a single POCO (`Puffling.Core/Settings/CompanionSettings.cs`) persisted to
  `%APPDATA%\Puffling\settings.json`. Any new user-facing option needs a default, a documented
  valid range, and a `Normalize()` clamp so a hand-edited or corrupted file can't put the app in
  an invalid state. Never put secrets or account tokens in `settings.json` — the Microsoft
  sign-in cache is stored separately, encrypted, in `%LOCALAPPDATA%\Puffling`.
- Roster/personality data (red, blue, green Pufflings) is keyed by roster slot and must survive
  restart; preserve that persistence when touching `Companion.cs`, `CompanionVariant.cs`, or the
  roster tests.
- Keep per-frame work off anything that isn't the render/update loop. System or network
  monitoring (activity reactions, reminders) must poll infrequently or react to OS events, never
  run inline with rendering.
- When behavior or visuals change in a way the README documents or shows (GIFs, screenshots,
  interaction tables), regenerate the relevant preview(s) and update the README in the same
  change.
- This is an unofficial fan project; don't bundle real Minecraft textures, models, or assets —
  the look is procedural.

## Workflow

- Work through `ROADMAP.md` in order within whichever milestone is active; the desktop-assistant
  section at the bottom is deferred and should stay untouched until it's explicitly reprioritized.
  Some items (e.g. the personality/spec items in Milestone 6) are explicitly incremental ("a
  handful at a time") — it's fine to land a partial slice as its own commit and leave the item
  unchecked with a note on what's still open, rather than forcing everything into one change.
- Before implementing a roadmap item that's large, touches unfamiliar code, or depends on a real
  platform API (notification listeners, Shell icon interop, packaging, ...), research it first —
  map the existing extensibility points it should plug into (behavior/clip/settings wiring
  patterns, existing coordinators) and the real feasibility of any new platform dependency, rather
  than guessing. A background research pass (e.g. a forked/sub-agent investigation) that reports
  back before any code is written works well here.
- When a roadmap item's scope hinges on a real design tradeoff only the user can weigh (e.g.
  whether to add packaging just to call an official API, versus an unofficial workaround, versus
  deferring) — ask, rather than picking a direction unilaterally. If the answer is "defer," still
  record the scoping work directly on the roadmap item (what's blocking it, what the options were)
  so the research doesn't need repeating later, and log/commit that as its own small change.
- Build and run the full test suite after each meaningful increment, not just once at the end —
  it catches regressions (and RNG-distribution shifts in probabilistic tests) close to the change
  that caused them. Before a final commit, build the whole solution
  (`dotnet build Puffling.sln -c Release`), not just `Puffling.Core`, so `Puffling.App`/
  `Puffling.Preview` wiring is verified too.
- A seeded, probabilistic test that starts failing after an unrelated change is a signal to find
  the actual mechanism (a new option diluting an existing weighted pick, a safety net hijacking a
  deliberate move mid-transit, two independent RNG streams no longer lining up, ...), not just to
  widen a tolerance or pick a different seed until it passes. Several of those turned out to be
  real bugs (see item 38's and item 42's notes in `ROADMAP.md`) rather than test flakiness.
- After completing each task, add a concise entry to `docs/logs.json` describing what was done.
- Commit the completed task's changes to the local Git repository. Keep unrelated changes out of
  the commit.
- Don't push to the remote unless explicitly asked.
