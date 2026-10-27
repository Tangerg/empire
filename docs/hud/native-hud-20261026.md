# Native tactical HUD — 2026-10-26 / 2026-10-27

## Current composition

The second pass replaces the first pass's full-height command rail. The painted
battlefield now occupies the whole native viewport. HUD controls intercept input
only over their actual islands; their transparent root is not a second map hit
owner.

- A slate-blue cloth pennant at the upper left holds mission, turn and resources.
  The upper-right paper checklist retains both named objectives and canonical
  progress; its disclosure does not expose hidden objective leaves.
- The bottom-left selected-unit card pins the acting unit instead of following a
  target hovered during a pending order. Name, short action state, current/max HP
  and the HP gauge have reserved space. Faction and secondary statistics live in
  the dossier, not in the health line.
- An 80-pixel portrait medallion rises above the card. Eight named heroes use
  individually reviewed, explicitly declared head-and-shoulder AtlasTexture
  regions. The original narrative portrait image remains untouched. Generic
  units continue to use their declared idle-frame art.
- The nearby command island grows only to the current orders (up to four columns),
  with short names, native icons and shield silhouettes. Complete descriptions
  and costs remain available as tooltips. One order still reserves room for its
  cancel control and instruction; it does not create an undersized strip.
  Selecting an actionable unit immediately exposes canonical commands at its
  current cell (labelled “origin orders”), rather than disguising details tabs as
  skills. Map movement and move-attack preview remain the controller's existing
  paths. Details have one entry on the unit card.
- An acted or enemy unit is inspection only: health, action state, dossier and a
  close control, with no fake command bar or unrelated commander tactics.
- End Turn is a separate, single-line control at the lower right. Undo remains
  visually secondary. No empty full-width opaque bar is drawn behind the map.
- A forecast replaces the bottom command island only while a valid target is being
  previewed. Its two columns show the acting unit's counter-damage/remaining HP
  and the actual recipient's strike-damage/remaining HP. Interception, collateral
  and lethal warnings remain visible. Cancellation shares the forecast header;
  numeric modifiers move to the dossier.
- Details, forces, terrain, reactions and formations use a native popup with
  disclosure/focus preservation. Core selection and forecast information do not
  require scrolling at 960×640 or 1180×812.
- Recent events appear as a three-second toast, while the original `BattleLog`
  retains history for the system menu's read-only battle report. System save,
  load, restart and camp controls remain separate; restart requires confirmation.

The cloth, paper and wood/copper textures are original generated HUD assets, not
copied game UI. Runtime atlas regions remove transparent outer padding without
editing the source PNGs. Wood frames use nine-patch sampling with fixed-scale
corners. The surface is outside its text MarginContainer so layout cannot shrink
the background to the content rectangle.

## Ownership and input

`GrayFlagSession.ProjectHud` projects canonical names, objectives, resources,
combat results and revision-bound intents. `TacticalHud` owns presentation layout,
local disclosures, focus and artwork composition, not battle legality or damage.
The selection controller remains the sole owner of selection/target transitions.

Selecting a destination is always a preview, including one's own cell or a cell
whose only order is Wait. Only choosing an order commits. An explicit origin Wait button uses the queried
own-cell path and submits through the same command/session boundary. A non-self
origin command opens typed target preview even with one target; Escape changes no
battle revision. Acted actors cannot dispatch origin commands. Cycling available units
updates the canonical cursor hit; illegal clicks retain context and explain the
refusal. Stale/detached callbacks and repeated emitted button signals are inert.

`BattleScreen` keeps one full-size map viewport and one world transform. The
existing `BattlefieldCamera.Resize(viewport, insets)` reserves HUD framing space
once; map dimensions are not reduced again. Tab/Shift+Tab use native focus;
focused buttons own Enter/Space/arrows, and Escape returns to the map. Popup focus
is read from the popup's viewport rather than the main viewport.

## Verification and scope

Focused native checks cover wide/narrow layout, named/hidden objectives, pinned
identity, visible health above the dossier button, paired forecasts, compact
cancellation, single-command material bounds, disclosure/focus preservation,
callback lifetime and validated portrait crops. Visual acceptance is separately
recorded with real native C07 screenshots, not inferred from headless assertions.

Only independent copies of already-played C07 saves are used. No C08 continuation,
rule change or save-schema change. The completed original T13 save is untouched.
The original C07 T11 screenshot and save already show the granary completed and
1/2 overall; the earlier instantaneous 0/2 observation is not evidence for a
persistent counting defect and does not justify changing victory rules.

Use `--capture-size=960x640` or `--capture-size=1180x812` for reproducible actual
native window sizes. Evidence and recovery patches are retained separately in
Library. Remote publication is not implied by local commits or verification.

Native pointer ownership: equivalent projections retain controls; changes to hover details preserve an unchanged revision-bound command subtree without detaching it. Global orders are stable within a revision/busy binding. Forecasts stay inside the bottom safe area rather than covering the hovered map target. Native viewport press/release tests cover a hover refresh between press and release and duplicate-release suppression.

The dossier PopupPanel and its ScrollContainer live for the HUD lifetime. Opening is deferred beyond the current GUI input event and revision-checked; projection refreshes replace only dossier content, never unregister or reopen the active native subwindow. Tests exercise native opening, synchronous hover refresh during visibility change, native disclosure clicks, and repeated close/reopen.
