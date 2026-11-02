# Puffling to do

No particular order. Numbers are stable references, not priorities. Directions below are from the user's point of view.

1. [ ] **Face the direction of travel.** The Happy Ghast usually shows its right side and faces left in its resting pose. When it travels right, it should visibly turn right; when it travels left, it should face left. Check the renderer's fixed camera angle and the travel clip's yaw so both directions read clearly at normal desktop size. Keep turns smooth when movement starts, stops, or reverses.

2. [ ] **Give multiple Pufflings individuality.** With two or three present, they currently look and act too uniform. Give each one a persistent, recognizable variation in appearance and personality, such as subtle face or harness details, preferred idle poses, movement rhythm, and reaction timing. Keep the Happy Ghast design recognizable and avoid desynchronizing the deliberate pair and trio interactions.

3. [ ] **Let Pufflings cross between displays on their own.** They seem to treat the seam between monitors as a boundary, even though click-drag can move them across. Review destination selection, path visibility, and the return-to-screen behavior. A Puffling should occasionally travel from one connected display to another without being dragged, while avoiding actual gaps and staying inside usable screen areas.

4. [ ] **Fix README GIF frame smearing.** Some GIFs leave the previous frame visible, so frames pile up into a smear by the end. Regenerate and inspect every README GIF, including the pair and trio previews. Each frame should clear or dispose of the previous frame correctly, and the animation should loop cleanly on GitHub.

5. [ ] **Later: react to system activity.** Add lightweight, distinct reactions for events such as opening an app or playing music. Decide which signals are useful, then use Windows events or infrequent checks where possible. Keep monitoring off the render loop, limit background work and memory use, and verify that idle CPU use stays low.

6. [ ] **Open a computer overview by tapping Puffling.** Make a single click open a small, readable panel beside Puffling with a concise snapshot of the computer. Preserve dragging, petting, and the existing double-click dance; define how a second click closes the panel and how it dismisses when the user clicks elsewhere. Keep the panel on screen across monitors and DPI settings without stealing focus unnecessarily.

7. [ ] **Show useful live system status.** Include CPU and memory usage, available storage, battery level and charging state when applicable, network connection status, and uptime. Label units clearly, handle missing sensors gracefully, and refresh only while the overview is visible at a modest rate.

8. [ ] **Show computer details on demand.** Add an expandable details view for Windows version, processor, installed memory, graphics adapter, display information, and drive capacities. Keep the first view compact and make longer values readable or copyable.

9. [ ] **Make Puffling a useful desktop assistant.** From the overview, provide a small set of safe actions such as opening Windows Settings, Task Manager, and the relevant storage or network settings page. Show clear labels, use Windows-supported launch paths, and avoid actions that change system state without an explicit click.

10. [ ] **Let me choose what computer information appears.** Add settings to enable or hide individual status cards and choose whether the panel opens on a single click. Save those preferences with the existing settings and give the panel a sensible default layout.

11. [ ] **Keep computer information local and lightweight.** Collect only the data needed for visible features, keep routine polling off the render loop, and stop sampling when the panel is closed. Do not transmit device details. Check idle CPU and memory use with the panel closed and open.
