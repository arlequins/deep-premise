# Type1 concept review — 2026-09-10

Status: design discussion only. No Type1 gameplay has been implemented or playtested. Type0 remains available; see `TYPE0.md`.

## Method

Three GPT-6 Astra agents independently proposed an idea, exchanged objections directly, and ranked candidates after rebuttals. The lead challenged whether a small action puzzle answered the player's request for a larger, visibly growing game. Votes are design judgments, not evidence of fun.

## Independent proposals

| Reviewer | Proposal | Immediate action | Fatal risk |
| --- | --- | --- | --- |
| Action | Folded City | Fold hinged floors into stairs and shields; pull an exit toward the character while moving. | Preset hinges become switches; free rotation becomes hard to read and control. |
| World | Folded Continent | Fold terrain to connect paths, water, and ecosystems, then traverse the changed landscape. | Same folding mechanic as City, with more difficult causal and spatial interpretation. |
| Skeptic | Stolen Factory | Cut working parts out of a hostile factory and eject copies to traverse or disrupt its machinery. | Correct-tool puzzles, unlimited bridges, clutter, or novelty without a lasting game. |

City and Continent were merged into one mechanic family during discussion. The World reviewer withdrew the ecological expansion as an independent candidate.

## Rebuttals and revisions

- Folding reviewers acknowledged that the need for real-time manipulation was not established. More ecology would increase scope before solving the input problem.
- Factory initially proposed three active copies, no resource collection, and immediate ejection while moving. The fourth copy must visibly preview which old copy will disappear. This limit itself remains a design hypothesis.
- Mold combination was challenged as a hand-authored recipe table rather than emergent depth. It is excluded from the initial scope.
- The lead and Action reviewer rejected room-clearing and an expanding tool inventory as sufficient answers to productive, visible growth.
- Factory was revised around stealing a moving factory's major components to grow a personal escape machine: handheld press, then rideable press cart, eventually an engine that ejects its own path. Large components attach to fixed sockets without a construction menu. Size and weight should change available routes, with a choice to discard capability for access or speed.
- Action judged this a concrete improvement but retained opposition to full concept adoption: the handheld-to-vehicle transition introduces another untested claim.

## Current decision

Stolen Factory is the first experiment candidate. Folding is second. There is no claim that a finished Type1 design has passed review or that any candidate is fun. The Action reviewer explicitly withholds full adoption; World distinguishes experiment priority from concept approval.

Working player-facing premise: steal the factory that is trying to crush you, piece by piece, until your own machine can break out.

## Proposed experiment, not implementation authorization

1. Validate direct action in one readable fixed-camera space: two recoverable part types, at least two uses per type, one interaction between them, a conveyor, and a pursuing press. No recipes, permanent upgrades, story systems, or sprawling ecology.
2. Test a continuous 10–12 minute route with one visible growth transition from handheld press to rideable machine. Include a meaningful route tradeoff created by that growth. Do not claim growth was tested with a single-room toy alone.

Observe voluntary alternative attempts after an initial success, understandable useful accidents, and willingness to replay. If only one prescribed solution is used, or the vehicle adds hauling and friction instead of new decisions, reject or redesign the experiment before adding content. These are future checks; no tester counts or outcomes have been collected.

If later implemented, retain the independent C# core and Godot presentation, English source and documentation, natural Korean and English UI, and detailed consented local diagnostics. Use separate Type1 source, scene, and save identity. Preserve the Type0 checkpoint and existing saves.
