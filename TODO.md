# Puffling to do

Work through the active milestones in order. Item numbers are written into each checkbox so they remain visible in Markdown and can be used as references. The desktop assistant work at the bottom is deferred; do not start it until it becomes a priority again.

## Milestone 1 — Pet movement

- [x] **1. Face the direction of travel.** The Happy Ghast usually shows its right side and faces left at rest. Make travel right visibly face right and travel left face left. Check the fixed camera angle and travel clip yaw at normal desktop size, with smooth turns on starts, stops, and reversals.

- [ ] **2. Cross between displays autonomously.** Review destination selection, path visibility, and return-to-screen behavior so Pufflings occasionally travel between connected displays without being dragged. Avoid real gaps and keep them in usable screen areas.

## Milestone 2 — Pet behavior

- [x] **3. Give multiple Pufflings individuality.** Give the three roster slots red, blue, and green accents with distinct faces and temperaments, individual actions, and shared chase, bounce sparring, and pong-like play. Keep their identities stable after restart and preserve synchronized pair and trio interactions.

- [ ] **4. React to system activity.** Add lightweight, distinct reactions to useful events such as opening an app or playing music. Prefer Windows events or infrequent checks and keep monitoring off the render loop.

## Milestone 3 — Presentation

- [x] **5. Fix README GIF frame smearing.** Regenerate and inspect every README GIF, including pair and trio previews. Each frame should clear or dispose of the previous frame correctly and loop cleanly on GitHub.

## Deferred — Desktop assistant (lowest priority)

These items were the former assistant milestones. Keep them as a future plan, but leave them inactive until the desktop pet work is done or the assistant becomes a priority again.

- [ ] **6. Define the tap interaction.** Make a single click available to open an assistant panel while preserving dragging, back-and-forth petting, the double-click dance, and clicks that wake a sleeping Puffling. Decide how a second click closes the panel and how clicking elsewhere dismisses it.

- [ ] **7. Build a local computer-information service.** Add a Windows-side provider for system status and computer details, separate from the renderer and animation loop. Handle unavailable sensors and disconnected devices gracefully. Collect only the data needed for visible features and never transmit device details.

- [ ] **8. Build the assistant panel shell.** Show a small, readable panel beside the tapped Puffling. Keep it inside the usable screen area on multiple monitors and mixed-DPI setups, and avoid taking keyboard focus unnecessarily. Make it possible to add cards and actions without changing the pet's rendering code.

- [ ] **9. Add the computer overview.** Show clearly labeled CPU and memory usage, available storage, battery level and charging state when applicable, network connection status, and uptime. Refresh at a modest rate only while the panel is visible.

- [ ] **10. Add computer details on demand.** Let the user expand the panel to see the Windows version, processor, installed memory, graphics adapter, display information, and drive capacities. Keep the default view compact and make long values readable or copyable.

- [ ] **11. Add desktop shortcuts.** Provide clearly labeled actions to open Windows Settings, Task Manager, and relevant storage or network settings. Use Windows-supported launch paths and run each action only after an explicit click.

- [ ] **12. Add assistant preferences.** Let the user choose which status cards appear and whether a single click opens the panel. Save these choices with the existing settings and provide a useful default layout.

- [ ] **13. Check assistant resource use and privacy.** Stop sampling when the panel closes, limit background work and memory use, and measure idle CPU and memory with the panel both closed and open. Confirm computer details remain local.
