# Sixfold playable iteration

A twelve-encounter creature-building roguelike. The player drafts one organ per encounter, arranges six slots, invests food in healing or upgrades, and chooses ordinary or tougher opponents. Boss rewards persist within the run. Older prototypes and saves remain separate.

## Review and changes

- Replaced open-ended observation with an explicit final boss objective and discrete rewards.
- Fixed the mismatch between the six-slot display and orthogonal adjacency: the board is now three columns by two rows.
- Prevented replacing the final damage source with a support organ.
- Initial automated build policies won 179 of 180 runs. Increased late encounter health and attack; the same policies now win 105 of 180 runs. Every starter won its first encounter across 60 seeds. These are scripted policies, not evidence of human enjoyment.
- Corrected disabled controls that made the body and victory reward hard to read.
- Native Godot input smoke exercises starter selection, drafting, and starting combat. Screenshots are generated from the real viewport. This is scripted UI verification, not a manual play session.
- Save continuation, exact log replay, phase guards, detached state observation and all 180 bounded run endings are tested.

## Limits

This is a compact prototype, not a validated long-term retention design. It currently has nine organs, six relics, five ordinary enemy types and three bosses. The visual creatures are procedural placeholders. No paid AI or BGM is used. Balance estimates cover a simple greedy policy on ordinary routes; expert play and elite routes need further player evidence.

## Play and feedback

Run `powershell -ExecutionPolicy Bypass -File tools/run-sixfold.ps1`. Add `-FreshRun` to archive the current run and start again. Korean is the default; `-Language en` or the in-game toggle selects English. F8 records a feedback marker. Per-tick state deltas and player decisions are stored under `artifacts/playtests`; current context is in `artifacts/live/sixfold-context.json`. User saves use separate `sixfold-v13.json` and `sixfold-profile.json` files.
