# Live battlefield presentation

`Presentation/DynamicStateView.cs` is a read-only adapter, configured with the selected asset catalog and explicit content packs. `BoardView` replaces its dynamic nodes from each canonical snapshot. It never advances HP, ownership, capture, markers, composite status or any other rule state.

## Supported projection

- All nine structures in the shipped GrayFlag levels: Redstone's command node (`c01-03`), the four chain controls (`c01-09`), three bell parts (`c01-12`), and the 500-HP mother-root (`c01-15`).
- Native structure bindings explicitly name `normal`, `captured`, `damaged`, and `destroyed` topic/frame pairs. HP zero selects `destroyed`; positive HP at or below half, or disabled, selects `damaged`; otherwise an owner selects `captured`, and neutral selects `normal`. The five existing three-frame sheets deliberately bind their destroyed role to their existing damaged frame. A new obstacle can instead bind a separate rubble asset. Missing roles or frames are rejected at catalog construction, not resolved by a fallback.
- Source rule-derived structure drawing when the source campaign has no art binding, notably mother-root. Shape reflects movement obstruction/cover, damage, disabled state and ownership. Missing bound resources remain diagnosed rather than silently substituted with unrelated art.
- Actual source marker topics: aftermath-2 for a kind containing `corpse` or a fallen-unit snapshot, aftermath-3 for a kind containing `transport`, and evidence-1 otherwise. The source rule-derived marker drawing remains an explicit missing-resource fallback.
- Canonical position, HP and owner changes, disappearance when a state object is removed, and the explicit destroyed visual role when a destroyed structure remains in state with zero HP. Source destruction does not necessarily remove the structure record.
- Terrain-bound keeps and villages share the source owner banner with live structures. Changing from one player to another changes the banner's actual pixels; neutral ownership removes it.
- Unit HP gauge, acted shading and capture progress. Maximum HP comes from the injected unit definition; capture comes from `map.captureProgress`, not the unit's capture field.
- Optional directional pip from an injected grid vector transformed by the shared board projection. Direction names are not interpreted in the renderer. Existing left/right art mirroring remains compatible.
- An optional explicit visible-unit-ID set filters actor rendering without changing canonical state, canonical unit counts, or treating hidden actors as missing assets.

Composite entities are not an extra authored body: the original board renders their structure parts. Aggregate progress must come from the canonical `CompositeRules.Status` query. Static decoration stays independent and is not hidden or reassigned merely because it shares a coordinate or topic with a dynamic object. None of the seven restored authored scenes contains a duplicated live-structure topic placement.

## Native interaction and authoring

Visible live-structure pixels resolve to the canonical structure cell through the same depth-aware picker as actors and terrain. They do not select an occluded unit behind the structure. The tactical attack forecast consumes `PrimaryStructure` damage and remaining HP directly from the composed engine; the HUD does not repeat combat calculations.

`data/art-bindings.json` is native authoring. The historical TypeScript exporter writes its old binding shape only below `tests/oracle/source-v1/`, so regenerating an oracle cannot erase native lifecycle art.

## Historical source provenance

- `packages/game-ui/src/ui/board.ts`: `syncBattlefieldObjects`, `renderUnits`, `facingBadge`.
- `packages/game-ui/src/art/field-objects-from-rules.ts`: total structure/marker fallback.
- `packages/game-ui/src/art/gauges.ts`: shared gauge geometry and thresholds.
- `packages/story-candidate-01/src/presentation/candidate-01-runtime.ts`: structure frames, owner banners and marker-topic selection.
- `packages/story-candidate-01/src/presentation/candidate-01-actor-art.ts`: explicit actor-key identity. Native `AssetCatalog.ResolveUnit` already preserves this identity.

## Scope boundaries

This restores gameplay visibility, not complete renderer/animation parity. Dynamic movement and ownership were already refreshed by the native snapshot renderer. Event-driven movement/attack/departure animation and full lighting/shadows remain separate work. Fog and incoming casts are query-supplied overlays, not a renderer's inference from raw JSON. The source board does not directly paint scenario overlays; no shipped GrayFlag level currently declares one. Their art must not be fabricated as purported source parity.

The nine levels without restored authored scene documents still have no invented authored scenery. Rule-derived terrain and live objectives do not turn those missing source layouts into completed authored layouts.

## Verification

After a successful fresh project build, run:

```sh
source tools/godot/env.sh
dotnet build godot/Empire.csproj --no-restore --nologo &&
  "$GODOT_NET" --headless --path godot res://Scenes/DynamicStateChecks.tscn
```

The native scene passed **100 assertions** against the current Core/native assemblies. It covers all sixteen canonical campaign openings, all nine structures, actual exported marker sheets, nontransparent fallback pixels, HP/disabled/owner/location changes, retained destroyed records, removals, repeated-render ownership, authored-decoration preservation, composite parts without a duplicate body, badges, site ownership pixels and explicit actor visibility. Rendering checks do not substitute for gameplay rule differential tests.
