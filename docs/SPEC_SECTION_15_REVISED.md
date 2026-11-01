## 15. Final Experience

The finished application should make the desktop companion feel like a tiny living creature that shares the user's desktop.

It should float around naturally, smile when interacted with, dance when excited, sleep when tired, notice the mouse cursor, and occasionally wander across the screen.

**Icon-sized at all times.** The companion must stay desktop-icon-sized throughout every animation and behavior, not only while idle. Its footprint must remain within its configured size (48 × 48 logical pixels by default, adjustable from 32 × 32 to 96 × 96, scaled for each monitor's DPI) while it:

- dances, including spins, sways, and hops,
- travels or wanders across the screen, including leaning and trailing tentacles,
- is dragged, thrown, or settling after release,
- shows reactions such as surprise, excitement, or affection, and
- sleeps, wakes, or stretches.

No animation may temporarily enlarge the character, its window, or its clickable area. Expressive motion such as bobbing, hopping, and swaying moves the character as a whole; it does not grow it. Decorative effects such as sleep or heart particles must stay inside the same footprint. The size guarantee should be verifiable with an automated test that renders every animation at every supported size and confirms nothing extends beyond the canvas.

**The intended experience is a combination of a blocky, pixel-art floating creature, a virtual desktop pet, and a lightweight interactive Windows overlay.**

The companion should feel alive, adorable, and responsive while staying small and unobtrusive during everyday computer use, so that even at its most energetic it never becomes larger or more intrusive than a desktop icon.
