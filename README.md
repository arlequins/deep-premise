# Unseen Order

A conversation-first simulation prototype. Six neighbors, four nearby places, and incomplete accounts of an ordinary morning.

**Status:** local Windows prototype 0.2.0. This is a small playable experiment, not a finished Steam release.

## Play

Extract `dist/Unseen-Order-0.2.0-Windows-x64.zip` and run `Unseen-Order.exe`. Keep the adjacent `.pck` and `data_*` directory with the executable. The portable package includes its .NET runtime; players do not install Godot, Python, or a development SDK.

- Choose a place on the map or with a location button.
- Choose someone within earshot, then a conversation option.
- Compare accounts, pass on something you heard, share food, or make a small request.
- Time advances while the window is open. A conversation or a walk also takes time.
- Space pauses the world. The notebook pauses time while open.
- Use the notebook for observations and personal notes. Saves are automatic.
- Start another neighborhood from the notebook to explore a fresh seed; the previous save is archived first.

New saves use a random seed. The same seed and inputs are reproducible for development. Not every moment is surprising; the aim is for understandable habits to interact in unexpected ways.

Saves: `%APPDATA%/UnseenOrder/conversation-v3.json`, with a backup. The earlier GDScript `world-v2.json` file is neither loaded nor overwritten. Corrupt saves are preserved during backup recovery.

## Architecture

- `src/DeepPremise.Core`: pure C# / .NET 8. World state, agents, relationships, needs, situated knowledge, deterministic ticks, dialogue intent, actions, persistence, and detached view models. No Godot dependency.
- `src/DeepPremise.Godot`: native Godot 4.7.2 .NET presentation, input, map, conversations, notebook, and optional AI settings.
- `src/DeepPremise.Dialogue`: optional OpenAI Responses API voice adapter. It receives a limited character context and cannot execute world actions.
- `tests/DeepPremise.Tests`: dependency-light executable tests, including headless simulation and mocked AI transport.

One tick is 15 in-world minutes. The viewer normally advances one tick every eight seconds. No catch-up simulation is performed for time spent with the app closed. The core is small by design: there is no war, full economy, multigenerational society, mobile UI, Steam integration, or BGM in this slice.

## Optional AI dialogue

Open **Notebook / AI → AI voice**. Enter an OpenAI API key and a model ID available to your API account, then enable it for this session. The key is kept only in memory and is not included in saves or source control. The current Codex conversation is not an embedded game AI service.

Enabling sends your question, recent dialogue, and the selected resident's known accounts to OpenAI. API usage may incur charges. This prototype allows at most 20 requests per process session, with a 12-second timeout and automatic local fallback. No background simulation uses AI.

The adapter follows the official [Responses API structured output guide](https://developers.openai.com/api/docs/guides/structured-outputs), requests `store: false`, and uses a constrained character prompt. This limits the model's role but does not guarantee perfect factual phrasing. Generated dialogue is presentation only; the model cannot alter resources, facts, or memories. Responses are saved in the local conversation transcript. Generated wording is not deterministic; simulation state remains deterministic.

Without AI, free-text input uses simple English topic matching. Buttons provide all local interactions. The AI integration is tested with mocked HTTP responses; a live paid API round trip has not been verified in this workspace.

## Development on Windows

Install a .NET 8 SDK and Python 3, or use local runtimes already placed in `.tools`. Prepare the checksum-pinned Godot .NET engine and Windows templates:

```powershell
python tools/setup_engine.py --templates
```

Run tests without Godot:

```powershell
dotnet run --project tests/DeepPremise.Tests/DeepPremise.Tests.csproj
```

Build the native viewer and portable release:

```powershell
./tools/build.ps1
```

`build.ps1` prefers `.tools/dotnet/dotnet.exe` if it exists, then a system SDK. It imports the Godot project, exports Windows, checks export logs for errors, and creates a portable ZIP with SHA-256 checksums.

For isolated native UI smoke tests:

```powershell
$engine = (Get-Content .tools/engine-path.txt -Raw).Trim()
& $engine --path . -- --smoke --save-dir=C:/temp/unseen-order-ui-test
```

Do not use an actual player save folder for automated tests. Native smoke mode saves screenshots under `artifacts` and exits. This developer-only mode is not part of the normal player flow.

## Verification and limits

The headless suite covers deterministic continuation, detached observations, multiple seed histories, conversation reachability, sharing, player consequences, malformed saves, backup recovery, 100,000 ticks, and optional AI success/failure contracts. Native smoke checks exercise the actual C# viewer and two window sizes. See `docs/HANDOFF.md` for current implementation notes and remaining work.

This repository, source comments, documentation, and game text use English. Do not explain the hidden world mechanisms to the player in status reports or marketing copy.

## Notices

Godot and bundled font license notices are in `game/assets` and included in the portable package. Game code and original content have no separate redistribution license granted. No website or AWS resources are part of this project.
