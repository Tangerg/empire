# Ghost source and proposed idle contract

## Exact source and identity
Fourth row of the complete 1254x1254 combat-atlas-berserker-templar-inquisitor-ghost.png. First three rows are separate berserker, templar and inquisitor subjects.
An exposed human apparition with tousled gray-brown hair, clean-shaven youthful face, dark eyes and a sorrowful expression. Blue-gray scarf and blue-gray/brown leather armored coat, brown shoulder straps with small brass round fasteners, brown belt and hip pouch. Two bare empty hands. Limbs and lower trouser/coat remnants dissolve into warm ochre-gold translucent curling wisps, with a few small close-floating fragments. No skull face, hood, horns, staff, sword or physical shield.
Both hands are empty in every original frame. The apparent gold fragments and fading lower body belong to the original ghost silhouette; the independent ghost-touch effect remains a separate topic.
No sacred or faction emblem observed. Brass strap fasteners, buckle and pouch details are costume hardware, not invented runes. Warm ochre-gold edges are contained ghost-body styling, not an external glowing aura.

## Actual consumers
Ten initial campaign enemies across c01-11, c01-12 and c01-16; two explicit c01-12 turn-5 ghost reinforcements; one experience-lab enemy-ghost actor. No distinct built-in consumer found.
- c01-11: echo-3 at [10, 9] (levels.ts:688,691,694)
- c01-11: echo-4 at [14, 9] (levels.ts:688,691,694)
- c01-11: east-ghost at [18, 6] (levels.ts:688,692,694)
- c01-11: west-ghost at [6, 10] (levels.ts:688,692,694)
- c01-12: oath-echo-2 at [11, 10] (levels.ts:702,705,708)
- c01-12: south-echo at [15, 12] (levels.ts:702,706,708)
- c01-16: memory-1 at [9, 5] (levels.ts:780,790,793)
- c01-16: memory-2 at [15, 5] (levels.ts:780,790,793)
- c01-16: memory-4 at [16, 10] (levels.ts:780,791,793)
- c01-16: memory-6 at [18, 3] (levels.ts:780,791,793)
- c01-12 drowned-echoes trigger: two ghosts at [6,0] and [16,0], turnStart / turnAtLeast 5 (levels.ts:717–719)
- experience-lab enemy-ghost at [17,4], owner 2, facing west (index.ts:111)

## Four-pose and rules contract
Original idle and third attack are nearly identical upright hovering poses. Walk-a and walk-b show different arm extension and curled drifting lower-leg/wisp phases. The figure does not have ordinary planted boot contacts; lower limbs fade into connected wisps. Preserve hover semantics rather than adding a grounded infantry gait.
Actual ghost-touch is magic 36, range 1–2, arcane/undead tags. Unit is flying and unarmored. Attack will become a clear empty-hand reach; no invented physical weapon. Equipment C01-EQUIP-GHOST and FX08 remain independent.
Original art frame32x48, anchor[16,47], footprint[1,1]. Rule flying/unarmored semantics remain unchanged. Hover registration uses an explicitly estimated virtual support anchor at the lower connected wisp, not fictitious boot contact.

## Alpha contract
The source depicts a spectral faded body with gold-rim wisps. Original HD alpha is largely opaque in the central body (idle 2073 alpha255 pixels among 3657 nonzero); partial alpha is common near edges and particles. Do not claim original interior was uniformly semitransparent. Coordinator explicitly requests intentional bounded body translucency in the remake; empty canvas and limb/wisp gaps must remain truly alpha0, with no pervasive haze.

## Idle scale proposal
44 logical pixels from hair crown to the lowest connected body-wisp tip, excluding detached fragments. Native landscape 1586x992; tentative frame 104x64 at origin [8,4]. Large head and hands, broad matte ghost clothing, controlled partial alpha within the body only. Actual native idle alpha, geometry and white/dark-background legibility determine the shared scale and virtual hover anchor before other poses.

