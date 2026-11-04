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

- [ ] **4. React to system activity.** Add lightweight, distinct reactions to useful events such
  as opening an app or playing music. Prefer Windows events or infrequent checks and keep
  monitoring off the render loop.

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

## Later ideas

Candidate features worth considering once the milestones above are settled. Not yet scheduled —
pull one into an active milestone when it becomes a priority, and add acceptance criteria at
that point the way the items above have them.

- **14. Seasonal and time-of-day moods.** Subtle palette or pose shifts for night versus day, or
  a light seasonal accent (e.g. around winter holidays), without adding new settings surface.
- **15. More roster slots and customization.** Let a user rename a Puffling or pick its accent
  color instead of only the fixed red/blue/green trio, while keeping the "stable after restart"
  guarantee from item 3.
- **16. Snap-to-taskbar / window-aware perching.** Let Puffling land on the taskbar edge or avoid
  overlapping the currently focused window, instead of only floating over the desktop.
- **17. Idle screenshake-free screenshot/GIF capture command.** A tray action or hotkey that
  grabs a short GIF of Puffling's current animation, reusing the preview tool's renderer, for
  sharing without needing the dev tooling.
- **18. Localization pass.** Externalize the tray menu, settings window, and reminder bubble
  strings so a translation can be dropped in without touching behavior code.
- **19. Crash and update telemetry opt-in.** A strictly opt-in, local-first way to see why the
  app exited unexpectedly (e.g. a log file surfaced in the tray menu), with no network reporting
  unless the user explicitly enables it.
- **20. Auto-update channel.** Check GitHub releases for a newer build and offer to download it,
  reusing the existing "checks every five minutes" cadence pattern from Microsoft reminders.
- **21. Reduced-motion / accessibility mode.** A setting that caps animation intensity and
  disables flashing/particle effects for users sensitive to motion, distinct from the existing
  `AnimationIntensity` slider's normal range.
