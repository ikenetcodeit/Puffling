# Puffling to do

No particular order. Directions below are from the user's point of view.

- [ ] **Face the direction of travel.** The Happy Ghast usually shows its right side and faces left in its resting pose. When it travels right, it should visibly turn right; when it travels left, it should face left. Check the renderer's fixed camera angle and the travel clip's yaw so both directions read clearly at normal desktop size. Keep turns smooth when movement starts, stops, or reverses.

- [ ] **Give multiple Pufflings individuality.** With two or three present, they currently look and act too uniform. Give each one a persistent, recognizable variation in appearance and personality, such as subtle face or harness details, preferred idle poses, movement rhythm, and reaction timing. Keep the Happy Ghast design recognizable and avoid desynchronizing the deliberate pair and trio interactions.

- [ ] **Let Pufflings cross between displays on their own.** They seem to treat the seam between monitors as a boundary, even though click-drag can move them across. Review destination selection, path visibility, and the return-to-screen behavior. A Puffling should occasionally travel from one connected display to another without being dragged, while avoiding actual gaps and staying inside usable screen areas.

- [ ] **Fix README GIF frame smearing.** Some GIFs leave the previous frame visible, so frames pile up into a smear by the end. Regenerate and inspect every README GIF, including the pair and trio previews. Each frame should clear or dispose of the previous frame correctly, and the animation should loop cleanly on GitHub.

- [ ] **Later: react to system activity.** Add lightweight, distinct reactions for events such as opening an app or playing music. Decide which signals are useful, then use Windows events or infrequent checks where possible. Keep monitoring off the render loop, limit background work and memory use, and verify that idle CPU use stays low.
