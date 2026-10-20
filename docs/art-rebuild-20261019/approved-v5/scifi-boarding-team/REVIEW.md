# Boarding team authored quartet

The original full combat-board-06 was inspected. Row 2 defines two astronauts, four legs, an orange boarding-cutter operator with cable reel and a blue oval-shield bearer. Equipment-card substitutions were not used.

Four raw 1254×1254 RGBA masters are preserved byte-for-byte. Exact built-in requests and original output paths are in generation-provenance.json. Rejected idle placement, five-boot attack, and repeated-stride walk-b are retained.

The unchanged export_frame_overrides.plan passes against temporary-frame-overrides.json and returns exactly one existing native 256×48 sheet, with four 64×48 frames. There is no invented HD consumer or runtime write. Shared renderWidth 44, origin [5,3], offsets idle [2,-1], walk-a [2,0], attack [2,0], walk-b [-2,0]. Full canvases fit; the ±2 guard is unchanged.

The correction restores attack to four boots. Walk-b clearly reverses the orange operator contact relative to walk-a, while the blue bearer's alternating motion is subtle. Native team identity, shield, and raised cutter remain distinguishable on three backgrounds.

## Contact limitation

Measured support has 1.18 px X and 0.70 px Y residual drift and sits 2.14–2.84 px above the canonical y47 anchor. An originY4 candidate failed native perimeter alpha (walk-a edge alpha 3); one extra padding pixel makes the valid diagnostic candidate. No alpha cleanup or cropping was applied. Scene-contact evaluation is still required before acceptance. This quartet is not claimed integrated, ready, or live-game verified.

Reproduce with python docs/art-rebuild-20261019/approved-v5/scifi-boarding-team/verify_bundle.py from repository root. This only produces local diagnostic outputs in this topic directory.

## Shared-renderer contact proof

renderer-contact-proof.ts calls the actual runtimeUnitPicture and boardStripMarkup helpers at scale 1 with the existing 64×48 frame and [32,47] anchor. In the resulting PNG, projected sole y28.16–28.86 overlaps the unchanged shadow ellipse centered y29.2 (extent25.0–33.4); no obvious floating gap is visible. This is a static helper diagnostic, not a C02 scene or gameplay test. The two-person stance is wider than the central contact ellipse.

The one-pixel padding obeys the existing perimeter alpha<=1 contract in export_overrides.validate_alpha, which was not changed. An originY4 native projection fails edge alpha3; originY3 passes without editing source alpha.

The remaining stance limitation is that walk-b clearly reverses orange operator contact, but the blue rightmost boot stays the main plant in both walking phases. Do not describe this as fully synchronized alternating two-person gait.
