# Abandoned cart stop, index 13

Preserves the original standing settlement group: one covered timber wagon, two visible near-side spoked wheels, two forward shafts, two barrels, one open crate, one low rectangular four-legged timber table/platform, and one post with one hanging lantern. No extra occupants, flags or vehicles. The original olive-brown canvas and high three-quarter orientation are retained. The far-side wheel set is naturally occluded, not removed as a semantic change.

The broad irregular muddy stopping-place is functional source content, retained with its wheel ruts and water. This is not a flat decal or an arbitrary decorative platform. The final native edit reduced dense polygonal dirt mottling to a few long continuous matte brown areas. Two principal curved rut families are readable; four separated simple gray wet shapes remain, rather than the requested two groups. This is compatible with the original's several wet patches and does not add objects. A few grouped large stones remain; tiny original gravel/grass noise was intentionally simplified. Soil is not bevelled or raised.

The group is still standing settlement scenery, obstructionHeight 1 and metadata footprint 3x3. Runtime contract is 144 logical / 288 HD, anchor (72,139) / (144,278). Actual CANDIDATE_SETTLEMENT_LIFE consumers use base scale .3 plus deterministic variation up to .12, chance .42 and jitter 7; the audited c01-02 scene has four instances at .308/.357/.302/.340. c01-01 has no instances. Do not reclassify this group into groundPieces or use lying mode.

## Foot registration

Source registration foot (627,1054) is the visually selected middle of the forward contact edge of the functional mud patch, near the end of the main curved wheel ruts. It is an authored visual estimate, not the global alpha bottom and not inferred from an alpha bounding box. It preserves the original whole-group standing-foot convention. The native strongest silhouette extends down to y=1070 elsewhere, which is a small irregular edge lobe, not the contact anchor.

Additional visual geometry landmarks for review: rear visible wheel bottom approximately (445,557), front visible wheel bottom approximately (550,664), lantern post support approximately (249,550), and foreground table leg approximately (854,915). These are local object contacts, not replacements for the whole group's depth anchor. Extended ground-center reference is approximately (635,739).

A provisional uniform export factor of 0.124 logical pixels per native pixel maps sourceFoot to (72,139), placing the strong silhouette at approximately x=7.15..139.33 and y=37.69..141.11. HD factor is 0.248. This is a whole-source-canvas transform with translation (-5.748,8.304) at logical resolution, not a tight alpha-bbox crop. It produces approximately 132.2px logical group width, close to the old runtime's 134px width, without source geometry distortion. At the four observed consumer scales this group is approximately 39.9..47.2px wide. Final runtime registration and full-scene occlusion review remain the parent's responsibility.

## Provenance and revision

Full original 1254px atlas and diagnostic quarter were inspected before generation. Its 313.5px mathematical quarters require diagnostic integer cropping; the old (314,940)-(627,1254) crop is not a new registration specification. This particular original whole group is contained within the diagnostic quarter. No neighboring fragments were copied.

The initial native was preserved in attempts/initial-native.png. One built-in imagegen edit simplified only the ground treatment and increased the overall margin. The exact initial and final prompts are both preserved. No generated pixel cleanup, programmatic paint, alpha normalization or resizing was applied to either native source. Final raw.png and master are byte-for-byte copies of the selected native output.


Native output and alpha preserved byte-for-byte. Source foot is a visual estimate. Strong-alpha LTRB margins: [104, 237, 84, 183]. Strict perimeter alpha-zero check: FAIL, faint native edge alpha retained and explicitly recorded. No programmatic paint, color changes or alpha cleanup. Runtime scale, padding, anchoring and gameplay-size validation remain for parent integration.

Current source perimeter gate passes: native alpha-1 residue is retained unchanged; alpha 2 or greater would fail.
