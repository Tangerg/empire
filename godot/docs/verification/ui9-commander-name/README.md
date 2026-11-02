# Commander display identity — 2026-11-02

## Actual defect

The C01 dossier displayed `编队 laiya-command · 光环生效`, leaking an internal commander identifier. The same 1180×812 C01 turn-1 view now displays `指挥官 莱娅 · 光环生效`; see the before/after images. Both use actual application data, not a generated UI mockup. No gameplay order was issued.

## One name owner

`BattleStateFactory.CreateCommanders` resolves authored `unitKey` to runtime `unitId`; `commander.id` is a rules identity, not a display label. The HUD now obtains the live leader through existing `TacticalModifiers.CommanderUnit`, then delegates to its existing `Name(unit)` / `BattleUnitNames` pipeline. That pipeline owns authored character identity localization and generic content-type names. The Chinese format now accurately labels the subject **指挥官**, not **编队**. No ID replacement table, parallel translation map, or name cache was added.

If no live leader exists, the row shows the existing localized **指挥官已离场** without fabricating a name. This contract does not promise a departed leader's historical name. Battle-log before/after identity recovery is receipt-scoped, not a persistent commander-name authority; expired casualty markers are not consulted. Aura active/out-of-range behavior still uses the same TacticalModifiers rule query. Save schemas and rules are unchanged.

## Checks

- New `res://Scenes/CommanderNameChecks.tscn`: **36 assertions**, detached C01 initial-state projection fixtures only. Covers active named leader, out-of-range follower, absent leader, generic unnamed leader, unassigned unit, and an alternate identity translation catalog. No command/AI/turn dispatch; snapshots, revision and event counts remain unchanged by projection.
- Absence is an explicit detached fixture with the leader omitted before save/load, not an actual kill, reconstructed playthrough, or modified user's save. Out-of-range uses a detached zero-radius fixture. Both pass the domain loader.
- Baseline runtime fails the intended `active: HUD must use the canonical localized leader name...` assertion; no fixture initialization failure is counted as a negative control.
- Fresh production build 0 warnings/errors, official editor import, targeted format check, source metadata check, and the previous **46** dossier focus checks pass.
- Independent read-only review confirmed the name owner and the deliberate absence boundary before implementation.
- Actual 1180×812 screenshot confirms the localized name and visible keyboard focus. The two-line bottom instruction remains readable in this view; the prior 960×640 capture also shows both lines. This batch does not claim exhaustive HUD text/scale coverage and adds no speculative layout change.

C06/C07 actions remain paused; no aggregate test containing their actions was run. The official C07 scene and selected scene assets are untouched.
