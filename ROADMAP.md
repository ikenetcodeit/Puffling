# Puffling roadmap

A living plan of features to add, grouped by milestone. Work through the active milestone in
order; item numbers are written into each checkbox so they remain stable references even as
items are checked off. The **Desktop assistant** and **Later ideas** sections are deferred —
don't start them until the active milestones above them are done or they're explicitly
reprioritized.

## Milestone 1 — Pet movement

- [x] **1. Face the direction of travel.** Travel right visibly faces right and travel left
  faces left, with smooth turns on starts, stops, and reversals.

- [ ] **2. Cross between displays autonomously.** Review destination selection, path visibility,
  and return-to-screen behavior so Pufflings occasionally travel between connected displays
  without being dragged. Avoid real gaps and keep them in usable screen areas.

## Milestone 2 — Pet behavior

- [x] **3. Give multiple Pufflings individuality.** Red, blue, and green accents with distinct
  faces, temperaments, individual actions, and shared chase, bounce sparring, and pong-like play.
  Identities stay stable after restart; synchronized pair and trio interactions are preserved.

- [x] **4. React to system activity.** Puffling polls running processes and the foreground
  window title every few seconds (off the render loop) to infer music, a movie/video, coding, or
  a coding agent running, and settles into a matching ambient pose when idle: a groove for music,
  a still "watching" pose for video, a focused pose for an IDE, and an alert, sparkly pose while
  an agent runs. Each change announces itself with a short pixel-font notification bubble. The
  heuristics live in `Puffling.App.Platform.SystemActivityDetector` and are intentionally coarse
  and extensible — add process names rather than new detection machinery when covering a new app.

- [x] **34. Make group play rare and personality-shaped.** The full-roster routine (everyone
  dances/chases/spars/plays pong together) now fires every 3-5 minutes instead of every ~45
  seconds, so it reads as a surprise rather than a loop that flattens each Puffling's identity.
  With all three present, a separate "duo" routine (dance, chase, or a staring contest) picks two
  of the three on a shorter, ~70-150 second cadence, leaving the third to wander on its own. The
  pair is weighted by temperament — red and blue (the composed leader and the bubbly one) pair up
  most, red occasionally draws green in, and blue and green pair up least, so green mostly does
  its own thing. Alone, a Puffling eventually plays a one-off "looking for someone" clip
  (`BehaviorKind.Searching`); it never fires with company around
  (`Brain.CompanionsPresent`). See `GroupInteractionCoordinator`.

## Milestone 3 — Presentation

- [x] **5. Fix README GIF frame smearing.** Every README GIF, including pair and trio previews,
  clears or disposes of the previous frame correctly and loops cleanly on GitHub.

## Milestone 4 — Desktop assistant

These were the former assistant milestones. Keep them as a plan, but leave them inactive until
the desktop pet work above is done or the assistant becomes a priority again.

- [ ] **6. Define the tap interaction.** Make a single click available to open an assistant panel
  while preserving dragging, back-and-forth petting, the double-click dance, and clicks that wake
  a sleeping Puffling. Decide how a second click closes the panel and how clicking elsewhere
  dismisses it.

- [ ] **7. Build a local computer-information service.** Add a Windows-side provider for system
  status and computer details, separate from the renderer and animation loop. Handle unavailable
  sensors and disconnected devices gracefully. Collect only the data needed for visible features
  and never transmit device details.

- [ ] **8. Build the assistant panel shell.** Show a small, readable panel beside the tapped
  Puffling. Keep it inside the usable screen area on multiple monitors and mixed-DPI setups, and
  avoid taking keyboard focus unnecessarily. Make it possible to add cards and actions without
  changing the pet's rendering code.

- [ ] **9. Add the computer overview.** Show clearly labeled CPU and memory usage, available
  storage, battery level and charging state when applicable, network connection status, and
  uptime. Refresh at a modest rate only while the panel is visible.

- [ ] **10. Add computer details on demand.** Let the user expand the panel to see the Windows
  version, processor, installed memory, graphics adapter, display information, and drive
  capacities. Keep the default view compact and make long values readable or copyable.

- [ ] **11. Add desktop shortcuts.** Provide clearly labeled actions to open Windows Settings,
  Task Manager, and relevant storage or network settings. Use Windows-supported launch paths and
  run each action only after an explicit click.

- [ ] **12. Add assistant preferences.** Let the user choose which status cards appear and
  whether a single click opens the panel. Save these choices with the existing settings and
  provide a useful default layout.

- [ ] **13. Check assistant resource use and privacy.** Stop sampling when the panel closes,
  limit background work and memory use, and measure idle CPU and memory with the panel both
  closed and open. Confirm computer details remain local.

## Milestone 5 — More interactions and animations

