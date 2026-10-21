# Painted mountain-pass sample

This independent playable scene tests the natural, complete-illustration art direction. It does not replace the campaign entry, is not a visual acceptance of the entire game, and does not migrate the other levels.

## Run

From the repository root, source `tools/godot/env.sh`, build `godot/Empire.csproj`, import the Godot project, then run:

```sh
"$GODOT_NET" --path godot res://Scenes/PaintedPassSample.tscn
```

Use the existing unit-body selection, movement confirmation, attack and end-turn controls. F5/F9 and the Save/Load buttons share the same callbacks. Saves are isolated at `user://painted-pass-sample/battle.json`; the campaign slot is untouched. Restart begins this sample again, not the main campaign.

## Owners and registration

- `samples/painted-pass-level.json` owns the 16×8 tactical cells, six units, players and objectives. `painted-pass-content.json` registers the blocked rock through the normal composed content catalog. There is no separate navigation mask or demo combat implementation.
- `painted-pass-art.json` owns image registration, not legality. Its exact level-source SHA-256 makes an unreviewed level/layout change fail explicitly. The opaque 1672×941 image is scaled uniformly by 0.42. Camera zoom/pan may crop the viewport; neither the grid nor the camera stretches one image axis.
- `BoardPlane` owns the explicit affine ground basis, consumed by the existing `BoardProjection` for ground, feet, overlays, depth and inverse picking. The default campaign explicitly supplies its existing dimetric basis. The sample supplies its measured painting registration.
- `BoardGroundView` is the consumer-owned ground representation contract. The material-based `TerrainView` and fixed `PaintedGroundView` are real representations selected at composition, not level-ID branches or missing-resource fallbacks. BoardView owns the returned node; ground views own transferred authored children and borrow resource textures.
- `PaintedPassSample` composes the normal BattleEngine, GrayFlagSession, BattleScreen and AI pacer. It does not duplicate their rules or command state machines. Recruitment instructions use the existing tactical query rather than a sample-specific UI condition.

## Deliberate limits

Only the foreground valley is playable. The central rock is blocked; distant village, cliffs and high platforms are scenery. Tree-covered foreground margins are excluded from movement. Foreground trees have not been extracted into an occluding layer, so this sample does not demonstrate walking behind them. Fixed painted terrain rejects terrain/elevation changes rather than displaying a stale surface. There is no arbitrary terrain editor or dynamic artwork regeneration.

The image is a generated edit of the approved chapter-one illustration with baked soldiers, horses and flags removed. Local details were redrawn; it is not a lossless cutout. Native provenance and the asset checksum are in `data/native-scene-art.json`. It is not an exported screenshot or a conceptual image substituted for runtime evidence.

## Verification scope

Actual cloud Godot interaction exercised body selection, a three-cell movement, end-turn AI movement/attack/counter, a player attack, Save/Load buttons and F5→move→F9→F5. Full canonical state equality was checked after reload. Central-rock and outside clicks did not commit movement. The native resource check additionally covers all registered cell-center inverse picks, domain rock blocking, unchanged aspect ratio, rejection of unregistered terrain/elevation and borrowed-texture lifetime. Existing presentation checks cover the unchanged campaign representation.

GUI runs report a cloud X11 `BadShmSeg` startup diagnostic while displaying and accepting interaction correctly. Native headless verification and clean teardown are recorded separately; the X11 diagnostic is not mislabeled as a passing clean GUI log. macOS execution and final user visual acceptance remain pending.
