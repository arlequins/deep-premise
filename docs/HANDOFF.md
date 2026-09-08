# Development handoff — conversation slice

## Current direction

The user changed the technical direction to **Godot 4 .NET + C# + independent simulation core**. The user wants a small first playable slice focused on dialogue and the gradual experience that familiar assumptions may not hold. No BGM is needed. Do not restore broad management or tactical features merely because they existed in an earlier checkpoint.

The central quality target is **surprise arising from interacting patterns**. The world must not be entirely AI-generated. Stable habits, needs, relationships, incomplete knowledge, and player actions should combine into outcomes that are understandable afterwards but not always predictable in advance. AI is optional dialogue presentation, not an omniscient world author.

Use English for code identifiers, comments, and development documentation. The game supports English and Korean; Korean text belongs in localization resources. Chat with the user in Korean. Keep hidden world mechanics out of user-facing development updates. Inform the user of significant modifications/deletions; ordinary in-scope work is authorized. No extra tasks or agents unless explicitly requested.

## Preserved work

Commit `3e86635` preserves the prior native GDScript prototype and tests. Active GDScript simulation/UI files were removed during the C# transition. Its `world-v2.json` save format is not migrated or overwritten. New saves use `conversation-v3.json`.

## Implemented

- Pure .NET 8 core with six agents, four places, seeded deterministic PRNG, 15-minute ticks, routines, needs, relationship-sensitive visits, encounters, sharing, local knowledge exchange, and forgetting.
- Persistent authoritative world events separated from the public view and subjective dialogue accounts.
- Conversation choices based on presence and known accounts; a few small actions with persistent consequences.
- Contextual dialogue and repeated-question handling. English topic matching is the offline free-text fallback, not an LLM.
- Godot C# native map, local resident selector, conversation history, choices, free-text entry, pause/wait, observations, personal notebook, and AI settings.
- Detached character context sent to an optional OpenAI Responses API adapter. No tool calls or state-changing capabilities. Only the selected NPC's reply is rewritten.
- API keys remain in memory. AI is off by default, with 20 requests/process session, rate spacing, timeout, bounded response reading, and local fallback.
- Atomic disk replacement with backup and preservation of corrupt primary saves.
- A headless executable suite with 45 assertions, including 100,000 ticks and mocked AI transport. No Godot runtime is needed to run it.
- Windows export and portable packaging scripts. Use the .NET engine and .NET templates, not standard Godot executables.

## Important limits

- This is a conversation prototype, not the original full society design. Characters and dialogue topics are authored; dynamic state selects and combines outcomes. There is a finite content vocabulary.
- The knowledge mechanics are deliberately not explained here; inspect the core when changing them, and keep implementation explanations out of player-facing text.
- Core identity and roles are simple. No full faction formation, generations, law, ecology, tactical battle, or commercial Steam integration is implemented.
- Generated prose can contradict constraints even with a careful prompt. The adapter's lack of mutation access protects simulation state, not semantic accuracy of every line. Further dialogue evaluation is needed before commercial use.
- Live paid AI requests have not been validated without a user-provided API key. Mock success, rate error, malformed output, and world-state isolation are tested.
- Mobile support is architectural intent only. Neither mobile UI nor exports are verified.
- Exported Windows files are unsigned. No installer or public release is claimed. Prefer the portable ZIP for local play.
- No catch-up ticks on app restart. While open, pause and notebook windows stop automatic ticking; conversations still advance one tick.

## Next useful work

Observe actual play before expanding the feature list. Evaluate whether users compare accounts, revisit people, and notice changed answers. Measure repeated content, grounded AI replies, and causal variety across many seeds. Add a small number of reusable event patterns that genuinely change conversation context rather than more explanatory lore.

Treat the core as the reusable asset. Keep Godot types out of it. Keep the optional network adapter outside it. Use explicit domain actions for consequences; free text must not silently turn into resource transfers.

No website, Beat, AWS, Steam account, or unrelated repository was modified during this migration.

## Verification from this Windows handoff

- Core and optional voice adapter: 45/45 executable checks passed.
- The 100,000-tick test completed in about 0.6 seconds on this development machine; this is a six-agent core microbenchmark, not a large-world performance claim.
- Native C# viewer smoke passed at 1440x900 and 1160x840. Both screenshots were visually inspected.
- Portable Windows startup and restart both exited successfully with identical saved state during the short smoke run.
- The packaged app was launched with global .NET lookup disabled and an intentionally absent DOTNET_ROOT; its bundled runtime loaded successfully.
- Export log contains no ERROR entries. SHA-256 sums are generated under dist.
- The ZIP is a local artifact, not a published Steam or GitHub release. No live paid AI request was made.

## Bilingual live debugging update

The user requested English/Korean support, a local running debug game for feedback, and Luna-only AI dialogue. The header has a persistent language picker. Core facts remain canonical English. Free-text matching understands both languages. Typed text and notes remain verbatim. New AI replies preserve per-language variants and their authored fallback. Older v3 saves load without migration.

The adapter pins `gpt-5.6-luna`, disables reasoning, and never upgrades models. An API key entered by the player is still required. Codex subscription credentials are not reused. No paid live API call was made during this update.

For feedback, read `artifacts/live/context.json`, inspect `screen.png`, and check `game.log`. Verify timestamps and process liveness first. Screenshots are skipped while the notebook/settings window is open. `tools/run-debug.ps1` launches the session without duplicating an active debug process. `Play-Unseen-Order.cmd` is a convenient launcher. Preserve active player saves when restarting. Do not terminate unrelated processes.
