# Combat action accents: implementation and verification

Implementation: `e0474632d0e9c4a2ee0a408aadfe5f962cd198fa`.
Baseline: `85bcf458396eb3898887dd31c69a97bb67170808`.

The existing actor poses now have compact motion sweeps and stationary focus
lines, sampled by the same finite clip clock. Stationary accents use neutral
colors: movement policy does not classify a bow as magic. A short angular impact
replaces the filled white circle. Damage values, release markers, recovery times,
source effect strips and damage-number lifetimes are unchanged. No core rules,
content balance, actor sheets or camera behavior changed.

See [action presentation](ACTION-PRESENTATION.md#clip-bound-action-accents) for the
ownership and visual scope. These are new procedural accents around existing
poses, not a newly drawn set of actor animations.

## Passed

- Native compilation: zero warnings/errors, including the restored final build.
- Formatting, source UID/resource metadata and authored scene static guards.
- All 17 native core groups, including all 16 independent level save/replays.
- Final `AnimationChecks`: **3,127 assertions**, including 16 new checks for
  anticipation, release, recovery cleanup, cancellation and disabled animation.
- Camera checks (36) and camera/screen integration (317).
- A separate final `BattleScreenChecks` run and its strict process/diagnostic gate.
- Separate `TacticalHudChecks` runs on both baseline and final implementation:
  1,163 assertions each, normal exit and strict diagnostic gate passed.

## Failed and remaining

`bash tools/godot/verify-native.sh` **failed**, despite reaching the above
animation checks. `TacticalHudChecks` printed its 1,163-assertion success marker,
then emitted unsafe AtlasTexture/StyleBox reference errors and exited 139 during
shutdown. Its wrapper correctly rejected this run. The isolated baseline and
final-code reruns did not reproduce the shutdown failure. This does not establish
its root cause, erase the failure, or constitute a passing full gate.

The aggregate stopped at that scene; subsequent aggregate stages were not run.
The separate final BattleScreen run covers that named scene only. Retained logs:

- [Failed full gate](verification/combat-accents-20261109/full-native-failed.txt)
- [Passing baseline HUD](verification/combat-accents-20261109/baseline-hud-passed.txt)
- [Passing final HUD rerun](verification/combat-accents-20261109/final-hud-rerun-passed.txt)
- [Passing final battle screen](verification/combat-accents-20261109/final-battle-screen-passed.txt)
- [Passing final build](verification/combat-accents-20261109/final-build-passed.txt)

No real-display or exported-package visual acceptance was performed. This
environment has no display server. Actual readability at gameplay zoom, terrain
occlusion, melee/ranged/healing/area effect composition and perceived timing still
need real rendered review. Headless timing and geometry checks are not visual
acceptance.
