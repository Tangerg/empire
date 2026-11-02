# West front wooden planter — shared asset / independent placement

This adds only a new instance to the unique selected assembly, reusing the existing reviewed wooden planter asset and its 82 measured local supports. No new texture, mirrored east feet, runtime exception or competing asset definition. Official C07 and runtimeReady=false are unchanged.

## Ownership and redesign

Original visible stone foot is approximately (691,483) → (706,491) → (717,485), with asymmetric left/right spans. Hidden rear is not certified. West rear white container and a separate low corner pier are NOT this object's feet: the bright top near(683,475)/foot near(685,482) belongs to that low pier. Their absence remains explicit.

Final new design uses scale .032, anchor(640,1056), world contact(699.5,491), authored hidden rear(699.5,479). This westward repositioning keeps the complete ground-standing stone body outside the platform. It does not reproduce the original three feet exactly. The fixed original point(706,490) is now transparent, and(700,486) changes from source stone to new wooden face. Neither difference is hidden or reclassified as a precise material restoration. original-point-differences.json preserves all fixed samples. Shared texture remains unmodified.

## Bounded checks

- Original actual Torren p(43,32), east: source points(700,486)/(706,490) both opaque on the actor and actor picked, with no old depth owner. This is a front role, NOT evidence of old rear occlusion failure.
- New candidate and final selected: fixed solid location(700,486) remains owned by this instance but correctly behind actor; 44 true opaque overlaps all preserve actor drawing/Pick, zero wrong depth. New fixed control explicitly tests solid ownership rather than exact old stone material.
- Read-only nearest legal two-facing search found no actual overlapping Torren back role (46 poses); no invented back pass.
- 816 measured visible stone-foot columns all fall on blocked cells; 324 directed edges and 82,950 actual walk-a/walk-b sole segments have zero intersection. This is bounded static math, not all appearances/animations/engine movement.
- Complete stone-body box[686.988,480.568]–[713.196,491.064] is separate from all platform member bounds. A provisional contact x704.5 fails this conservative clearance guard; this does not alone claim exact alpha penetration or visual failure.
- Wrong registration reaches and fails `West wood planter must own fixed solid witness and preserve actual front role`. A deliberately north-shifted foot reaches and fails the blocked-foot assertion; provisional body bounds fail complete stone separation. All three failures occur at their intended contracts, not initialization.
- Prior40instances, catalog assets and official scene unchanged; six paused-save hashes unchanged. Fresh production build, static selected structure and original/candidate logs retained.

Root reviewed the same-view original/candidate: ground-bearing and separation clear; accepted as rules-first geometry redesign, not final art. Foliage/contact-shadow/style and rendering costs remain scene-wide debts. This instance must NOT be used to claim the whole original50blocked-cell region is covered: withdrawn original foot and remaining bare blocked centres remain in the upcoming complete responsibility inventory. Do not assign them to absent objects without source evidence.

No movement, wait, turn, enemy AI, real-save write or gameplay was performed. C06/C07 gameplay remains paused independently of authentication.
