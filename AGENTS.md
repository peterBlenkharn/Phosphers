# Phosphers Agent Log

This file tracks project intent, current status, and the latest requests from the project owner. Update this file as work progresses.

## Project Intent
- Build an agent-driven simulation where Phosphers forage for resources, navigate fog-of-war, and return to an anchor to score.
- Emphasize emergent behavior, visual feedback, and tunable simulation parameters.

## Current State (Last Reviewed)
- Core game loop (Menu → Run → End) in place with run stats tracking.
- Phospher agents spawn around the anchor, forage/seek/return, and deposit bits.
- Resource system handles pooled bit spawning and scoring.
- Fog-of-war system uses a mask texture with per-run reset.
- Basic UI panels show run stats and end-of-run summaries.
- Trail field painting/decay is in place, with phospher-driven trail signals and a trail juice HUD.
- Phospher lifetime expiry triggers a death event and ends the run when population reaches zero.
- Trail painter now explicitly references the Phospher agent namespace to prevent missing type errors.

## Owner Requests / Expectations
- Provide comprehensive reviews and optimization/tidy-up recommendations.
- Keep README updated with development progress.
- Keep this AGENTS.md updated with goals, status, and next priorities.

## Suggested Next Priorities
- Profile flocking/perception and fog updates to identify hotspots.
- Tune signal fields and steering weights for clearer emergent behaviors.
- Expand UI/VFX feedback for deposits, depletion, and run transitions.
- Decide on trail rendering visuals (material, palette, blending) and tune decay/strength.
- Audit signal-related scripts for missing namespace/import references after refactors.

## Open Questions
- What is the target platform and performance budget?
- Is the current gameplay loop intended to include progression/meta systems?
- Which visuals/audio assets are considered final vs placeholder?
