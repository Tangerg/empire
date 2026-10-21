# Native catalog ownership

`ReadOnlyContent` is the canonical catalog supplied at engine composition. It owns one
`StateContent`, which normalizes the authored packs once and owns their definition,
terrain-character and default-terrain indexes. Each rules subsystem consumes that
same collaborator instead of constructing another table from copied packs. Separate
engines may share a catalog; their battle states and registries remain independent.

## Definition boundary

The normalized JSON nodes are private to `StateContent`. They never leave by reference,
including through assembly-internal queries. Its immutable `JsonElement` read values
are created with `EcmaJson.ToElement`, whose cloned document lifetime is independent of
temporary `JsonDocument` disposal. They are read projections, not additional writers.
Its JSON-object reads and pack exports deep-copy every nested object and collection.

Public definitions use two deliberate shapes:

- `ReadOnlyContent.Definition` / `Definitions` and `TacticalCell.Terrain` / `Overlays`
  provide immutable `JsonElement` values. Catalog definition collections are read-only.
- Weapon queries, reactive strikes, ability and combat-modifier callbacks, formation
  queries/options, and planned hit effects provide detached JSON projections. Callers
  may edit their own result, but cannot change a future query or either engine's catalog.

Combat modifiers and hit-effect handlers never receive a mutable catalog node. This
is enforced at the definition owner rather than by remembering to clone individual
outward getters. A new consumer of `StateContent` therefore cannot accidentally return
a live definition.

This guarantee does not turn battle state into immutable data. Units, overlay instances,
map ownership and other live state remain plain serializable JSON under their domain
transition owners. A caller-created or edited combat plan is still that caller's
proposal; isolating it from catalog definitions does not authorize it as a legal command.

`EcmaJson` remains the text boundary. For exact UTF-16 round trips, materialize immutable
values with `EcmaJson.Parse(element.GetRawText())`, not a serializer that replaces lone
surrogates. No production content, numeric semantics or text encoding changed here.

## Intentional API changes

Content-reading subsystem constructors now require `ReadOnlyContent`, not `JsonArray`:
`MovementRules`, `DefaultTacticalSpace`, `DeploymentSystem`, `SupportOrders`,
`RecruitmentOrders`, `BattleProgression`, `CareerRules`, `FormationRules`,
`BattleTransport`, `BattleOverlays`, and `CampaignBattleBridge`. Pass the already-composed
catalog, rather than wrapping packs separately for each subsystem. Removed raw-pack
constructors have no compatibility overloads.

`RulesetManifest.Create` also takes the canonical catalog. `BattleDefinitionValidator`
and `BattleContentValidator` obtain it from their declared `RuleReferenceRules` port;
there is no independent packs argument that could disagree with the content they admit.

`TacticalCell.Terrain` is now `JsonElement`; `Overlays` is
`IReadOnlyList<JsonElement>`. Use `GetProperty`, `EnumerateArray`, and immutable reads.
The overlay instances (`OverlayStates`) and occupied units/structures remain battle
state, not content definitions.

## Verification

[`catalog-boundary-checks`](../../tools/godot/catalog-boundary-checks/README.md) is a
separate executable with an actual `ProjectReference` to `Empire.Core`. It exercises
public mutation paths, nested data, shared engines, UTF-16, save bytes, event streams,
state hashes, and replay/load. Its negative control deliberately returns a live node
from the catalog and must compile successfully before the public behavioral guard
rejects the defect. Frozen combat/AI oracles and the shipped replay gate continue to
check unchanged default gameplay; the boundary suite is not a substitute for them.
