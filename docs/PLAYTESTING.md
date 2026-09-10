# Local playtest recording

Detailed local recording is enabled for this first-player prototype with the player's explicit consent. It does not upload data. API credentials are kept in memory and never passed to the recorder. Questions, notebook content, feedback, dialogue, and hidden simulation state are recorded intentionally for development.

## Find a session

- Debug launcher: `artifacts/playtests/<UTC timestamp>-<id>/`.
- Portable game: `<Godot user data directory>/playtests/<session>/`.
- `artifacts/live/context.json` identifies the active debug session, process, sequence, and save path. Check the timestamp and process liveness before treating it as current.
- Synthetic smoke runs use isolated save directories. Do not treat their markers as human feedback.

Every session contains a manifest and initial state, an append-only `events.jsonl`, and occasional full checkpoints. Files are flushed as records are written. A crash may leave an incomplete final line; recovery tolerates that final fragment while rejecting missing sequences or incorrect state hashes. No session is automatically deleted.

## What is recorded

- Actual player inputs, available alternatives, failed attempts, selected resident, language, travel, waits, pauses, notebook use, and AI enable/disable actions.
- Visible authored/localized dialogue and choices, grounded AI request context, generated wording or fallback outcome. No API key, raw provider error, or unrelated desktop content is recorded.
- Seeded random draws, daily visit candidates, movement, provisioning, world events and their witnesses, knowledge exchange, memory reasons, request transitions, and later personal-conversation outcomes.
- A structural state delta after every simulation tick and saved action, with a SHA-256 hash. Notes and per-language AI wording are included in state changes.
- F8 feedback markers: expectation text, category, public world view, wall-clock time, simulation tick, and event sequence.

Logs intentionally contain hidden mechanics. Do not show a raw trace or review report inside the player-facing game. A visible warning appears if recording becomes unavailable; ordinary game saving remains separate.

## Review and reproduce

From the repository:

```powershell
.\tools\review-playtest.ps1
```

This verifies the recorded states and writes `review.md` beside the session. The report indexes actions, repeated authored replies, failures, AI use, story state, decision categories, and explicit player markers. Counts are evidence for review, not a score for surprise.

Restore an exact sequence into a **new** destination:

```powershell
.\tools\review-playtest.ps1 -Session 'C:\path\to\session' -Sequence 1200 -Output 'C:\path\to\replay\conversation-v3.json'
```

The tool refuses to overwrite an existing output. For a native replay, launch the Godot project with `-- --save-dir=C:/path/to/replay --language=ko`. Keep the original player's save separate. The restored state includes simulation RNG and can continue deterministically under the same subsequent inputs. Replaying a saved AI wording does not require an API call.

## Development review order

1. Read the player's expectation and category first. A surprising moment and a confusing moment are not interchangeable.
2. Check the nearby `action`, `view`, and `conversation-result` records to establish what was actually offered and shown.
3. Restore the marked state and inspect preceding `decision` records. Locate the cause rather than inventing an explanation after the fact.
4. Compare neighboring seeds or a changed choice. Keep stable rules and change only the input being tested.
5. Fix lost context, misleading choices, or repeated replies before adding more systems. Extend a pattern when the player can discover its cause through ordinary conversations.

The recorder does not measure private emotions or guarantee that a technically varied world feels surprising. F8 markers and the player's own comments remain the primary evidence.

The debug launcher starts paused so the opening scene remains intact until the player is ready. Press Space or Resume to let time advance. Explicit conversations and travel can still advance time while automatic time is paused.
