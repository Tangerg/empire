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
Historical source-v1 oracle regeneration requires the preserved full repository
and its locked development dependencies. It is not part of current-schema acceptance;
see `OBJECTIVE-OUTCOME-SCHEMA.md`.

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

A self-contained Linux x86_64 release is now exported and tested outside the
checkout with an empty executable search path and no SDK environment. The
recorded preset and build/smoke commands are in [LINUX-PACKAGE.md](LINUX-PACKAGE.md).
Matching official Godot 4.6.3 .NET templates were checksum-verified and installed
workspace-locally. No system settings changed.

```sh
bash tools/godot/export-linux.sh /absolute/new-output-directory
```

This command builds, imports, exports, validates real runtime JSON/art and the
application lifecycle, and packages the release. The release includes .NET but
excludes source-test/oracle fixtures. Unknown/duplicate diagnostics and
pre-existing save data are refused. Imported PNG guards use ResourceLoader.Exists,
so exported remapped textures are tested rather than assuming source files exist.

The actual release passed 185 assertions across 22 runtime JSON documents and
159 textures, authored story/choice, movement, save/load/restart, camp continuation
and clean teardown. Those checks do not prove every order family, a victorious
campaign, typography, shader appearance, or actual desktop input. Keep user saves
separate from build cleanup. Windows/macOS exports need their own platform tests;
a Linux build is not proof of those platforms. The recovered original has no
audio runtime, so silence is expected.

## Desktop acceptance scope

The actual v4 Linux release passed title, Chinese story/choice, staging,
battlefield rendering, legal mouse movement and Save/Restart/Load restoration on
the cloud desktop. This is real input/pixel evidence, not only headless contracts.
The confirmed internal footer text was then removed; its source art-review
metadata remains in diagnostics/logs/tests without being shown as player copy.
Fresh rebuilt-release screenshots and the river scene are the next visual checks.

This does not establish every tactical menu, animation, later map, full campaign
victory, performance target or another platform. The earlier uncertain terminal
input was preserved untouched; the release test used a newly observed terminal
and isolated user data, with no desktop restart.
