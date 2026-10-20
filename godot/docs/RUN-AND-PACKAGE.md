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
files or modify an existing save by itself. The application writes a save only
when Save/F5 is selected. Headless smoke checks use:

```sh
bash tools/godot/verify-native.sh
```

On another desktop, install the official .NET 8 SDK and Godot 4.6.3 **.NET** editor.
Open `godot/project.godot`, build its C# project and run the main scene. The source
archive contains `godot` and `tools/godot`, but not the original TypeScript tree,
Node dependencies or the toolchain. Native build/gameplay needs no TypeScript;
`--full` oracle verification requires the preserved full repository and its
locked development dependencies.

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
cloud desktop later stopped answering bounded read-only calls. Do not label the
new integrated animation/campaign UI visually accepted until it can be inspected
and exercised on a responsive native desktop. No desktop restart was attempted.
