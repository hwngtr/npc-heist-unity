# Full presentation redesign, 2026-11-19

User request: completely redesign the Unity game's UI and graphics top to bottom with multiple agents. Keep the philosophy, project requirements, NPC mechanisms and every gameplay rule identical. Make it better looking, clearer section segregation, easier navigation.

Shared design system: docs/visual-system.md, authored by the coordinator before fan-out. It pins the five-ink palette, four type roles, screen layout, 12 room signatures, navigation and verification rules.

Worktrees, all based on main 516c14b:
- redesign/ui at /home/hwngtr/Projects/npc-heist-ui. Owns MarketHUD.cs and new Market/UI files. Runtime-built widgets only.
- redesign/navigation at /home/hwngtr/Projects/npc-heist-nav. Owns MarketRunner.cs and new Market/Navigation files. Minimap, objective arrow, exit beacon, camera.
- redesign/world at /home/hwngtr/Projects/npc-heist-world. Owns Editor files, materials, Market.unity and its capture. Only worktree that rebuilds the scene.

Verification: each agent gates at 163 tests. Coordinator merges UI, then navigation, then world, reruns the combined gate, recaptures and shows the user. No push.

- [ ] UI agent result
- [ ] Navigation agent result
- [ ] World agent result
- [ ] Three merges, combined gate, final capture, user review
