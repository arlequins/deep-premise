# Unseen Order — Deep Premise

**Version: 0.9.1 — prototype archive checkpoint.** Development direction is paused for redesign after player feedback that the objective and motivation remain unclear. This is not a finished game or a validated fun design.

Godot 4.7.2 .NET handles presentation; independent C#/.NET 8 cores handle simulation. Source and documentation are English; active prototypes support English and Korean.

## Preserved prototypes

| Prototype | Status | Entry point |
| --- | --- | --- |
| Type0: habit world | Archived playable experiment; autonomous work and learned local rules | `game/type0.tscn` |
| Type1: stolen factory | Concept debate only; never implemented | `docs/TYPE1_CONCEPT_REVIEW.md` |
| Type2: Riverside Days | Playable city observation experiment | `game/type2.tscn` |
| Type3: A World, Gathered | Latest playable ecology experiment; current default | `game/type3.tscn` |

Earlier conversation, colony, caravan, defense, and salvage experiments are retained in source. Their implementation status and the feedback that superseded them are summarized in [the checkpoint](docs/PROTOTYPE_CHECKPOINT.md). No prototype is removed or silently promoted to an approved final direction.

## Run locally

```powershell
./tools/run-debug.ps1                 # Type3, Korean
./tools/run-debug.ps1 -Type type2
./tools/run-debug.ps1 -Type type0
./tools/run-type3.ps1 -Language en
```

Launchers build with the local `.tools/dotnet` runtime and the configured Godot .NET engine. To prepare the engine and templates, run `python tools/setup_engine.py --templates`. Space pauses; F8 records a local feedback marker. Type3 uses dragging, light control, and a pouch paged three items at a time. Click the hollow after a first bloom to explore.

## Validate and package

```powershell
.tools/dotnet/dotnet.exe run --project tests/DeepPremise.Tests/DeepPremise.Tests.csproj
.tools/dotnet/dotnet.exe build DeepPremise.Godot.csproj --nologo
./tools/build.ps1
```

The portable package is `dist/Unseen-Order-0.9.1-Windows-x64.zip`. The build script reads the version from `project.godot` and produces SHA-256 checksums. Build outputs and local tooling are not committed.

## Saves and local feedback

Saves live in `%APPDATA%/UnseenOrder/`, with separate identities for each prototype. Type3 uses `type3-garden-v11.json`; it imports v10 only when no v11 save exists and preserves the old file. Fresh runs archive the current save first. Do not delete player saves when changing direction.

The player explicitly requested detailed local playtest recording. `artifacts/playtests/` stores actions, simulation decisions, exact state changes, and feedback markers. Current Type3 context and viewport captures are `artifacts/live/type3-context.json` and `type3-screen.png`. These remain local and are excluded from Git and exports. See [PLAYTESTING.md](docs/PLAYTESTING.md) for replay concepts and [TYPE3_GARDEN.md](docs/TYPE3_GARDEN.md) for current paths and rules.

## Architecture and limitations

- `src/DeepPremise.Core`: engine-independent simulation and diagnostics.
- `src/DeepPremise.Godot`: native rendering, controls, localization, and local persistence.
- `src/DeepPremise.Dialogue`: optional historical Luna voice adapter; unused by Type0, Type2, and Type3.
- `tests/DeepPremise.Tests`: deterministic simulation, interactions, migration, and replay checks.

No Steam integration, commercial release validation, or mobile port is included. Passing tests establishes behavior, not enjoyment. Historical conversation documentation is retained under `docs/archive/`.

Godot and font notices are in `game/assets`. No separate redistribution license is granted for original game code or content.
