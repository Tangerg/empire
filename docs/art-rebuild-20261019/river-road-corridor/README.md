# River crossing road corridor

c01-02 now shares the reviewed continuous earth material/corridor renderer with
c01-01. Its 33 road/settlement cells and 21 cardinal graph edges are unchanged.
The 32px width, 22px tangent cut and 2px permitted plain shoulder are unchanged;
bridge cells connect to thresholds but are never painted as earth. Editor geometry
retains its existing cell connectors. No other map is opted in.

## Full scene proof

`before/full-board@1x.png` and `after/full-board@1x.png` show the entire actual
BoardView, including the outer frame, all 20 actors and all 9 terrain buildings.
Both also have 2x rasters and source SVGs. These are offline Inkscape renders of
production SVG with app styles, not live browser/GPU or gameplay validation.
No source image was edited. No crop conceals the remaining scene problems.

The material no longer repeats per road cell. This does not make the level fully
accepted: old shoreline strips still expose rectangular river boundaries, the
outer forest frame is dense and repetitive, and existing tree/actor/building
occlusion remains visible. Those are separate unfinished layout work.

## Verification

- 12 focused road tests passed, including c01-02 counts, untouched bridge paint,
  exact scenery preservation and deterministic repeated rendering.
- Typecheck passed after correcting local worktree dependency links.
- All 16 candidate scene projections compared in the same checkout: only c01-02
  ground markup differs. Every level/state, standing/foreground layer and editor
  ground hash is unchanged. This preserves authoritative gameplay reachability.
- Actual BoardView repeated render and empty-overlay roundtrip are exact; a
  selection/cursor/path overlay does not change actor markup or state.
- No full test/replay claim is made here; integration coordinator owns aggregate
  verification on the final main commit.

Reproduce the after capture with `RIVER_ROAD_OUTPUT=<directory> npx vite-node
 tools/art/review_river_road.ts`, then use `render_ground_review.rasterize` at 1x/2x.
The before capture was made in this worktree with only c01-02's new roadCorridor
field absent, then the field was restored before final testing.