## Semantic pose plan
idle: upright sorrowful hovering figure, empty hands lowered, attached lower wisps contained near body
walk-a: forward drift with one spectral lower-leg/wisp curl forward and the other trailing; one arm balances forward
attack: clear forward extension of an empty hand/palm toward the target for ghost-touch, other empty hand balancing; no weapon or separate impact FX
walk-b: opposite lower-leg/wisp and arm drift phase, same hover camera/body scale

Read-only source audit complete. Await coordinator idle approval before generation.

## Idle pilot approved, 20:22 UTC
Coordinator approved source identity, 44 logical starting height and a virtual hovering-wisp anchor. Intentional body translucency must remain bounded, with no full-canvas mist. Empty-hand touch and independent FX stay separate. Generate idle first and measure before locking remaining poses.

## Single bounded translucency revision authorized, 20:26 UTC
Initial interior probes have alpha252–253, near opaque. The numeric 70–85% opacity range was a worker prompt target, not a user requirement. Coordinator permits one imagegen-only body-alpha correction, preserving v1. If the revision is still near opaque, report measurements and choose the better legal native version by true outer transparency, ghost identity, readability and contained spectral wisps. No programmatic alpha editing and no repeated numeric-opacity rerolls.

## Bounded translucency revision measured, 20:29 UTC
Native attempt-02 succeeds in meaningful bounded body alpha: chest median191, arm199 and lower-wisp interior197. Face stays near opaque at median253 for readability; no uniform opacity claim is made. Empty side regions have no alpha>1 pixels, all three 7x7 body-gap probes are alpha0, and perimeter alpha>1 count is zero. Full canvas remains1586x992. Largest connected alpha>64 body excluding detached fragments has crown y32 and connected-wisp tip y959, body927px, virtual anchor [831,959]. Proposed shared scale44/927, candidate104x64 at origin[8,4]. Both native attempts and exact prompts are preserved. Coordinator selection pending.

## Selected idle and shared-scale approval
Coordinator 2026-10-19 20:29 UTC reviewed black/white views and selected attempt-02: clear face and true bounded body translucency accepted. Lock shared44/927, virtual anchor[831,959], full1586x992, candidate104x64/origin[8,4], preserve identity across drifting phases and empty-hand touch.

## Floating anchor and alpha-consistency review contract, 20:32 UTC
Coordinator approved x registration using a fixed torso/belt-buckle landmark calibrated to idle, and y from the lowest connected body-wisp. Horizontal tail-tip swing must not translate the entire figure. List each landmark and offset at the unchanged shared scale. Walk-a returned near-opaque interiors (median253); continue the remaining poses, then compare the entire quartet. If the other poses are consistently near opaque, prefer comparison with original near-opaque idle attempt-01 rather than accepting one translucent idle and three opaque frames. Keep true-translucent idle attempt-02 as evidence. No repeated numeric-opacity rerolls.

## Final quartet selection and review, 20:43 UTC
Coordinator 2026-10-19 20:43 UTC reviewed native common-scale quartet: identity, opposite curling hover phases and extended-palm attack pass. Selected idle attempt-01 matches other poses near-opaque body alpha; successful translucent attempt-02 retained as alternate evidence only. Shared44/927, measured floating anchors and full-canvas registration accepted. Actual c01-11 BoardView verification and formal binding are coordinator-owned and still underway.
All three action outputs had near-opaque interior alpha253; original idle attempt-01 supplies consistent animation rather than one translucent frame among three opaque ones. No programmatic alpha change. The translucent idle attempt-02 remains intact. Walk-b attempt-01 mirrored pouch/scarf sides; one bounded imagegen correction attempt-02 restores scarf side and shows the near-side pouch edge naturally occluded by the forward hand. The coordinator accepted this and prohibited further rerolls. Final anchors idle[831,959], walk-a[833,926], attack[694,949], walk-b[714,950]. The x landmark is gold buckle center minus35.5 native pixels, y is lowest connected wisp. Scale44/927;104x64;origin[8,4];limit7;only walk-b requires one transparent right column. All original native pixels preserved.
