# FX13 scorched embers: proposed nonloop phases

Actual runtime strip and atlas04 row1 were inspected. The visual progression is a compact dark cracked-earth patch, then crack ignition with rising wisps, peak short flames and sparks, then low residual embers. The patch is part of the source subject and should remain registered. It is not permission for a rectangular scene backing.

Proposed strict phase order: `cracked-earth`, `ember-rise`, `flame-peak`, `ember-residue`.

Preserve the current manifest contract: four 32×32 logical frames, 12fps, loop=false, normal blend, anchor [16,16]. The old source audit recorded a historical [16,24] anchor; the current manifest is the authority and is now [16,16]. The fourth phase is a nonempty terminal residue, not ambient renewal. No 4→1 continuity is required. Exact samples start at 0, 83⅓, 166⅔ and 250ms; the nominal clip ends at 333⅓ms. Final hold/removal belongs to a future consumer; no 420ms hit lifetime is assumed.

Use four genuine 1254 RGBA drawings with one common whole-canvas 28/1254 transform and source pivot [627,627]. Keep earth outline, cracks and contact registered while fire and smoke rise and subside. Source exterior alpha must be ≤1 with clean actual black/white and native terrain composites. Final 2px logical padding must be fully clear.

No literal current runtime consumer was found in the inspected package TypeScript/TSX and nonmanifest JSON search. This remains a catalog-only asset. No new art has been generated; phase order awaits parent approval before any call.
