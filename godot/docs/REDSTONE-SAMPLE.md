# Redstone painted playable sample

This independent 101×52 battlefield combines the original 1642×958 painting,
authored foreground polygons, eight interactive sites and the normal battle UI.
It is a visual/playable sample, not the campaign's `c01-03` replacement or a
claim of completed gameplay, balance or performance acceptance.

## Open and run

Use Godot **4.6.3 .NET** and **.NET SDK 8**. Open `godot/project.godot`, build
the C# project, open `Scenes/PaintedRedstoneSample.tscn`, and run the current
scene (F6). Running the project (F5 in the editor) still opens the campaign.

From the repository root with the configured local toolchain:

```sh
source tools/godot/env.sh
dotnet build godot/Empire.csproj
"$GODOT_NET" --headless --editor --path godot --import
"$GODOT_NET" --path godot res://Scenes/PaintedRedstoneSample.tscn
```

Select a friendly unit, choose its action and destination, then confirm the
order. Use the existing attack, site interaction and end-turn controls.
Click a site to keep its name, owner and use instructions open while
moving the pointer into the HUD. An actionable friendly unit on a site keeps its
normal unit-selection priority. Click another site or friendly unit to switch;
right-click, Esc or the panel's close button clears the selection. The initial
P1 outpost at (38,23) heals owned units by up to 15 HP at the start of their side's next turn;
it does not recruit. Capturing another site requires the existing capture command,
not merely moving onto it. None of these inspection controls changes site rules.

Drag the battlefield or use the mouse wheel to pan; hold Ctrl/Cmd while
scrolling, or use the top −/+ buttons, to zoom.
In the running game, F5 saves and F9 loads; the header buttons use the same
callbacks. Saves are isolated at `user://redstone-prototype/battle.json` and
never overwrite the campaign slot. Restart starts this sample again.

## Data and ownership

`data/samples/redstone-prototype/scene.json` is the single sample definition.
`level.json` owns tactical terrain, sites, units, objectives and scenario
transitions. It preserves this sample's authored turn-4 reinforcements,
turn-15 sapper event and turn-18 limit. It does not modify the campaign data.

`art.json` registers the original `painting.png` and same-image foreground
polygons. These polygons own visual occlusion and selection geometry, not
movement legality or a separate combat state. Its level checksum uses the
engine's canonical JSON serialization, not the on-disk whitespace.

`PaintedBattleSample` composes the existing engine, session, AI pacer and
battle screen through the shared `BoardSceneRendering` path. BoardProjection
owns cell-center contacts. The scene adds no second renderer, navigation mask
or command path. Level text uses existing Simplified Chinese catalog keys.

## Verification and limitations

- Native GUI checks exercised engineer movement from (61,23) to (65,23),
  node center/right-rim selection and transparent-pixel fallthrough. Subsequent
  selection did not change the canonical battle state. Site body registration
  was checked, but not every site's complete arrival/capture/recruit flow.
- One natural AI run completed 287 legal actions and matched its exact replay.
  The turn-15 sapper event ended that run, not an engineer arriving at the node.
  Three surviving oathbound units were rescued; one died beforehand. The run
  did not exercise new arrivals, captures or recruitment at any of the eight
  sites, or traversal of the alternate gates.
- Full route clearance, canopy/trunk and upper-gate ambiguity, and general
  clutter-footprint semantics still need review. Local barrel-base exclusions
  are authored terrain, not a general clutter interaction system.
- Performance remains unaccepted. On the cloud llvmpipe software renderer,
  world drawing dominated the measured cost and gameplay was slow. This does
  not establish either the specific expensive component or hardware-GPU speed.
- Fixed painted terrain does not support arbitrary terrain/elevation editing.
  Campaign integration, broad balance testing and final visual acceptance are
  separate work.
