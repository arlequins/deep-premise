# Development handoff — v0.9.1 checkpoint

## Current decision

Stop adding features. The player reports that the game objective is unclear and wants to rethink the direction. The immediate request is to organize, integrate, version, and tag all existing work. Do not interpret this archive checkpoint as approval to continue Type3 or begin another prototype.

Read `PROTOTYPE_CHECKPOINT.md` for the experiment history and `TYPE3_GARDEN.md` for the latest implementation. Historical v0.3 conversation guidance is in `archive/HANDOFF_CONVERSATION_V0_3.md`; it does not define the current direction.

## Persistent preferences

- Talk to the user in Korean; use English identifiers and development documentation.
- Preserve Godot 4 + C# with independent headless simulation cores.
- Favor visible agency, growth, understandable consequences, and systemic surprise.
- Keep the interface simple. Avoid dense panels, mandatory typing, repeated dialogue clicks, and an unclear objective.
- The user prefers calm simulation/observation over the rejected defense and action-oriented iterations.
- Preserve all prototype saves. Detailed local playtest logging is explicitly consented to.
- Optional game AI remains Luna-only; active garden and city simulations do not call AI.
- Do not spawn agents unless the user requests them. Type1's three-agent discussion is already recorded.

## Repository and verification

The default scene is `game/type3.tscn`. `run-debug.ps1 -Type type0` and `-Type type2` access the earlier playable prototypes. Type1 has no playable implementation.

The checkpoint includes all previously uncommitted experiment source, bilingual catalogs, scenes, tests, launchers, diagnostics, and design records. Runtime saves, local logs, SDKs, generated caches, and package binaries remain ignored.

Run the executable test suite and native viewer build before release packaging. Consult `CHANGELOG.md` and the release checkpoint document for version-specific evidence and limitations. Do not infer that a long test run establishes player enjoyment or a particular playtime.
