# Unity port plan and todolist

Definition of done, falsifiable: Unity project at this path where both levels are playable with keyboard, the guard runs FSM and BT controllers on identical GuardPercepts and GuardIntent contracts with an in-play switch, all guard math lives in Inspector-editable ScriptableObjects, the Unity Test Framework suite is green including a trace parity test, the comparison numbers regenerate in engine, and fullscreen holds 60 fps at 1080p. The web game stays the graded milestone.

Rigor: high on parity, tests precede features each stage. Medium on visuals, decided by arena with the user picking from rendered screenshots. Low on scaffolding. Baselines captured from the web repo: the 240-episode comparison numbers, the verify-script behavior lists, the blits and fps readout.

Phases:

- [x] Stage 0a: web perf fix, pixel budget cap on the canvas backing store, landed as e98f619 in npc-stealth.
- [x] Stage 0b: project scaffold, README, version pin 6000.6.10f1, manifest, layout.
- [ ] Stage 1: AI core port. GuardPercepts, GuardIntent, GuardProfile ScriptableObject, table FSM, reactive BT, perception. EditMode parity and perception tests. Gate: batch-mode Test Runner green.
- [ ] Stage 2: sim domain port. Heat and tiers, objectives, timed channel, collision, BFS navgrid. EditMode tests mirror verify-market-heist cases. Gate: tier thresholds and channel rules match the web game.
- [ ] Stage 3: rendering arena. Three riso approaches rendered as screenshots, user picks one. Gate: user decision recorded.
- [ ] Stage 4: market level playable. Fixtures data, input, camera, HUD, CCTV sweep, guard and staff movement, radio backup. Gate: PlayMode scenario test passes.
- [ ] Stage 5: house level port. Gate: same.
- [ ] Stage 6: comparison regeneration in C#. Seeded episodes, results.csv and summary.json equivalents, report gains a Unity column, diagrams regenerate from the C# FSM table. Gate: 100 percent trace agreement across the seeded suite.
- [ ] Stage 7: Unity-native workshop. Editor window for fixtures and profiles. Gate: an Inspector edit to a profile measurably changes catch rate in the test that reads it.
