# Type2 — Riverside Days

A calm city observation prototype, requested after the Type1 concept review. Type0 remains in `game/type0.tscn`; Type1 remains a design document. Type2 is available explicitly through `game/type2.tscn`; Type3 is the current default scene.

## Play

Run `./tools/run-type2.ps1`. Korean is the default; pass `-Language en` for English. Select streets, homes, workshops, markets, or parks, then click an empty plot. Select Observe to inspect a building or resident. Resident selection reveals their route, work, preferences, and familiar place. Space pauses, the speed button cycles 1/3/8, and F8 records a feedback marker.

No loss timer, combat, or typing. Occupied homes and the entrance cannot be removed. Other structures can be removed for half their construction price. Street connectivity starts at the left-hand town entrance.

## Simulation

The pure C# `CityWorld` runs fixed ticks; 240 ticks form a day (approximately 53 seconds at normal speed). Residents commute, visit favored reachable parks or markets, and return home. Jobs, access, leisure, and workshop noise affect wellbeing. Daily income pays maintenance. Free beds, sufficient jobs, and wellbeing permit immigration. Happy homes expand from day four. Eighteen visits establish a gathering place or evening market, with additional wellbeing or jobs and visitor income. Population is capped at 72 for this prototype.

The first version uses one compact hand-authored river map. It is not a complete city builder and has no weather, utilities, disasters, live AI, or audio. Place growth is rule-based; no claim is made that its enjoyment has been validated by a player.

## Persistence and verification

- Separate save: `%APPDATA%/UnseenOrder/type2-city-v9.json` (atomic replacement).
- Local exact-state recordings: `artifacts/playtests/`, identified by `city-v9` in the manifest.
- Current debug context: `artifacts/live/type2-context.json`.
- Game-only screenshot: `artifacts/live/type2-screen.png`.
- Restore/review support through `DeepPremise.Playtest` includes version 9.
- `--smoke-city` isolates the save, runs a developed town, clicks the road tool and a plot through native input, captures the localized screen, and exits.
- Core checks cover deterministic continuation, growth, road disconnection, protected demolition, money, detached views, and exact recording restoration.
