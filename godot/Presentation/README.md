# Native presentation boundary

`BoardView` is a read-only migration preview, not a playable battle. The caller must
display `StatusText` and make `Diagnostics` available. There are deliberately no
click-to-move, attack, turn, legality, or save controls in this node.

```csharp
var assets = AssetCatalog.Load();
var scene = AuthoredScene.Load();
var environment = EnvironmentCatalog.Load();
board.Configure(assets, level, scene, environment, facesLeft);
board.RenderState(canonicalState);
```

`level` is the exported level `JsonObject`. `canonicalState` is a complete canonical
state projection, such as an oracle fixture's `initialState`, not the level's seed
unit list. The view copies its visual values and never mutates or retains that JSON
object. The application supplies `facesLeft` from its tiling strategy.

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

## Deliberate remaining scope

The native renderer handles actor sheets, map structure state frames, authored
foundations/decals/standing props, and authored perimeter props. It does not claim to
reproduce the continuous world-material ground, forest coverage/edges, road clipping,
automatic elevation, procedural scenery, or the full original frame compositor.
These layers are not replaced with tiled or colored placeholder terrain. The source
scene and environment catalog's `runtimeReady=false` flags remain visible.

Dynamic battle structures, markers and composites are diagnosed when present. Team
rings preserve owner color without recoloring authored sprite pixels. Animation
timelines, HP gauges, badges and effects remain outside this snapshot renderer.

Creating or importing the `.tscn` with standard Godot is not a C# execution test.
Compile and run with a matching Godot .NET editor and .NET SDK before claiming native
runtime verification.

After building the C# project and importing assets, execute
`Godot --headless --path godot res://Scenes/PresentationChecks.tscn`.
This native-resource test checks named actor selection, density, pivot, crop/frame
ordering, palette, authored placement coverage, canonical-state immutability, actor
movement projection, and repeated rendering/configuration without duplicate layers.
It is not a full-scene screenshot comparison or battle-rule parity test.
