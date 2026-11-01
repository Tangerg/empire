# C05 independent assets — unbound preparation

These thirteen built-in image-generation outputs are a recoverable material checkpoint,
not the accepted C05 scene. `c01-05.json` still uses the original painted village.
No gameplay, geometry, scene registration, or collision data changes in this batch.
See `prompts.md` and `manifest.json` for prompts, exact bytes and dimensions.

The first ground candidate retains some boulders; `ground-clean-v2.png` removes
those standing rocks while retaining the bridge/dock. Both remain unbound pending
registration review, and must not receive duplicate rock sprites. The rail candidate
has a measured slope discrepancy requiring review before binding.
House variants differ in occupied pixel bounds despite the same canvas size; each
must be registered from its actual foundation/door pixels, not a shared canvas pivot.
The watch shelter's plank floor must remain click-through; only its physical posts
and rails may participate in the existing furniture selection contract.

## Frozen baseline before layering

Reviewed `ead3f129df8916141ad907087a40f9929888529c`, canonical level SHA-256
`69f0b09cbe9d5daae16e4f36a063b3168af38146a5a7a0b6607c19b2be3656ab`.
Isolated native probes: 1044/1044 empty legal-cell centers Ground; 120/120 site and
approach samples Ground; 68/68 opaque actor-body witnesses select their actual unit;
zero renderer issues and unchanged state. These are synthetic probes, not gameplay.
The cart mouth at source (1028,442) correctly selects the neighboring village guard
at (31,15) in the populated opening; empty-map Ground is not the populated oracle.
Ten legal-ground Object witnesses are genuine open-shelter rails, not input defects.

Physical responsibility: 103 house record-cells + 47 rocks + 38 furniture = 188,
with ten overlaps, giving 178 distinct blocked cells. Keep the original seven-cell
bridge and nine-step bank-to-bank route; the dock is not a second crossing.
House foundations remain distinct from interaction sites. Authoritative coordinates
are in `../reed-village-geometry.json`; all 26 physical groups must be reviewed.

Local evidence: `empire-evidence-20261101/c05-physical-preaudit/` contains the native
probe, complete responsibility matrix, intersections and source crops. Registration,
formal native acceptance and actual C05 survival gameplay are still pending.
