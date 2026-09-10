# The living machine — 0.6.0

The current experiment treats enemies as threats, ammunition, food, and machine components. Drag an enemy to a landing point; it flies across lanes, then bowls rightward, carrying resin and fire into collisions. Landing it in a living magnet captures its species. Runners become turrets, sprinters accelerate neighboring machines, armored creatures provide breach shields, and brutes discharge area damage. Feeding the same species grows the machine to level three; another species changes its function. Captives get hungry during combat and break out inside the player's own defense if neglected. The bar above the machine shows the countdown.

The opening includes a coil, a magnet, an oiled creature and a nearby crowd to make the first throw immediately usable. During aiming, the held target is protected and time slows. Cancel and expiry consume cooldown. Between waves, drag whole machines to rearrange them while preserving their creatures. Selling discards the creature.

New adventure runs have stronger health scaling, faster later waves, and lower passive kill income than the earlier basic test scenario. Collectors cannot farm a stalled final enemy. Escaped captives cannot generate repeat capture/kill scrap.

From wave two, plan an optional courier expedition before launch. Recall with intermediate scrap, or risk reaching a survivor or relic deeper in the lane. A deadline prevents risk-free post-combat extraction. Rescued crew give persistent benefits. Three optional relics change rules: pull enemies toward a double-damage danger zone; borrow stronger pushback at the cost of future enemies; spread statuses across deflector destinations. All effects are disclosed when equipped.

The implementation followed three independent Astra design/playtest reviews requested by the player. Those reviews identified a solved late-dispatch strategy, invisible sling collisions, passive-income stalling, and a weak opening opportunity. These were addressed in code. This is not evidence that subjective enjoyment has been established.

The active game is now a machine-combination survival run. Previous conversation, colony, caravan and watched-route implementations remain inactive and their saves are preserved. The player authorized a fresh implementation and requested results rather than explanations of the previous design.

## Play

Run `./tools/run-debug.ps1 -Language ko`. Select one of five machines (keys 1–5), then click a deck cell. Place an arc coil in each lane for an accessible opening. Enemies approach from the right. Click an existing machine to upgrade; right-click to repack with a full refund between waves. Space launches or pauses. Q/W/E or a lane click fires a shared-cooldown pushback pulse. Pick one of three seeded rewards after clearing a wave. Survive eight waves, including heavy enemies on waves four and eight.

Resin slows and conducts electricity; fire detonates resin. Deflectors move enemies into other lanes. Magnets generate scrap while fighting. Weather changes conduction or movement speed. Enemy resistance responds to the dominant attack type used in the previous wave; the next resistance is disclosed before deployment. This is a deliberately short run, not a claim that the long-term world simulation design is complete.

## Persistence and diagnostics

Pure C# `SalvageRun` has no engine dependency. Seeded xorshift randomness and fixed 20 Hz ticks determine combat. Saves use a separate `salvage-v7.json` with atomic replacement. Mid-fight loads pause before advancing. New runs create a separate local recording whose initial state preserves the previous run through its own session history.

Local playtests include player orders, accepted/rejected results, hits and their resolved damage, spawns, combinations, wave outcomes, state deltas, hashes and F8 markers. `artifacts/live/context.json` identifies the current process/session/state; `screen.png` is the game viewport. The replay and review CLI now support version 7. There are no live AI calls or music.

## Validation

126 checks passed, including behavioral comparisons of aimed versus missed throws, protected aiming, capture traits, hunger breakouts, feeding/mutation, partial extraction versus greed, debt repayment, deterministic continuation and exact log reconstruction. One crisis test loses when idle but survives when the player throws an armored enemy into a magnet to create a shield before a breach. Native mouse press/drag/release smoke testing confirms actual capture through the UI; a separate native extraction smoke verifies partial cargo recall. English and Korean screenshots were inspected at 1440 × 900. Smoke mode uses an isolated save.

Run `./tools/run-debug.ps1 -Language ko -FreshRun` to archive the current save and start the new opening. Existing saves remain loadable. The 12-seed scripted balance check still targets the basic simulation fixture; it should not be misreported as balance validation for the harder adventure mode.

A separate four-seed adventure-mode comparison was run after the harder scaling change. The same reinvesting build script without active abilities lost on waves 5–7 in all four seeds; adding aimed capture/feeding and lane pulses survived wave 8 in all four, with 14–18 hull. This demonstrates a consequential active strategy in those fixtures, not proof of player enjoyment or exhaustive balance.
