# Native presentation boundary

`BoardView` is a read-only projection, not a battle owner. The caller must display
`StatusText` and make `Diagnostics` available. `BattleScreen` supplies interactive
selection and commands through an explicitly injected `ITacticalBattleSession`; the view
never computes legality or mutates the canonical state.

```csharp
var assets = AssetCatalog.Load();
var scene = AuthoredScene.Load("res://data/scenes/c01-01.json");
var environment = EnvironmentCatalog.Load();
board.Configure(new(assets, environment, grid, facesLeft, directionVector), level, scene);
board.ConfigureTerrain(WorldMaterialsManifest.Load());
board.RenderState(canonicalState);
```

`level` is the exported level `JsonObject`. `canonicalState` is a complete canonical
state projection, such as an oracle fixture's `initialState`, not the level's seed
unit list. The view copies its visual values and never mutates or retains that JSON
object. The application supplies the resolved composed `grid`, `facesLeft` and `directionVector` from its tiling strategy.

## Interactive session boundary

`BattleScreen.BindSession(session, assets, level, scene, environment, facesLeft)`
connects the screen to a real application session.
`ConfigureTerrain(WorldMaterialsManifest.Load())` supplies the material metadata.

- `GetSnapshot()` returns a revision and detached immutable `JsonElement`.
- `GetTacticalQueries(binding)` supplies revision-bound canonical facts to the
  `TacticalSelectionController`, the sole owner of local selection and targeting.
- `ProjectHud(...)` returns revision-stamped content rows and intents. Native HUD
  buttons, keyboard input and board cells use the same controller/session path.
- Selecting any reachable destination opens a reversible command preview, including
  the selected actor's current cell and a destination with only one available order.
  Only choosing a command commits; move-and-wait remains one complete domain action.
- `Dispatch(action, expectedRevision)` owns revalidation and execution. The screen
  forwards canonical controller/HUD actions and refreshes after accepted or rejected
  commands. Both the screen and session reject stale revisions.
- `Undo(expectedRevision)` restores through the session; availability comes from
  the same revision-bound tactical queries, not a separate interface property.
- Right-click and Escape follow the controller's back operation. No flat order
  enumeration, direct test-only submission API, local damage calculation or
  UI-only turn transition is used by the application.

## Resource contracts

- `selected-assets.json` owns physical paths and density. `AtlasTexture` regions
  use physical pixels; `Sprite2D` scales by `1 / pixelDensity` exactly once.
- Base topics own canonical identity and footprint. Named actor appearances inherit
  those fields while retaining their own frame size, order and logical anchor.
  Map-structure presentation variants retain their own declared visual footprint.
- Unit bindings optionally include `CANDIDATE_01_ACTOR_ART`, keyed by canonical actor
  key. A declared override must match its expected unit type. Missing named art is
  reported, never substituted with a generic actor.
- Every sheet uses its declared frame order. Idle, walk-a, attack, walk-b are not
  assumed to be sequential animation frames. The preview renders idle only.
- Authored topic placements mirror around their foot. Environment atlas placements
  mirror within their original frame, matching the TypeScript placement contract.
- Units use the original cell contact `(16,31)`; authored placements use
  `(x * 32, (y + 1) * 32)`, and frame contacts are direct tile coordinates.
- Standing art is sorted by contact y, then x, source layer, then stable source
  index/actor ID. Footprints are metadata, not permission to change battle geometry.
- Environment resources use an explicit `resourcePath` when exported; otherwise the
  declared PNG is resolved under `res://assets/environment-builder-v1/`, preferring
  its declared `png2x`. Missing resources are listed with their exact expected path.
  A wrong physical image size is an invariant error, not a missing-art fallback.

## Native terrain

`world-materials.json` exports original material manifests, source resource paths,
terrain bindings, terrain tags, tag-family vocabulary and scenery specifications.
The view reads these dependencies rather than creating a competing content table.

- The field is a textured native `Polygon2D` using the authored irregular outline.
  UV periods are logical material dimensions, not PNG file dimensions.
