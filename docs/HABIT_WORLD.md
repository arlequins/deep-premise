# The things we repeat — 0.7.0

The active scene is HabitMain. The player requested a larger conceptual change and asked to experience it in the game. The previous defense experiments and their saves remain preserved.

Five settlers autonomously gather food, water and fiber, weave cloth, deliver cargo and rest at a shared hearth. Drag a person onto another place to reassign them. Orders finish the physical return journey before beginning another route. Resources support daily meals, rain shelter and new arrivals. Sustain seven people through day six; simulation continues after the objective.

Repeated cooperation changes a site's handling requirement and yield. Repeated craft makes a loom recognize its maker. Reassigning a frequent traveler can leave their former journey running briefly without them, consuming actual local reserves. Residents separately learn what they witness. Empty-handed rehearsal can relax material expectations; solo carrying can avoid forming a cooperative expectation. These are deterministic local processes, not random popups or unlock cards.

The interface shows people, carried objects, paths, worker thoughts and observed consequences. New arrivals begin without knowledge of the settlement's habits. Raw repetition counters are not displayed. The model remains a pure C# simulation and can run without Godot.

Saves use `habits-v8.json`, separate from earlier experiments. Detailed local playtest records, exact replay, F8 markers and live viewport/context files remain enabled. Native smoke testing uses an isolated save and real mouse input to move a worker after habits have formed. English and Korean are supported. No live AI API or background music is used.

This initial world includes four work sites, three interacting repetition effects and growth up to nine people. It does not yet implement a complete social or civilization simulation.

Validation: 139 checks passed across the project, including 13 habit-world checks. Tests cover independent task methods, learned restrictions, changing a habit, worker reassignment, actual new production after restoring a familiar maker, exact replay and a balanced seven-person settlement reaching its objective. Native English and Korean drag-input smoke runs passed; both 1440 × 900 screenshots were inspected.
