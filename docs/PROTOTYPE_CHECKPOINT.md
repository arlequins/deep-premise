# Prototype checkpoint — v0.9.1

Date: 2026-09-11. Status: archived experiments, awaiting a fresh design discussion.

## Player outcome

The latest feedback is that the player does not understand the purpose of play. Previous rounds also failed to sustain interest despite functioning simulation, additional content, and interface improvements. This release preserves evidence and implementation; it does not declare the design successful. No next concept is selected.

## Experiment history

| Experiment | What exists | Outcome / current status |
| --- | --- | --- |
| Original GDScript | Historical commit `3e86635` | Preserved before the C# migration |
| Conversation v0.3 | C# world, bilingual dialogue, optional Luna voice, multi-day favors, local replay | Player rejected mandatory/repetitive conversation and weak productive activity |
| Colony / caravan | Independent core sketches; colony map source | Not complete active games; conventional building and familiar travel concepts did not settle the direction |
| Defense / salvage | Defense and salvage viewers and simulation, relics, capture, feeding, mutations, expeditions | Felt too familiar and insufficiently engaging |
| Type0 / habit world | Autonomous workers, repeated behavior affecting local rules; explicit preserved scene | Rejected as unfun; retained as Type0 |
| Type1 / stolen factory | Three Astra agents proposed, challenged, and ranked folding and portable-factory concepts | Factory was only a conditional experiment preference; no Type1 game was built |
| Type2 / Riverside Days | Streets, homes, workplaces, markets, parks, commuting, immigration, growth, local identity | Calm city observation, but player still found it unfun |
| Type3 / A World, Gathered | Drag-based terrarium, warmth/light following, exploration, watering, pollination, four flower colors, reproduction, inheritance, renewable seeds, paged inventory | Latest playable prototype; purpose and motivation remain unclear to the player |

The numbered types are separate experiments, not a linear set of approved releases. Type0's source ZIP is a local auxiliary checkpoint; committed source and the explicit Type0 scene are the durable repository record.

## Retained technical assets

- Godot presentation separated from pure C# simulation.
- Deterministic fixed-step behavior and save continuation.
- English/Korean catalogs and natural-language revision work.
- Detailed local decisions, action traces, state deltas, feedback markers, and exact replay.
- Separate save identities; Type3 v10-to-v11 migration preserves the original save.
- Native mouse-input smoke modes and headless regression checks.
- Local debug launchers for Type0, Type2, and Type3.

## Checkpoint scope

The integration collects the earlier committed conversation work and all uncommitted prototype additions into one source checkpoint. `main` is the integration branch; the existing `feature/prototype-v1` history is retained. The annotated release tag is `v0.9.1`. Build output is a local portable Windows package; no GitHub release publication or default-branch setting change is implied.

Verified at this checkpoint: 183/183 headless checks; warning-free Godot project build; successful Windows portable export; packaged executable native-input smoke test in Korean (exit 0). Native Type3 basic and expanded smoke modes were also exercised in Korean and English during implementation.

Local package: `dist/Unseen-Order-0.9.1-Windows-x64.zip`.
SHA-256: `aa62f165934184db0e0e6f75d7c19dec3c1760cd8b79c6b320a9e85058ca7dbd`.
Checksums for the ZIP, executable, and PCK are in `dist/SHA256SUMS-0.9.1.txt`. The ZIP was created before the isolated packaged smoke run, so that run's logs and recordings are not included in the distributable.

## Next discussion

Before another implementation, establish a concrete player objective, the first meaningful decision, visible progress toward that objective, and a reason to choose another action after the first discovery. Preserve the quiet layout preference. Additional systems alone have not resolved the missing motivation.
