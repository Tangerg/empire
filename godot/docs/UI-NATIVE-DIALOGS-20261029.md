# Native dialog visual checkpoint — 2026-10-29

## Change and ownership

Existing campaign notices, overwrite/restart confirmations and the journal now
share the approved leather dispatch-board nine-slice, a restrained gold native
title frame, and a 24×24 close target. No artwork was regenerated. The source
corner cuts and fixed 24px destination corners are the existing DispatchPanel
contract. A canvas layer paints beneath native content; AcceptDialog remains
responsible for wrapping, action placement, dismissal and keyboard behavior.

Confirm remains visually secondary/destructive; Cancel retains the default focus.
A mouse-ignoring light backdrop separates the modal from the battlefield. It is
shown only for that dialog and removed with it. Root notices are dismissed on
both direct and prepared view transitions, so an outgoing scene cannot leave its
notice or shade over the next scene. No gameplay pause/command rule was added.

Godot 4.6.3's embedded title close supports normal and pressed textures, not a
separate hover/focus theme state. Its texture dimensions define the hit rectangle.
We use those native states rather than adding custom pointer tracking. Explicit
Cancel/Close controls retain the full themed focus/hover/pressed keyboard paths.
See the engine [viewport implementation](https://raw.githubusercontent.com/godotengine/godot/4.6.3-stable/scene/main/viewport.cpp)
and [AcceptDialog layout](https://raw.githubusercontent.com/godotengine/godot/4.6.3-stable/scene/gui/dialogs.cpp).

## Verification and evidence

Evidence prefix: `empire-c01-evidence-recovery/ui6-`.

- `ui6-after/restart-1180.jpg`: real native restart confirmation over the isolated
  QA manual-save battle. Same angle as `ui4-after/restart-1180.jpg`. No restart or
  new victory was executed; this is a UI review, not gameplay completion evidence.
- `overwrite-1180.jpg`: real campaign overwrite confirmation. Enter canceled;
  repeated opening and native title X returned to the unchanged menu.
- `notice-640.jpg`: explicitly labeled UI fixture at 640×480, not a save failure
  or actual victory. The title, wrapped body, material corners and Close all fit.
- Actual GUI Esc closed the battle confirmation and fixture notice with background
  brightness restored. Headless native-button checks cover Cancel/Confirm/reopen,
  viewport coverage, safe default focus, surface fit and unmount cleanup.
- Synthetic emission of Window.CloseRequested is not the engine's native window
  close notification and cannot stand in for an X click. X/Esc dismissal is thus
  verified with real GUI input, not a misleading signal-only test.
- A negative control leaving the modal shade visible after native-button dismissal
  must fail the lifecycle assertions; the original source is restored afterward.
- Root notice transition checks cover both SetView and prepared ShowNode activation.
  They run after the existing initial-save fixture establishes its manual-slot
  baseline; running them before baseline setup would falsely report a changed slot.

No new chapter, gameplay rule, simulation state, image asset or real-campaign
completion is claimed. Existing source-environment runtimeReady=false diagnostics
remain unchanged. GitHub publication is still blocked by the previously reported
network/auth environment restriction; no new route or auth retry was attempted.

Final affected gates: Campaign UI **200**, Navigation **305**, Camera integration
**277**, Result flow **154**; all passed. Build: zero warnings/errors; source
metadata: **305** unique script UIDs. `ui6-negative-shade.log` records the killed
stale-shade negative control. `ui6-save-check.log` confirms both pre-existing QA
manual/autosave files are byte-identical. No full campaign replay was rerun.
