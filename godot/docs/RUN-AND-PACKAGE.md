# Run and package the native GreyFlag build

## Verified development run

From a complete recovered repository checkout, with the approved sibling
`empire-tools` installation:

```sh
bash tools/godot/run-native.sh
```

For another workspace-local toolchain, set `EMPIRE_TOOLCHAIN_ROOT` to its root
before running the command. The helper builds the C# project, imports Godot
resources, then runs the actual campaign. It does not install software, publish
files or clear an existing save on launch. The initial title menu offers New or
Continue when a valid campaign autosave exists. New requires confirmation before
replacing an existing valid campaign. Ordinary final story beats, choices,
prebattle entry, victory settlement and ending completion save the campaign-only
slot automatically. F5/F9 (or battle Save/Load controls) use a separate full manual
slot, including a pending battle. Exit to camp restarts that battle on reentry,
while returning to the title and Continue uses the last clean autosave boundary.
Invalid saves are reported and left intact; explicit New replaces only the
autosave, never the manual slot. Preserve both files when migrating machines.

The slots are `grayflag-campaign-autosave.json` and
`grayflag-campaign-save.json` under Godot's application user-data directory, not
inside the checkout. With the workspace helper, this is normally
`$EMPIRE_TOOLCHAIN_ROOT/xdg-data/godot/app_userdata/Empire GrayFlag Migration/`.
A directly launched desktop editor uses that platform's normal Godot user-data
location instead. Headless smoke checks use isolated temporary user data:


```sh
bash tools/godot/verify-native.sh
```

On another desktop, install the official .NET 8 SDK and Godot 4.6.3 **.NET** editor.
Open `godot/project.godot`, build its C# project and run the main scene. The source
archive contains `godot` and `tools/godot`, but not the original TypeScript tree,
Node dependencies or the toolchain. Native build/gameplay needs no TypeScript;
`--full` oracle verification requires the preserved full repository and its
locked development dependencies.

## Tactical controls

Select a unit, then a destination, then a command. A destination with only one
self-targeted command commits immediately, matching the original game. Right
click or Escape steps back through targeting, destination and unit selection.
Tab or Space cycles available actors; arrows or WASD move the cursor and Enter
uses the same cell-selection path as the mouse. In a destination menu, A/C/H/W
select attack/capture/heal/wait before their cursor-key meanings. E ends the turn
and U undoes the last eligible human action. Successful handoff clears undo;
loading or restarting does not restore an undo stack.

The sidebar exposes source forecasts, inspection, objectives, accounts and
support/deployment/recruitment options, including unavailable options and their
reasons. Hovering and leaving do not commit actions. Drag to pan, use the wheel
to pan, or Ctrl/Command plus wheel to zoom. Save/load and tactical commands are
blocked during animation and the AI's full post-settlement pause. The following
source-backed UI contracts are under active integration validation; the exact
checkpoint report, not this control guide, states what has passed.

## Packaged release gate: not yet built

This checkpoint is a verified development project, not a tested stand-alone
binary distribution. Godot requires a matching export preset and export templates
for release export. See the official [4.6 export guide](https://docs.godotengine.org/en/4.6/tutorials/export/exporting_projects.html)
and [command-line export guide](https://docs.godotengine.org/en/stable/tutorials/editor/command_line_tutorial.html).

When the official 4.6.3 .NET export templates are available, create a Linux desktop
preset in Project > Export. Include runtime JSON data and all selected art; exclude
test oracles, test scenes and development documentation. Record the preset in the
repository, then export using its exact name:

```sh
"$GODOT_NET" --headless --path godot --export-release '<recorded preset name>' '<absolute output path>'
```

The placeholder is deliberate: no preset or template installation is claimed in
this checkpoint. Validate the resulting release outside the source checkout,
without Node/TypeScript or editor caches. Test story/choice, movement/attack/AI,
all supported order families, save/load/restart, finishing a battle and campaign
continuation. Verify JSON/art resolution, fonts, shader/material appearance,
fresh-user save location and expected silent behavior (the recovered original has no audio runtime). Keep user saves separate from build
cleanup. Windows/macOS exports need their own platform tests; a Linux build is
not proof of those platforms.

## Visual acceptance still pending

Headless resource and timeline contracts do not replace visual acceptance.
Earlier real desktop story/choice/save/load/restart checks passed before the
latest water and animation work. New terrain previews were inspected, but the
latest desktop inspection responded, but its bounded terminal-input attempt timed out with partial input uncertain. Do not label the
new integrated animation/campaign UI visually accepted until it can be inspected
and exercised on a responsive native desktop. No desktop restart was attempted.
