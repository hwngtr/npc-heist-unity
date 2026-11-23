# Game improvement fan-out, 2026-11-23

User request: improve the market game end to end with multiple agents. Named problems: fixtures in the middle of the road, no steal indicator and E doing nothing, no guard alert signals, everything too flat with weak contrast and outlines, market detail still incomplete.

Method: four read-only reviewers, one lens each, then two implementers on isolated worktrees with disjoint file ownership. Every finding must carry a file and line citation. The gate is ./tools/run-tests.sh, currently 143 tests, plus a recaptured Market.png.

Lenses:
- [x] Review A: blocked routes and placement. No fixture blocks a route, but every visual is shifted half a footprint into clear tiles, and CCTV panel t6a buries the panel point so power cut is impossible on foot. Guard waypoint 0 is a half tile off.
- [x] Review B: steal affordance and feedback. No in-range prompt, E failures silent, channel progress faked from key hold, no RF tag visual, no de-tagger or EAS cue.
- [x] Review C: guard alert signals and contrast. Snapshot lacks detection state, so no signal can be built; specified bubble, fan colors, flash, heat spine, vignette, pip, CCTV cones and outlines.
- [x] Review D: prop detail gaps. Unity has more fixture parts than web (408 vs 324); real gaps are store shell, blank aisle signs, empty containers, checkout and back-room decor, entrance glow.

Implementation workstreams, after synthesis:
- [ ] Feedback worktree /home/hwngtr/Projects/npc-heist-unity-feedback, branch feat/feedback-signals. Owns MarketRunner.cs, MarketHUD.cs, GuardVisionFan.cs, HeistGame.cs snapshot additions, new feedback components and their tests. Must NOT commit the built scene.
- [ ] Art worktree /home/hwngtr/Projects/npc-heist-unity-art, branch feat/market-detail. Owns FixtureShapeData.cs, DetailFactory.cs, MarketLevelBuilder.cs, MarketData fixture placement, new art data and their tests. This is the only worktree that rebuilds and commits Market.unity and the capture.
- [ ] Merge feedback first, then art, verifying the combined gate and the final capture.
- [ ] Recapture the final Market.png and show it to the user.
