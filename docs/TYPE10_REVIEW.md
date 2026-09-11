# Type10 review — 2026-09-11

Status: stop implementation and reconsider. This is a review of an unfinished local Type10 prototype, not an addition to the published v0.9.1 checkpoint.

## Method and limits

- Root launched the actual Godot viewer with a separate review project and save directory, then inspected its game-generated viewport screenshot.
- Direct Windows mouse play was attempted but could not be completed: window capture failed twice with `SetIsBorderRequired failed` / `0x80004002`. Do not describe this review as three agents manually playing the game.
- One independent agent inspected the core and executed alternative strategies in a temporary C# harness under `artifacts/type10-review-rules/`.
- A second independent agent reviewed the presentation, rules, objective, and previous player feedback. Its conclusions are source-based design judgments.
- No gameplay changes were made in response to the review. User saves were not used for the isolated review.

## Executed strategies

Times below are simulation ticks converted using the viewer's 0.1 seconds per tick, not measured human completion times.

| Strategy | Result |
| --- | --- |
| Switch off immediately and do nothing | Loss at 75.2 seconds |
| Keep the initial egg illuminated for 85 seconds, then switch off and do nothing | Win at 195.7 seconds |
| Keep the egg illuminated for 100 or 150 seconds, then switch off | Both win |
| Hold the hunter in light until the forest and lives mature, then switch off | Win at 120.6 seconds |

## Findings

The observation/freezing rule is striking as a premise. The current game does not translate it into sustained interesting decisions.

1. Waiting is a dominant solution. Illuminating the initial egg lets the forest mature without risk. Once the environment is ready, the player can release the egg and win without further intervention.
2. Protection has almost no cost. Light freezes hunger and growth together, and can indefinitely neutralize the only hunter.
3. The interesting living actions are hidden. Feeding, movement, growth, and reproduction happen in darkness; inspection stops them. Result notices and old silhouettes replace much of the process the player hoped to observe.
4. Information is ambiguous. Dim silhouettes are stored observations, not current living entities, but the interface does not clearly establish that distinction.
5. The lives lack identity. Three identical agents fill a counter; the game gives little reason to care about a particular individual.
6. The promised ending is weaker than its wording. Three adults surviving 24 seconds without light triggers victory and stops the simulation, despite a 72-second starvation interval. This does not establish a lasting independent ecosystem.
7. Restarting repeats the same fixed arrangement and timing. Discovering the rule once largely removes the uncertainty of subsequent runs.

## Conclusion

Not approved as a fun or sufficiently distinctive game in its current form. This is a short timing experiment with an evocative rule, rather than a compelling repeatable activity. More content is not justified until the recurring decision itself is worth making. No claim of worldwide originality was researched or established.

The review should inform the next discussion rather than trigger another immediate prototype or feature expansion.
