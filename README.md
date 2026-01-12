# Phosphers

Phosphers is a Unity-based prototype focused on autonomous agents ("Phosphers") that forage for resources, navigate a fog-of-war space, and return collected bits to a central anchor to score points. The project currently emphasizes the core simulation loop, agent behaviors, and lightweight UI feedback.

## Development Progress

**Implemented**
- ✅ Game state loop (Menu → Run → End) with per-run lifecycle hooks and stats tracking.
- ✅ Phospher spawn management, steering behaviors, and inventory-driven state transitions.
- ✅ Resource system with pooled Bits, food sources, and scoring events.
- ✅ Fog-of-war renderer with reveal masks and per-run reset support.
- ✅ Basic UI panels for run HUD and end-of-run summaries.
- ✅ Trail field painting/decay with phospher-followed signals and trail juice tracking.
- ✅ Phospher lifetime expiry with run end when population reaches zero.

**In Progress**
- 🟡 Signal/field authoring and tuning for emergent steering behaviors.
- 🟡 Visual feedback polish (VFX/SFX hooks, UI clarity).

**Planned**
- ⬜ Long-term progression systems and save data.
- ⬜ Performance profiling pass on flocking, perception, and fog updates.
- ⬜ Build pipeline and platform targets.

## Project Structure

- `Assets/Scripts/Core` — run lifecycle, global systems, and state management.
- `Assets/Scripts/Phosphers` — agent logic, perception, resources, fog, UI.
- `Assets/Scenes` — main playable scenes (`Main.unity`, `SampleScene.unity`).
- `Assets/Scriptable Objects` — tunable configuration assets (agent settings, bit types).

## Key Systems

- **Game State**: `GameManager` controls the main lifecycle and spawns the run anchor.
- **Agents**: `PhospherManager` spawns agents, `Phospher` handles steering, seeking, returning, and recovering.
- **Resources**: `ResourceSystem` pools and spawns Bits, tracks score, and fires deposit events.
- **Fog of War**: `FogSystem` maintains a mask texture and updates visibility with revealers.
- **UI**: `RunHUD` and `RunSummaryPanel` surface per-run stats.

## Getting Started

1. Open the project in Unity **6000.0.62f1**.
2. Load `Assets/Scenes/Main.unity`.
3. Press **Play** and use dev hotkeys (Space/E/R) to step through states.

## Notes

This README is meant to track current development progress and provide a quick map of the project. Update the Development Progress section as features move from planned to implemented.
