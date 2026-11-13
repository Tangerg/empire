# C14 forge district presentation

Existing level 14 only. Canonical level SHA256: `2c84925ca44a6e16b4bef255c1099de629850351bdda62e970a08ce08e354a26`. Terrain, roster, AI, triggers, victory alternatives and the 21-turn limit are unchanged.

The registered 1536x1024 hand-painted ground projects the 22x16 grid at origin (250,220), cell-X (40,0), cell-Y (5,30), world scale 1. The 252 forge floor cells, 56 molten cells, 16 bridge cells, 20 walls and eight sites remain canonical. Molten permits flying only; shutting a valve does not change terrain. Independent C01 house and C13 low-wall assets are reused by reference. Two static valve landmarks are pass-through scenery, not interaction or state owners.

Ground and transparent valve art were made with the built-in image generator; the ground used a canonical coordinate guide. Original generated PNG bytes are retained, without cropping, alpha editing or post-generation raster stretching. Read-only cell-center audit found no dry/molten center mismatch across all 352 cells; it is not a claim of pixel-perfect boundary alignment. Source hashes are recorded separately.

## Native acceptance and coverage limits

At 1180x812 and 960x640 the native objective locator exposes all four canonical bridge target cells, outside the HUD and separated from the molten channels. Independent valve landmarks remain visible behind those cells. Native movement previews distinguish foot prohibition from flying access to molten. Camera, HUD, targeting and interaction code are unchanged.

The real production C13 finale-to-C14 transition carries 20 actors (nine friendly), unlike the independent canonical 22-actor fixture (11 friendly). That carry has one C13 history record and does not establish continuous C01-to-C14 completion. The C13 starting run was detached. At turn 4, the carried Laiya was moved to (5,5) and used Gray Oath through actual native UI. Valve A advanced from 0 to 1; the saved battle state and event sequence exactly match legal production replay step 83. Loading the prior state alone is not counted as interaction evidence.

Both valves and a C14 victory were not demonstrated. Bounded legal carry routes lost or reached the existing turn limit, with at most one valve; no healing, resurrection, objective or balance changes were introduced. The existing independent 16-level scripted C14 baseline also reaches the turn limit with one valve. These are follow-up gameplay coverage gaps, not a new successful playthrough. The explicit primary route requires both valves and routing enemies, while the existing last-team-remaining alternative can resolve earlier. Valve zones trigger after an action, not merely crossing, full capture or a separate switch click.

`ForgeDistrictChecks` protects canonical data, registration, movement classes, unique site ownership, static valve semantics, picking, actor visibility and rendering nonmutation. It is included in the aggregate native gate but does not replace visual review or a played victory. Private saves, captures, trials and full logs remain outside the repository.
