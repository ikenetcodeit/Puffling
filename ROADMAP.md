# Puffling roadmap

A living plan of features to add, grouped by milestone. Work through the active milestone in
order; item numbers are written into each checkbox so they remain stable references even as
items are checked off. The **Desktop assistant** and **Later ideas** sections are deferred —
don't start them until the active milestones above them are done or they're explicitly
reprioritized.

## Milestone 1 — Pet movement

- [x] **1. Face the direction of travel.** Travel right visibly faces right and travel left
  faces left, with smooth turns on starts, stops, and reversals.

- [x] **2. Cross between displays autonomously.** `TravelBehavior` already picked destinations via
  `ScreenGeometry.RandomFarPoint`, which validates the straight path with `PathIsVisible` so it
  never aims through a real gap between monitors — but the random monitor pick made an actual
  crossing a rare coincidence, and two generic safety nets (`Brain.EvaluateContext`'s
  `ReturningBehavior` hijack and `ScreenEdgeCollision.Bounce` in `Companion.Tick`) could still fight
  a crossing in progress at the exact seam where two monitors' deflated safe areas touch, the same
  class of bug fixed for `WatchingMovie` in item 38. `RandomFarPoint` now spends most of its
  attempts targeting a monitor other than the current one before falling back to any monitor, so
  cross-display travel happens occasionally on its own rather than by chance. `TravelBehavior` now
  sizes its timeout to the actual distance so a long cross-monitor hop isn't cut off mid-flight, and
  both `Travel` exemptions mirror `WatchingMovie`'s in `EvaluateContext` and `Companion.Tick`. See
  the new `BehaviorTests.Travel_OccasionallyCrossesToAnotherMonitor_WithoutDriftingThroughTheGap`.

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

- [x] **35. Give companions a collision.** Pufflings no longer overlap: every frame,
  `Puffling.Core.Movement.CompanionCollision` pushes any overlapping pair apart and exchanges the
  along-normal component of their velocity with a soft restitution (0.35, tuned down from a hard
  billiard-ball swap), so flicking one into another gives it a satisfying but gentle shove rather
  than passing through it or bouncing off hard. A pet being actively dragged
  is never repositioned by a collision (it stays glued to the cursor) but still shoves whatever it
  hits, using the drag gesture's real velocity rather than its frozen `Motion.Velocity`.
  Choreographed group routines (dance/chase/spar/pong/staring contest) are skipped since those
  already hold pets at deliberate close offsets.

