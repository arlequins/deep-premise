# Windows development handoff — 2026-09-08

## Immediate scope

The user is moving development from macOS to Windows. This commit is an explicitly requested **work-in-progress checkpoint**, not a release. Stop at committing and pushing this checkpoint. Resume implementation on the destination machine when instructed.

Project title: **Unseen Order**. Repository: **arlequins/deep-premise**. Working branch: **feature/prototype-v1**.

## Latest binding decisions

- Use a real game engine. **No Electron, embedded browser, or webview.** Godot 4.7.2 stable / GDScript / Compatibility renderer was selected for the native 2D prototype.
- Target a standalone Windows x64 client, eventually suitable for Steam. The intended eventual delivery is a downloadable release, preferably an installer plus a portable bundle. Neither is built yet.
- The player does not want hidden world design or mysteries explained. Discuss engineering status without spoiling world mechanisms.
- No separate promotional website and no game-specific AWS hosting. Put any eventual public introduction in **Beat**. No Beat modifications have been made.
- The user authorized game-related implementation, testing, commits and eventual release. Ask before unrelated work or changes/deletions outside that scope. Preserve other projects and their worktrees.
- Do not resume the cancelled AWS provisioning request. The existing AWS task confirmed that no roles, stacks, or existing services were changed.
- Do not create extra tasks or spawn agents without an applicable instruction authorizing that.

## Product constraints to preserve

- One stable world, deep recurring consequences rather than a succession of unrelated settings.
- Limited in-world player authority. NPC intentions, relationship scores and objective event traces stay internal.
- Information arrives through situated reports, letters, witnesses and records; it can be delayed or incomplete. Investigation need not yield complete truth.
- The player learns customs before their reasons and later questions those explanations. Do not turn the game into an encyclopedia or a riddle with a mandatory answer.
- Autonomous simulation should remain interesting unattended for 30–120 minutes. Returning should make the player curious about observable changes.
- Explicit simulation rules determine facts. Generative AI is optional presentation/planning assistance, never an unrestricted author of world facts.
- Local tactical situations should use the same people and resources as the broader simulation, support meaningful player decisions, and resolve autonomously during absence.
- Politics and capable individuals should emerge from resources, relationships, authority and information. Institutions matter as well as heroes.
- Start small; the prototype asks whether players want to investigate what happened after 30–60 minutes away. Do not claim the full multigenerational design is already implemented.
- Keep warmth, ordinary life and successful cooperation alongside conflict.

## Files currently present

| Path | Status |
| --- | --- |
| `project.godot` | Godot project settings and native entry scene |
| `export_presets.cfg` | Draft Windows x64 export preset using locally downloaded templates |
| `game/main.tscn` | Entry scene; **references missing `game/main.gd`** |
| `game/core/world.gd` | Initial port of simulation, bounded memories/reports, observation projection and player commands; unverified |
| `game/core/session.gd` | Tick scheduler, save/backup handling and command entry point; unverified |
| `game/core/automation.gd` | Draft opt-in authenticated loopback connection for automation; unverified |
| `game/ui/city_map.gd` | Native CanvasItem city rendering and place selection; not connected to an application UI |
| `game/assets/` | Native SVG icon, bundled Korean font, engine/font license notices |
| `tools/godot-release.json` | Official Godot 4.7.2 asset URLs and SHA-256 digests |
| `tools/setup_engine.py` | Download/checksum/extraction helper for Windows, macOS and Linux; templates option extracts Windows templates |

The game has no HTML/CSS/JavaScript renderer, Node runtime, npm dependency manifest, website or AWS deployment code.

## Verified versus not verified

Verified on macOS before this handoff:

- Official macOS Godot archive and Windows export-template archive were downloaded; their SHA-256 values matched the pinned manifest.
- Korean font and associated license files are present.
- Repository was initially empty with no remote branch or commits.

**Not verified:** GDScript parsing, scene loading, native UI, native saves, automation protocol, unattended balance, Windows export, installer, Steam integration.

An earlier Electron prototype passed some JavaScript/browser tests. Those tests **do not validate this Godot rewrite** and must not be reported as current native-game coverage. The superseded prototype was moved out of the repository to a macOS temporary folder; it is not a dependency or part of this handoff. Do not restore its architecture.

## Recommended continuation order

1. Read repository instructions and inspect branch/status before editing. Run `py -3 tools/setup_engine.py --templates` to prepare the pinned engine on Windows.
2. Validate the current GDScript files and fix actual parser/type errors. This port was interrupted before the first native validation pass.
3. Implement `game/main.gd` and native Control-based panels: time controls, player-owned resources, standing policy, city/place/resident views, letters with delayed investigation, notes and local tactical controls. Connect `GameSession` and `CityMap` without exposing `world.s` to UI or automation.
4. Test save round trips and corruption recovery, input validation, bounded memory, deterministic replay, hidden-state projection, causal policy effects and long unattended runs. Review numeric/type validation of all externally supplied actions before enabling automation.
5. Review the draft TCP automation implementation for buffering, UTF-8 split handling, response delivery, authentication and disconnects. It is **not yet an MCP server**: a stdio MCP adapter, protocol tests and user enable/disable controls still need implementation.
6. Add native UI and packaged-startup smoke checks. Visually inspect Korean typography and minimum-window layout on Windows. Ensure the final player app does not require Python or development tools.
7. Export Windows `.exe` + `.pck`, build portable ZIP and an installer, test clean install/start/save/restart, and provide checksums and accurate release notes. No installer script or CI/release workflow exists yet.
8. Commit and publish only after those checks. Release authorization was given earlier; do not represent this checkpoint as a release.
9. Once a real release URL exists, add a small spoiler-free introduction/download link to Beat using its current conventions and an isolated checkout. Inspect its own instructions and branch protections first. Do not alter AWS resources for this task.

## Important implementation gaps

- The draft is a small prototype, not a complete simulation of generations, war, ecology, law and culture. Family identity is currently simple; births/succession and a complete long-term history model are not implemented.
- The current main scene cannot run because its script is missing. Do not conceal this with a placeholder that claims playability.
- Save schema is version 2. There is no migration from the superseded Electron schema and no released player save to migrate.
- Save files use the game's custom user-data directory `UnseenOrder`; avoid overwriting user data during tests. Use temporary test directories.
- `.tools`, `.godot`, build products and local screenshots are intentionally ignored. They should not be committed or copied from macOS to Windows.
- The checkpoint has no compiled Windows binary, release tag, GitHub release or AWS deployment.
