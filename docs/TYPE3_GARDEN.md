# Type3 — A World, Gathered

A small living terrarium with a deliberately quiet interface. The garden occupies most of the window; supply is paged three items at a time. Selecting an object reveals one sentence and a contextual storage action. Selecting the hollow reveals one expedition action. Discoveries temporarily replace the sentence, rather than opening panels.

## Play

Run `./tools/run-type3.ps1`, or the default `./tools/run-debug.ps1`. English is available with `-Language en` or the in-game toggle. Drag an object to move it; click a supply then click a spot to place another. Space pauses; Escape clears selection; F8 records feedback.

Stones warm in darkness. Snails approach warmth and retain a glowing shell after sustained exposure. Grass follows nearby light from snails or flowers. A glowing snail near the hollow finds two seeds. Seeds grow with nearby grass plus warmth, water, or moonlight. Those environments yield golden, blue, or moonlit flowers. Moths visiting differently colored flowers produce pearly flowers. Flowers compete with snails for the grass's attention.

## Ecology expansion (type3-0.2)

- The first bloom opens shore exploration, bringing a pond and dew beetle. A beetle carries water from ponds to seeds and flowers.
- Water delivery opens meadow exploration, bringing a moth and pillow moss. Moths work in daylight, carrying pollen between flowers. Pollinated flowers replenish the seed supply, with per-flower cooldowns.
- Blue flowers and pollination open the moonwoods, bringing moonstone. Further trips cycle through additional supplies and seeds. These expeditions have authored gates and rewards, not procedural outside maps.
- Two adult snails near moss beside a pond can nest at night. Eggs grow on moss, hatch, and mature. Children inherit light and shell color; differing parental colors yield a pearly shell. Wet and moonlit habitats also affect adult shell colors.
- Three live flower colors, a garden-born snail, and moonstone produce a quiet nighttime visual milestone. The garden remains playable afterward.
- Packing and unpacking preserve object identity, age, ancestry, color, and learned traits. Active explorers cannot be packed or moved.
- Boundaries: 36 active objects, breeding stops at 12 snails/eggs across active and stored objects, 24 units of each supply, nesting and pollination cooldowns. Explorers return exactly once across save/reload.

There is no asserted playtime estimate. The extension adds new decisions and renewable interactions instead of lengthening the initial warmth/bloom timers. Player enjoyment and time-to-discovery remain to be measured.

Rules run in a fixed-step pure C# core. Each step reads neighbors from a common snapshot. Rendering owns animation only. Objects never contain Godot types. Earlier prototypes remain in their own scenes and save files.

## Local diagnostics

- Save: `%APPDATA%/UnseenOrder/type3-garden-v11.json`, atomically replaced. When absent, the previous v10 garden is loaded and migrated; the original v10 file remains untouched. Fresh starts archive the current save first.
- Debug context and game-only screenshot: `artifacts/live/type3-context.json`, `type3-screen.png`.
- Detailed playtest sessions use recording kind `garden-v11`, including environmental behavior changes, interventions, exact state deltas, and feedback markers.
- Versions 10 and 11 are supported by the existing replay and review tools.
- `--smoke-garden` uses a separate save and checks native light-button, drag, and placement input before capturing the localized screen.
- `--smoke-garden-extended` uses a clearly isolated visual fixture and native input to verify the expedition button, inventory paging, and placement in both languages.
- Core tests cover warmth, distance, following, finite discovery rewards, growth, save continuation, invalid input, detached observations, and exact replay. Expansion checks play through all three expedition gates, watering, pollination, renewable seeds, flower colors, nesting, inheritance, storage, migration, and bounded long-running ecology.

This is an interaction prototype, not a claim of validated fun or a completed ecosystem game.
