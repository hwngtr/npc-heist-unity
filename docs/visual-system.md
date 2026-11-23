# Frischemarkt visual and UI redesign system

Scope: presentation only. The simulation, perception, FSM, BT, heat, objectives, input mapping, and all 163 tests are frozen. No agent changes a Sim file or a gameplay rule.

## Design intent

A riso-printed supermarket you can read at a glance. Warm paper stock, a strict five-ink palette, hard-edged shapes, chunky ordered dither, strong silhouettes, calm typography. Every screen element earns its place. The player must always know: where I am, what I can do, who can see me, what I still need, and where to go.

## Palette, from ArenaSpec.cs

| Role | Hex | Use |
|---|---|---|
| Paper | #F3EBDD | Screen background, primary floor, quiet surfaces |
| Card | #FBF6EC | Raised panels, secondary floor, prompt backgrounds |
| InkBlue | #344A80 | Key ink, headings, structural accents |
| InkBlue2 | #56628A | Muted text, cool zone signature, quiet notches |
| Teal | #008C8A | Player identity, calm guard state, safe actions |
| Coral | #EE6852 | Danger, detection, EAS, primary action accents |
| CoralInk | #A8402D | Attack, refusal, deepest shadow, baseboard |
| Sun | #FFD428 | RF tags, prompts, price labels, investigation state |
| Black | #242226 | Outlines, eyes, wheels, hard ground shadows |

No gradients. No new colors. No transparent panels below 0.9 alpha over the world.

## Typography

TextMeshPro only, using the vendored MarketSans asset. Four roles:
- Kicker: 11 pt, uppercase, wide tracking, InkBlue2.
- Headline: 26 pt, InkBlue, used once per screen.
- Body: 15 pt, InkBlue, primary numbers and labels.
- Micro: 12 pt, InkBlue2, hints and secondary facts.

All text sits on Card or Paper. No text floats in the world except the guard state bubble.

## Screen layout

- Top-left status card, fixed 360 px wide: title, heat spine with six notches, loot over quota, bag count, five objectives with done markers.
- Top-right glance: guard state, FSM/BT badge, current zone name.
- Bottom-right: minimap, 12 room cells, player dot in teal, guard dot in coral, staff dot in sun, exit marker, current room highlighted. Bottom-left is reserved for the event feed, so the minimap must not occupy it.
- Bottom-center: interaction prompt, a Sun E badge plus one short line, only when an action is available. Refusals flash in CoralInk above it.
- Bottom-right: hint line and wheel-zoom note.
- Center edges: chase vignette and detection flash, brief and translucent.

## Section segregation, all 12 rooms from MarketData.Rooms

Each department gets one ink signature and one floor pattern from the web source: tiles, planks, concrete, checker. Boundaries are hard: a key-ink threshold with a doorway gap matching the walkable band. A geometric aisle sign with the department's icon color stands at each entrance. The HUD names the current department; the minimap highlights its cell. The player must be able to tell which department they are in without reading text.

## World art

Fixtures are silhouettes first: black backing shape, then ink body, then smaller product parts on camera-facing faces. Products read as recognizable objects at 18 tiles of view: cartons with gable tops, cylinder cans, bottles with caps, fruit as spheres in clustered piles, bread as cone-topped loaves. Baskets, crates, freezers and gondolas are stocked. Actors keep their identities: player teal with sun cap and ring, guard midnight blue with coral cap and badge, staff yellow. Large fixtures get visual borders that do not change collision.

## Navigation

Camera follows the player at 18 tiles, wheel zoom clamped 8 to 24. A directional arrow on the screen edge points to the next active objective when it is off screen. The EAS exit has a world beacon. The minimap is authoritative for room layout and actor positions.

## Verification

- `./tools/run-tests.sh`, baseline 163 tests, must stay green after each workstream and after merge.
- One 1600 by 900 capture of the rebuilt scene, judged by the user before any gameplay claim.
- No world-space TMP components except the guard bubble.
- Every agent cites the file it changes. No agent writes outside its assigned worktree.
