# Game improvement fan-out, 2026-11-23

User request: improve the market game end to end with multiple agents. Named problems: fixtures in the middle of the road, no steal indicator and E doing nothing, no guard alert signals, everything too flat with weak contrast and outlines, market detail still incomplete.

Method: four read-only reviewers, one lens each, then two implementers on isolated worktrees with disjoint file ownership. Every finding must carry a file and line citation. The gate is ./tools/run-tests.sh, currently 143 tests, plus a recaptured Market.png.

Lenses:
- [x] Review A: blocked routes and placement. No fixture blocks a route, but every visual is shifted half a footprint into clear tiles, and CCTV panel t6a buries the panel point so power cut is impossible on foot. Guard waypoint 0 is a half tile off.
- [x] Review B: steal affordance and feedback. No in-range prompt, E failures silent, channel progress faked from key hold, no RF tag visual, no de-tagger or EAS cue.
- [x] Review C: guard alert signals and contrast. Snapshot lacks detection state, so no signal can be built; specified bubble, fan colors, flash, heat spine, vignette, pip, CCTV cones and outlines.
- [x] Review D: prop detail gaps. Unity has more fixture parts than web (408 vs 324); real gaps are store shell, blank aisle signs, empty containers, checkout and back-room decor, entrance glow.

Implementation workstreams, after synthesis:
- [x] Feedback worktree feat/feedback-signals, commit 7f859ee, gate 150 tests; own run confirmed 150. Owner verified.
- [x] Art worktree feat/market-detail, commit e85ae44, gate 156 tests. The reviewer's panel tile (27,22) would have blocked the only r5 to r6 doorway; agent chose local (0.4,3.0) instead, panel approach 0.5 tiles, doorway preserved.
- [x] Merged feedback then art into main with no conflicts. Combined gate: SUMMARY total=163 passed=163 failed=0 skipped=0, run by the owner.
- [x] Recaptured final Market.png and shown to the user.
