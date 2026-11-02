# C07 rear-west seat and planted bed — bounded geometry closure

## Result and limits

One coherent rear-west group joins the unique selected assembly: **48 instances, 44 assets, 6 semantic sites, 3,882 depth entries**. The official C07 document, game rules, AI and paused saves are untouched. This is not gameplay or final city/art/performance acceptance. The selected scene remains runtimeReady=false. Excessively orange wood, hard edges and missing contact shading remain art-unification debt. The rear-east bed's prior native/geometry candidates remain FAILED/unselected and its original fixed leaf defect is unresolved.

The prior 50-centre responsibility inventory is included. Two additional centres now have actual material (30/50 total); this is NOT foundation coverage or 60% scene completion. The original brown lower-seat centre (728,456) becomes a designed open space beneath the new seat, not an omitted stone foot. No arbitrary solids fill all remaining centres.

## Source facts versus new design

Original wood near ends are around (722,459–460) and (739–740,452); visible wood is not the flower-bed stone foundation. The original complete stone foot is hidden by the column/plinth. A bounded survey of 12 actual C07 idle appearances and 96 legal northern display poses found no foliage overlap: **no second proven current-role rear occlusion bug is claimed**.

V1 resembled a box/slope and was visually rejected. V2/V3 use a genuinely thin seat edge, backboard and four finite 2px diamond legs. Opening the ambiguous original dark apron and completing rear legs are explicit design, not pixel restoration. The separate bed foot was moved inside blocked ground to (741,450),(745,454),(750,451),(744,446). The old wider bed collides with the actual right leg; point-only clearance was not sufficient.

The final four leg footprints and whole bed are all blocked-ground domains. Minimum wood/stone separation is 1.62635px. Each is tested against 82,950 continuous segments formed from the fixed Torren walk-boot witnesses and legal adjacent routes: zero intersections within this bounded set. No all-appearance/all-city traversal claim. None intersects the 670 independently visible core feet or actual platform member geometry; unknown hidden core foundations are not replaced by a bounding box.

The existing material is reused without raster edits. Each component has a physical support owner. The new flower ground root (743,449) projects to soil root (743,443), native UV(650,620), alpha253, inside the new soil. Soil and four stone rims are horizontal carriers projected down their actual 6px height. Four legs retain separate finite supports, and wood-seat members do not borrow the flower-bed root. Runtime draw/pick/bounds/depth use the existing compiled surface owner. Offline source is provenance; generated runtime surface is the sole current fact.

## Verification

- Fresh production build and external probe build: 0 warnings/errors.
- Formal ownership positive passes: 8 soil/foliage and 5 stone-rim/foliage material overlaps; no carrier covers the rooted foliage. The foreground seat legitimately covers 5 separate overlapping pixels; this is reported, not forced behind everything.
- Three valid negatives reach their intended assertions: late carrier support (+20px) fails the above-root physical-projection assertion; +10px whole registration fails actual material ownership; original wide bed fails finite-leg/stone-domain separation.
- V1 diagnostic initially mislabeled (744,451) as bed-left though actual owner was bed-right. That premature expectation is withdrawn and is NOT a valid negative. The corrected final new-foot sample (744,452) is independently on the new left stone face. V2 then exposed the real 13-pixel carrier ordering failure. All failed snapshots/logs remain.
- Current source/root merge regenerates byte-identical production surface; original 47 instances and 43 asset definitions remain exact. The study merger replaces its own one instance rather than appending duplicates when the selected assembly already includes it.
- Static selected-assembly ownership/sites/pointer contracts, core, platform, existing southern group and 50-centre inventory all pass on the new selected composition. No action/AI/turn/save commands are involved.
- Six paused C06 save hashes remain unchanged, including e8add96b2258b1e940ddecef4d6f5bda7414127562f89df47144325509e69bdf. Official C07 SHA256 remains 5bd8cac7e2263dcffaf23b88c38b25515575678c3085a227e4a1f58f61abcd88.

## Evidence / reproduction

`central-rear-west-original.png` and `central-rear-west-study-v1/v2/v3.png` are native static captures at the same 1180×812 view, focus45,27, zoom5. No clip is presented as a real playthrough. V3 was captured before selection with the exact same 48-instance composition; `central-rear-west-selected-immutability.json` records unchanged preceding instances and byte-exact selected surface.

`build-central-rear-west-group.py` writes explicit source parts; each part is exported by the existing `TexturedSurfaceExport.tscn`, then `merge-central-rear-west-group.py` assembles local coordinates. `CentralRearWestOwnershipProbe.cs` calls the formal loader. `run-central-rear-west-regression.sh` lists the pure-static affected regression batch. The separate math source and input snapshot live in `central-rear-west-bed-review/`; CLI source/output arguments support the old-bed negative without overwriting the current report. All paths in these archived scripts are execution evidence, not new production entry points.
