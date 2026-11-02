# Courtyard wall — actual walk-foot corridor follow-up

This is a new bounded geometry repair after d5558300/v72, **not a gameplay run**. Official C07 remains unchanged/unbound; six paused saves are byte-identical. The prior idle-standing pass did not prove continuous entry. Prior audit/probe remains frozen and its failing real walk boundary is preserved here.

## Actual counterexample, not assumed facing

`BoardView.Animation.cs` draws before-state then uses one support pose per path segment (`walk[segment % 2]`). Projected vertical delta0 retains the existing sprite mirror; `ApplyFrame` changes texture/anchor without resetting Scale. Thus west-facing vertical walk-a is possible. A nonmirrored two-segment legal terrain path (24,17)→(24,16)→(24,15) also uses walk-b on segment1 and reproduces the main wall defect; no special initial west pose is required. This establishes source/terrain compatibility only, not current-save occupancy or an executed order.

After inspecting the actual sheet, fixed front-support boot ROIs are walk-a local x280..380/y515..575 and walk-b x30..125/y515..575. Each X contributes its lowest A>=200 pixel:89 and86 points. The other lifted boot, sword/shadow and whole-sprite alpha lower bound are not substituted for ground-contact evidence. These are visible shoe contours, not hidden3D sole certification.

Frozen old failures: walk-a (338,564), A248 at (382.9615428,252) inside pier; walk-b (40,559), A247 at (377.1923148,248.8461544) inside main and pier. Both exact witnesses remain in the verification and old geometry archive.

## One bounded design change

Beyond source x365, wall-end and pier X coordinates are continuously narrowed from the old x387 end to x375.5. Faces remain straight, vertically consistent, connected; no per-pixel notch, actor change, action-code change, collision change or tolerance relaxation. This changes original right-end foot positions intentionally and is **new design**, not precise source restoration. Pillar is visibly thinner; final-art quality still fails.

World geometry/UV/contact are compiled through the same formal owner. A partial last slab exposed an offline exporter assumption: x+.5 could lie outside a subpixel-width owned span. The exporter now takes midpoint of its actual emitted polygon X extent and evaluates the same source support there. Runtime validation remains strict. First contact rejection is preserved. A subsequent attempted rerun used stale DLL after an external archived `.cs` caused duplicate compilation; it is explicitly EXCLUDED. Archived source is now `.cs.txt` in the external harness, fresh build succeeds, then export/probe are rerun. No failed build is counted as success.

## Scope of successful checks

-20canonical legal cells /50directed adjacent edges; actual horizontal movement mirror, both retained vertical mirrors, both walk phases.12,600 individual shoe-point continuous line segments checked against formal-cap-derived physical main/pier bases, with inclusive edge intersection, **zero intersection**. This is not merely17time samples and not all appearances/all C07 routes.
- Two frozen original failures are inside old geometry/outside current. Existing93-point idle boot both-facings standing remains clear.
- Prior Torren (24,15)/(24,16) east pier overlap disappears after actual material narrows. Original probe and its zero-overlap failure remain unchanged as **no longer applicable overlap controls**, not reported as passes.
- Separately named `GranaryCourtyardWalkProbe` uses surveyed actual C07 raider-1 at those two legal cells:28rear/25front material overlaps, correct local depth/Pick. Main retains Torren17/215front and raider4rear overlaps. These new witnesses do not rewrite the meaning of old ones.
- Wrong-main-flat265 and background-pier controls must initialize and fail specifically at the front/back depth assertion; final logs/results identify them separately.
- Same unique selected assembly,35instances/6owned sites/2598depth entries. Previous33instances and definitions unchanged. Fresh production and external probe builds, metadata, save hashes recorded. No frame-rate/performance pass.

## Remaining

Wall is gray/flat with unsegmented cap and insufficient contact shadow, and narrowed terminal is thin. Root accepted only bounded rule-corridor geometry candidate, **not final art**. Stop local material loops; continue separate left timber fence/connected courtyard responsibility. No official scene binding or C06/C07 action advancement.
