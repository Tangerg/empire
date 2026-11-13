# C08 courtyard layout and hand-painted scene

C08 stays within the existing sixteen-level campaign. C09 is unchanged. The reviewed layout replaces twelve isolated wall bars with connected warehouse-yard boundaries: 36 blocked wall cells rather than 30. The southwest escape opening at (5,9) remains a road. Units, resources, elevation, all sixteen sites, contract branches, objectives and turn limit are unchanged. The optional ledger still requires an allied unit to finish an action in (10,7) or (11,7); merely crossing does not trigger it.

Canonical level SHA256: `1f4ca03f82ad5f66ef0386d154697b1cb770eb3b0fc054bc7844e756b7e7712c`.

## Presentation ownership

The scene binds `c01-08-a2-ground.json` and its eleven-type object catalog. All twelve wall pieces are pass-through for pointer input; the authoritative terrain still blocks movement/line-of-sight. Each selectable site has one body owner. Decorative texture never creates extra tactical occupancy. C01 houses remain reused without global filtering changes.

The ledger instruction is a read-only projection of the existing trigger/zone and `firedTriggerIds`. It reuses objective focusing and busy/revision checks, creates no new objective or progress state, and does not award money. The translated signal now reports obtaining the ledger rather than implying payment.

## Art provenance

New raster assets were generated with the built-in image-generation tool. Source alpha is preserved, not threshold-cut or chroma-keyed. Ground composition references the formal C01 hand-painted scene: low-contrast flat loading yards, walkable grass and transport routes, reduced gravel noise, no painted buildings or implied new obstacles. Grain-weigh-store is a same-canvas sibling of grain-store, preserving its anchor and silhouette while showing tied sacks and scales inside the existing doorway. Three horizontal wall assets complement the existing two vertical hand-painted wall sprites. No procedural face extrusion or UV mesh experiment is included.

The prior `c08-materialized` document describes the superseded baseline, not the current layout. Isolated research saves and rejected candidate assets are not runtime dependencies.

## Verification and limits

`GrainRoadChecks` retains sixteen-site ownership/clickability, all-unit rendering, ledger-cell picking and non-mutation checks, and adds all-actor movement exclusion for every wall, the restored escape door, and occupied-village body picking. The occupancy witness is explicitly a synthetic render fixture, not a legal campaign move. Opaque-material negative controls remain independent of wall artwork.

Actual GUI checks used legal replay checkpoints at 1180x812 and the formal minimum width 960x650. Ledger focusing, stop-and-wait collection, completed feedback and saved-state equality were observed. Render registration and checks do not by themselves establish visual acceptance.

With the original production AI, two explicitly recorded carried-party player policies demonstrate different tradeoffs: collecting the ledger plus retreating endangered Tasha wins on turn14 with four allied losses; protecting wounded Bran on turn1 wins on turn10 with Bran/Mirelle alive and three mercenary losses, but does not collect the ledger. Neither result is a guarantee for every play style or a reason to silently tune enemy stats. The independent AI failOn experiment is not included.

### Explicit fixture migrations

The C07 continuation remains the same 263 orders / 699 events / terminal `ce680ba7`. Only the resulting C08 opening hash changes `d9e1ef40` → `09211fb1`; production digest comparison proves the difference is confined to map tiles. Opening combat previews now cover 196 unit attacks (C08 17 rather than 18; all other fifteen attack sets identical) and the same 24 structure attacks. The two lost Bran firing positions (6,1)/(6,2) are behind the new wall; the new legal firing position (10,5) was formerly a wall. Every preview still compares actual committed damage, counterattacks and state.

The C08 regeneration witness retains 50 prefix and 13 hidden-boundary commands. Three wait paths and two later wait paths take real courtyard openings instead of crossing new walls; no attacks, skills, controllers or health values were changed. Direct production replay preserves the full positive and hidden event sequences: unit 7 recovers 5, hidden unit 13 recovers 20. Only the six corresponding state hashes and those legal paths are migrated. The fog flag remains the explicitly declared visibility-test boundary, not an invented gameplay continuation.
