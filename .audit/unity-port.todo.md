# Unity port todolist

Definition of done, updated 2026-11-23 for the trimmed scope: the Frischemarkt market game is playable in Unity with keyboard, the guard runs FSM and BT controllers on identical GuardPercepts and GuardIntent contracts with an in-play switch, all guard math lives in Inspector-editable assets, the Unity Test Framework suite is green including trace parity, and the comparison numbers regenerate in engine. The house level is dropped per the user's market-only call. The web game stays the graded milestone.

Rigor: high on parity, tests precede features each stage. Baselines captured from the web repo: the 240-episode comparison numbers, the verify-script behavior lists, the tile-for-tile collision grid.

Phases:

- [x] Stage 0a: web perf fix, pixel budget cap on the canvas backing store, e98f619 in npc-stealth.
- [x] Stage 0b: Unity project scaffold, README, version pin 6000.6.10f1, manifest.
- [x] Stage 1: AI core port. Contracts, profiles as ScriptableObjects, table FSM, reactive BT, perception. Gate green twice, delegate and owner: 29 of 29.
- [x] Stage 2: sim domain. Heat, tiers, detection, channel, bag, objectives, BFS navgrid. Gate: 79 of 79.
- [x] Stage 3: rendering arena. Three looks plus a review-driven hybrid, LookD fixed for floor contrast and bayer scale, verified readable, user confirmed market-only scope with LookD.
- [x] Stage 4: market level. 4a data and grid, 972 tile centers cross-checked against the web game, 300 blocked tiles exact. 4b the full game loop as pure C#, FSM and BT traces identical through the scripted run. 4c the playable scene, 78 fixtures, HUD, input, riso capture pushed to the user review pane, gate 119 of 119 on the owner rerun, input handlers set to both. Final gate is the user's playtest.
- [x] Stage 5: house level. skip: user chose market-only scope.
- [ ] Stage 6: comparison regeneration in C#. Seeded episodes, results and summary equivalents, the report gains a Unity column, diagrams regenerate from the C# FSM table. Gate: trace agreement across the seeded suite.
- [ ] Stage 7: Unity-native workshop. Editor window for fixtures and profiles. Gate: an Inspector edit to a profile measurably changes catch rate in the test that reads it.
- [ ] Follow-up polish: expose guard angle, camera angles, and channel progress in HeistSnapshot so the presentation reads sim truth instead of mirroring formulas, flagged during 4c.
