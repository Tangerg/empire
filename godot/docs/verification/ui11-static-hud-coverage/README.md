# C01–C07 initial HUD display audit

Scope: detached canonical initial sessions, with idle plus every initially authored human-owned unit selected, at 960×640 and 1440×900. This is **not gameplay**, a late-battle state test, or a full-scene visual acceptance. No battle dispatcher, AI runner, intent subscription or save writer is installed. Every sample preserves its initial snapshot, zero revision and zero events. Six separately retained real C06 checkpoints remain byte-identical (private recovery evidence, not published saves).

178 display samples: C01 24, C02 26, C03 22, C04 22, C05/C06/C07 28 each. Visible Label boxes stay inside the viewport and expose all shaped lines. Visible buttons stay within the viewport and are not narrower than their Godot minimum size. Minimum width is not proof that every possible button string is readable. Initial data has no status effects; unit counts summarize into two faction rows, not a separate screen row for every unit.

Native Godot screenshots use the same production HUD, theme, portraits and weapon icons against a neutral background, explicitly marked static. C02's long escort objective wraps into two complete lines. C03's longest aggregate goal header is readable at 960. C04 Mirelle's long description and both real weapon-icon rows are fully readable through normal scrolling. No new confirmed layout/text defect was found; no production HUD changes were made.

## Limits

- The 178 automated samples do not open the dossier. Hidden dossier/roster contents are not counted as automatically verified; C04 actual open/scroll screenshots are separate evidence.
- This slice does not mount BattleScreen's global End Turn/Undo controls or the battlefield. Label bounds do not prove arbitrary sibling overlap or world-object occlusion. Earlier actual C01 UI8–UI10 integration evidence remains separately attributed.
- Long-log anchoring and historical late-battle status coverage are not freshly rerun. Do not call this an all-battle-state or all-window-size pass.
- The first C02 native log observed the window during its initial OS size transition; its `outsideWindow` comparison used the stale requested size. That log is withdrawn as numeric verification. The actual screenshot is 1180×812. The final native fixture waits for initialization and explicitly uses 960×640.
- C06/C07 actual actions remain paused; official C07 scene is unchanged.

## Reproduction

After the normal project build, run `res://Scenes/InitialHudCoverageChecks.tscn` headless for the complete static matrix. Optional native `--hud-level=c01-03` or `--hud-level=c01-04 --hud-unit=4` selects one initial display without attaching action handlers. Unknown levels fail rather than returning an empty successful audit.

The next bounded art task is the northwest gate's complete structural composition, not more HUD decoration. Preserve its original continuous opening/physical feet and independent leaf; resolve the previously rejected lookout-to-tower sleeve with a natural, single-owner composition before selecting it. Official C07 remains unchanged.
