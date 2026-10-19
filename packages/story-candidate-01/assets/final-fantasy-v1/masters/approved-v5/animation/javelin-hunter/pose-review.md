# Javelin hunter four-pose review

Catalog-only C01-UNIT-JAVELIN-HUNTER; no current campaign/built-in unit binding. Existing built-in soldier_javelin is on soldier, which uses C01-UNIT-SWORDSMAN, not this topic. Preserve original four-slot idle/walk-a/attack/walk-b order, anchor[16,47], footprint1x1. Reconstruct opposite walking contacts, retain shield throughout, and depict a clear overhand throwing wind-up just before release, retaining one held javelin and three back spares. No independent projectile/FX or new consumer.

Dark-brown-skinned young clean-shaven man, short curled black hair, bold brows, slate blue-gray scarf, ochre large-diamond quilted coat, brown strap/gloves/boots, brass belt buckle and beige cuff wraps. Exactly three spare pointed javelins in back carrier plus one held javelin, one original wooden plank shield with gray riveted rim and pale brass boss/brace bands. Shield stays screen-left and held spear screen-right. Exaggerated chunky head/hands/boots and broad matte color planes.

Native idle1585x992, walk-a1586x992, attack/walk-b1584x993. Export preparation may add transparent right/bottom padding to common1586x993; maximum2right/1bottom. No native pixel changes or per-pose fitting.

Largest measured adjustment is walk-a x+2.004555809logical from idlefoot737 to walk-afoot697. Recommend explicit limit3; all complete1586x993 padded canvases fit104x64 at origin[8,4].

## idle
Approved idle preserves original identity/gear; both boots planted with full shield, three back spare javelins and one held spear.
Source foot [737, 976]; sole-band midpoint [737.1939268730023, 973.0]; complete-canvas offset [0.0, 0.0]; full logical bounds [8.0, 4.0, 87.480638, 53.763098].

## walk-a
Right/far boot reaches forward and lands heel-first; left/near shield-side boot extends behind, heel lifted and toe low. One plank shield stays screen-left, held javelin stays screen-right, exactly three back spare tips. Same identity, canvas and apparent body scale.
Source foot [697, 969]; sole-band midpoint [697.0819956155858, 967.0]; complete-canvas offset [2.004555809, 0.350797267]; full logical bounds [10.004556, 4.350797, 89.485194, 54.113895].

## attack
Accepted overhand throwing wind-up: original screen-right spear arm raised overhead, complete held shaft aimed screen-right; shield remains on original screen-left forearm. Broad braced legs. All three back spare tips remain countable. Exactly two arms and two legs; no projectile or independent FX.
Source foot [701, 975]; sole-band midpoint [701.2102819660959, 971.5]; complete-canvas offset [1.804100228, 0.050113895]; full logical bounds [9.8041, 4.050114, 89.284738, 53.813212].

## walk-b
Accepted opposite contacts: near/shield-side screen-left boot is advanced into the foreground, heel down and toe raised with sole visible; far/spear-side screen-right boot trails on toe with heel lifted. This reverses walk-a right-heel-forward/left-toe-back. Shield remains screen-left and held spear screen-right, three spare tips, two legs, clear open leg gap.
Source foot [731, 968]; sole-band midpoint [731.0210437710438, 951.5]; complete-canvas offset [0.300683371, 0.400911162]; full logical bounds [8.300683, 4.400911, 87.781321, 54.164009].

## Rejected attempts
Attack attempt01 switched gear arms; walk-b attempt01 repeated right-foot-leading contact. Both raw images, exact prompts/requests, hashes and QA preserved.

## Provenance
Built-in imagegen with transparent_background=true. All native/raw/master and exact prompt hashes verified; source board/runtime/HD strips preserved. No source cleanup, per-pose fitting, repository edits, rule changes or invented binding.
