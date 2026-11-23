# NPC Heist, Unity port

Unity 6 (6000.6.10f1) port of the browser stealth heist in `../npc-stealth`: both levels, the FSM and reactive BT guard controllers, the perception and heat systems, and the comparison evidence, rebuilt so every guard parameter is editable in the Inspector while the game runs.

The web game remains the graded Project 2 prototype. This project is the engine port.

## Open it

Unity Hub is expected at `/usr/bin/unityhub` with editor `6000.6.10f1` installed at `~/Unity/Hub/Editor/6000.6.10f1`.

1. Open Unity Hub, sign in with the student license.
2. Add, then add project from disk, and pick this folder.
3. First import compiles the scripts and creates `Library/`. It can take a few minutes.

## Run the tests

Batch mode, from this directory:

```bash
./tools/run-tests.sh
```

Or in the editor under Window, General, Test Runner, EditMode, Run All.

## Layout

- `Assets/HeistSim/Scripts` — the sim: percepts, intents, profiles, FSM, BT, perception, heat, collision, navigation. No MonoBehaviour input handling below this line.
- `Assets/HeistSim/Scripts/Runtime` — MonoBehaviours: guard, player, cameras, HUD. Thin adapters over the sim.
- `Assets/HeistSim/Tests/EditMode` — NUnit tests, including the FSM/BT trace parity suite.
- `Assets/HeistSim/Data` — guard profiles as ScriptableObjects and the level fixture data.
- `tools/run-tests.sh` — batch-mode test invocation pinned to the installed editor.

## Port contract

The C# controllers consume `GuardPercepts` and emit `GuardIntent`, mirroring `src/npc/sim/contracts.py` and `src/npc/sim/perception.py` in the source repo. The FSM table and BT structure mirror `src/npc/ai/fsm.py` and `src/npc/ai/bt.py` rule for rule. Parity is asserted by tests, not asserted by hand.
