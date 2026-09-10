# The watched route — playable experiment

The player approved a production and deception defense prototype after reopening the concept review. The active scene now uses DefenseMain; the older conversation implementation and saves remain preserved. Colony and caravan sketches are inactive.

Run `./tools/run-debug.ps1 -Language ko` from the repository. Space pauses/resumes, F8 records a feedback marker. English is available in the sidebar. The game starts paused and saves separately to `defense-v6.json` in the Godot user directory.

Three lanes carry visible cargo. Workers deliver supplies automatically; supplies recruit workers, repair the cart or fund false cargo. A scout only reports deliveries or decoys witnessed on its lane. Interception prevents reporting. Attacks use that saved observation even after hauling orders change. The player resolves a public split between the quartermaster and pathfinder during the first eligible encounter. Survive six cycles to finish an expedition.

This is a small mechanics experiment, not a finished survival game. The three routes, fixed encounter schedule, single guard and two council preferences are deliberately simple. Seed variation currently changes initial routes and scout lanes only. It does not yet deliver the intended breadth of emergent world behavior. No AI API is called.

Local playtests record orders, accepted/rejected decisions, state deltas and hashes. `artifacts/live/context.json` identifies the active process, session and current state; `screen.png` captures only the game viewport. Replay supports defense saves. The older playtest review CLI may still expect conversation-specific fields; use PlaytestReplay for exact reconstruction.

Validation: 81 checks passed including 11 defense checks covering deterministic continuation, evidence boundaries, rerouting, interception, voting and exact playtest reconstruction. Native Korean startup was inspected at 1440 × 900.