More ways to play with Puffling directly, and more system activities worth a reaction. Pull
items into active work as they become a priority; each should get its own clip(s) and, where
it's a new gesture, a gesture-detection check the same way `PointerTracker` already distinguishes
click/double-click/drag/petting.

- [ ] **14. Scratch-behind-the-goggles gesture.** A slow circular drag on the character (distinct
  from the existing back-and-forth petting stroke) triggers a longer, more intense affection
  clip than plain petting.
- [ ] **15. Feed/treat action.** A tray or right-click "Give a treat" command plays a short,
  happy "eating" clip with a distinct bounce, usable as a quick way to cheer a Puffling up
  on demand (separate from the existing *Dance!*/*Come here* commands).
- [ ] **16. Peekaboo at screen edges.** When wandering near a monitor edge, occasionally have
  Puffling duck halfway off-screen and pop back with a surprised/happy clip, instead of only
  ever turning around at the margin.
- [ ] **17. Weather-reactive moods.** Using only local OS weather info if available (no new
  network calls otherwise), a rainy/snowy day gets a subtly cozier idle pose; skip entirely if
  no weather source exists rather than adding a new dependency.
- [ ] **18. Video call / meeting detection.** Recognize common conferencing apps (Teams, Zoom,
  Meet in a browser tab heuristically) as a system activity and have Puffling go quiet and still
  — no particles, minimal movement — so it doesn't catch the eye on a shared screen.
- [ ] **19. Build/test outcome reactions.** For the coding activity specifically, react to a
  local build or test run finishing (e.g. watch `dotnet build`/`dotnet test` exit codes via a
  lightweight hook, not constant polling): a happy clip on success, a surprised/concerned one on
  failure.
- [ ] **20. Low battery and charging reactions.** A brief, low-frequency check of battery state
  (laptops only) triggers a sleepy/concerned clip at low battery and a happy one when charging
  resumes, reusing the existing Drowsy/Happy clips rather than new art.
- [ ] **21. Gaming detection.** Recognize common full-screen games (or reuse `FullscreenDetector`)
  as its own system activity distinct from "movie," with an excited/cheering ambient clip for
  when the pet is still visible in windowed play.
- [ ] **22. Per-personality system-activity flavor.** Let the red/blue/green temperament bias
  *which* ambient reaction plays or how intense it is (e.g. green gets more energetic during
  `AgentBusy`), mirroring how clicks already differ per variant.
- [ ] **23. Idle mini-games against system activity.** While `AgentRunning` persists for a long
  stretch, let a bored Puffling start a tiny solo "waiting" fidget animation loop distinct from
  the short `AgentBusy` clip, so a multi-minute agent run doesn't look identical to a five-second
  one.
- [ ] **24. Notification history.** A small "what did I miss" tray item listing the last few
  system-activity and reminder bubbles, rendered in the same pixel font, for when a bubble was
  shown while the user was away.

## Later ideas

Candidate features worth considering once the milestones above are settled. Not yet scheduled —
pull one into an active milestone when it becomes a priority, and add acceptance criteria at
that point the way the items above have them.

- **25. Seasonal and time-of-day moods.** Subtle palette or pose shifts for night versus day, or
  a light seasonal accent (e.g. around winter holidays), without adding new settings surface.
- **26. More roster slots and customization.** Let a user rename a Puffling or pick its accent
  color instead of only the fixed red/blue/green trio, while keeping the "stable after restart"
  guarantee from item 3.
- **27. Snap-to-taskbar / window-aware perching.** Let Puffling land on the taskbar edge or avoid
  overlapping the currently focused window, instead of only floating over the desktop.
- **28. Idle screenshake-free screenshot/GIF capture command.** A tray action or hotkey that
  grabs a short GIF of Puffling's current animation, reusing the preview tool's renderer, for
  sharing without needing the dev tooling.
- **29. Localization pass.** Externalize the tray menu, settings window, and reminder bubble
  strings so a translation can be dropped in without touching behavior code.
- **30. Crash and update telemetry opt-in.** A strictly opt-in, local-first way to see why the
  app exited unexpectedly (e.g. a log file surfaced in the tray menu), with no network reporting
  unless the user explicitly enables it.
- **31. Auto-update channel.** Check GitHub releases for a newer build and offer to download it,
  reusing the existing "checks every five minutes" cadence pattern from Microsoft reminders.
- **32. Reduced-motion / accessibility mode.** A setting that caps animation intensity and
  disables flashing/particle effects for users sensitive to motion, distinct from the existing
  `AnimationIntensity` slider's normal range.
- **33. User-configurable activity signatures.** Let a settings page add extra process names to
  `SystemActivityDetector`'s lists (e.g. a niche IDE or player it doesn't recognize yet) without
  a code change.