- Forest coverage reuses the original Blob47 alpha atlas. One native mask texture
  combines the cells, while a shader samples the continuous forest-floor RGB.
- Roads port the source corridor graph, rounded joins, map-edge stubs, broad aprons,
  protected cells and clipping. Only topology alpha is rasterized by Godot's SVG
  loader. The road earth RGB remains the original independently sampled texture.
- Decals, boundary canopies, interior understory and adjacent settlement life use
  the exported specifications and the shared deterministic `TileArtVariation`.
  They never consume battle randomness.
- Authored hill zones suppress repeated procedural rock scenery. Standing native
  terrain sprites share the actor/structure foot-depth ordering.
- Identical tile arrays retain their cached material/scenery composition.
- Negative-depth terrain is below actors but above the screen's backdrop.

## Deliberate remaining scope

The renderer does not claim full visual parity: original lighting pools, projected
shadows, some generic legacy surfaces and automatic elevation are not migrated.
Unavailable resources are diagnosed with their exact paths, never replaced with
colored placeholder terrain. The source scene and environment catalog's
`runtimeReady=false` flags remain visible.

Dynamic battle structures, markers and composites are diagnosed when present. Team
rings preserve owner color without recoloring authored sprite pixels. Animation
timelines, HP gauges, badges and effects remain outside this snapshot renderer.

Creating or importing the `.tscn` with standard Godot is not a C# execution test.
Compile and run with a matching Godot .NET editor and .NET SDK.

After building the C# project and importing assets, execute
`Godot --headless --path godot res://Scenes/PresentationChecks.tscn`.
This native-resource test checks named actor selection, density, pivot, crop/frame
ordering, palette, authored placement coverage, canonical-state immutability, actor
movement projection, and repeated rendering/configuration without duplicate layers.
It also checks Blob47, road center coverage and protected-notch alpha, original
forest coverage, native field geometry, and cached terrain.

`Godot --headless --path godot res://Scenes/BattleScreenChecks.tscn` tests the real
application session end to end: select Torren, select a queried destination,
commit movement plus wait, exercise custom registered reactions through real HUD
buttons, refresh state, reject stale callbacks, and preserve
earlier detached snapshots. These tests are not a full-scene screenshot comparison
or a substitute for the engine's battle-rule differential tests.

`bash tools/godot/battle-screen-checks/mutants.sh` builds an isolated native
scene and proves the explicit session port, live-revision callback guard and open
reaction registry checks reject their respective regressions. A compilation error
or timeout never counts as a killed mutation.

## Native authored scene ownership

`data/scenes/<levelId>.json` is the sole native authored scene for a level.
Callers pass that path explicitly; no first-level alias or default scene exists.
The TypeScript exporter records its original scenes only under
`tests/oracle/source-v1/scenes/` and reads native scenes for asset dependency
selection. It does not overwrite native layout. Levels without authored scenery
keep the existing explicit unauthored-level presentation path. Scene outlines and
decorations do not define playable cells or collision; those remain domain level
data.

## Canonical elevation surface

`BoardView` copies `map.elevation` alongside terrain IDs into its detached read
projection. `TerrainView` invalidates its composition when either changes and
borrows the composed grid through `TerrainViewRules`. `ElevationSurfaceRenderer`
uses that grid's cell outline, center and inverse mapping to locate each height
transition. Shared equal-height edges receive no separate cliff object. Gentle
one-level slope bands and stronger two-level edge bands remain inside the higher
cell's footprint; no neighboring low cell is visually occupied by a face.

This is a fixed tile-space height cue, not a displaced mesh: actor feet, overlay
coordinates and pointer hit positions are unchanged. Connected threshold masks are
softened as a whole, avoiding per-cell seams. Generated alpha textures belong to
the terrain composition through `OwnedProceduralSprite`; no source texture is edited. The focused native
presentation regression changes only elevation and checks redraw, new surface
coverage, unchanged actor contact and unchanged input state. Actual rendered
appearance still needs visual acceptance on the integrated scene.