- [x] **36. Bounce instantly off the screen edge.** Free motion (chiefly a post-flick glide) no
  longer drifts noticeably off-screen before the slower `ReturningBehavior` steers it back. Every
  frame, `Puffling.Core.Movement.ScreenEdgeCollision` clamps a companion to the nearest safe point
  the moment it would stop being comfortably visible and reflects the velocity component that
  caused the overshoot, so it bounces off the edge immediately. Multi-monitor layouts are
  unaffected — adjacent monitors still read as one continuous safe area, so travelling between
  displays never counts as "off screen." Dragged pets are exempt (never fight the user's hand).

- [x] **37. Let Green decline Red's invitations.** When Red specifically draws Green into a duo
  (see item 34), Green now sometimes turns the invitation down outright: Red plays a brief,
  good-natured `BehaviorKind.Declined` shrug while Green is left completely untouched, continuing
  whatever it was already doing. A decline is a quick, minor beat rather than a "major"
  interaction, so it only costs a short 25-45s retry instead of the full duo cooldown. See
  `GroupInteractionCoordinator.TryStartPair` / `IsRedInvitingGreen`.

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

## Milestone 6 — Personality & social expansion spec

Tracks a user-provided design spec ("Puffling Companion — Personality, Social Interaction, and
System Awareness Expansion") against what's already built. Its architecture names map onto
existing code rather than new classes, per the spec's own guidance to reuse equivalents:
`PersonalityProfile` → `CompanionVariant`/`CompanionVariants`; `RelationshipManager` → the
`Affinity()` weighting in `GroupInteractionCoordinator`; `SocialInteractionScheduler`/
`SocialInteractionController` → `GroupInteractionCoordinator`; `ContextActivityDetector` →
`SystemActivityDetector`; `NotificationManager` → `SpeechBubbleWindow` + `CompanionApp.Say`.

**Already satisfied** by items 3, 4, 34, 35, 36, 37, and the existing click/variant reactions:
distinct personalities per variant; blue+red pairing most often, red initiating with green
(including Green sometimes declining); green favoring independent activity; the ≥180s global
cooldown on major (full-group) interactions with a shorter, separate cadence for minor ones;
pair/group routine variety (dance, chase, spar, pong, staring contest) without back-to-back
repeats (`_routineIndex`/`_pairRoutineIndex` cycling); the solo "looking for someone" reaction
gated to exactly one companion present; music/video/coding/agent ambient reactions; pixel-font
notifications. Eligibility already excludes paused/asleep/cross-monitor/mid-activity pets
(`AllEligible`).

- [x] **38. Video-watching mode: gather and face the screen.** On a multi-monitor rig with
  fullscreen video detected, an interested companion (Blue most, Red sometimes, Green rarely —
  `Brain.Video`) relocates to a different monitor and faces back toward the video the whole time,
  instead of only sitting in an ambient pose on top of it; single-monitor fullscreen video is
  covered by the existing generic fullscreen-hide (`FullscreenDetector`/`HideInFullscreen`), so no
  separate handling was needed there. For *windowed* video, movement is quieter near the window
  (`Brain.Speed`'s `VideoSlowdown`) and `Wander`/`Travel` targets are nudged outside its bounds
  (`Brain.AvoidVideo`) so companions never cross into it; uninvolved companions carry on normally.
  See `WatchingMovieBehavior` and `CompanionApp.UpdateVideoViewport`. Not included: a popcorn
  prop (new sprite art, left for later) and distinguishing "D3D fullscreen, unknown monitor"
  video from other fullscreen apps (falls back to the generic hide/ambient-pose behavior).
  Fixing this also surfaced and fixed a real bug: `ScreenEdgeCollision`'s bounce-back (item 36)
  and the general off-screen safety net (`EvaluateContext`) were both fighting any deliberate
  cross-monitor move at the exact seam where two monitors' margin-deflated safe areas touch —
  both now exempt `WatchingMovie`, which validates its own path with `Geometry.PathIsVisible`.

- [ ] **39. Distinguish AI-agent lifecycle states.** `AgentBusy` is currently a single ambient
  pose for "some agent process is running." Expand `SystemActivity.AgentRunning` into distinct
  idle/running/waiting-for-input/succeeded/failed signals wherever a reliable integration exists
  (explicit lifecycle events, not inferred from a terminal merely being open), with matching
  clips: supervising, impatient (green wandering off), celebrating on success, puzzled on failure.
- [ ] **40. React to Windows toast notifications.** Distinct from Puffling's own bubbles (item 24
  covers a history of those): have a nearby, available Puffling glance toward a real incoming
  Windows notification via the supported notification-listener APIs, occasionally with a subtle
  alert clip — without reading or exposing notification content, and without triggering every
  visible Puffling at once.
- [ ] **41. More unique per-variant idle flourishes.** The spec lists many small per-personality
  animations beyond what exists today (e.g. Blue: shy hiding, gentle humming, briefly admiring
  something; Red: confident hovering, inspecting active windows, checking on everyone; Green:
  challenging poses, mischievous staring, turning away when uninterested). Add a handful at a
  time as new idle-weighted clips per variant rather than all at once — each needs its own clip,
  a `ChooseNext`/`CompanionVariants` weight, and canvas-safety coverage in `AllClips`.
- [ ] **42. More environmental/emergent behaviors.** From the spec's list, still open: resting
  preferentially at a remembered favorite spot, occasionally visiting another Puffling's favorite
  spot, inspecting a desktop icon without activating it, a rare trip-and-recover stumble, and a
  companion reacting to watching another one sleep. (Watching the cursor and reacting to a
  disconnected monitor already exist via `CuriousBehavior` and `UpdateGeometry`.)

Items 19 (build/test outcomes), 20 (battery), 21 (gaming detection), 22 (per-personality system-
activity flavor), and 24 (notification history) in Milestone 5 already cover other parts of this
spec's System-Aware Activities and Notifications sections — no duplicate items needed for those.

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
