# Next active environment priorities

Snapshot: clean detached `654b382`, before the first four new cliff cells and the new plaza binding. The first cliff quartet (indices 4/8/12/15) is excluded from the next-batch priority. This is **not** a claim to measure the later f8789f8 artwork.

Both complete actual BoardView scenes are retained: C01-01 has 19 rule actors and 4 rule buildings; C01-02 has 20 and 9. No objects or facings were selected out. PNGs are offline Inkscape evidence, not a live browser/GPU capture.

## Next four cliffs

| ID | Slot | C01-01 / 02 | Actual scale | Source area | Visible area |
|---|---:|---:|---|---:|---:|
| temperate-low-ledge | 10 | 9 / 1 | [0.493, 0.579] | 18773.15 | 11398.70 |
| temperate-cliff-straight-south | 0 | 2 / 0 | [1.0, 1.0] | 13123.44 | 7408.87 |
| temperate-ramp-level-2 | 6 | 2 / 0 | [1.0, 1.0] | 10624.12 | 6993.58 |
| temperate-talus-foot | 11 | 5 / 2 | [0.472, 0.513] | 8839.01 | 6717.03 |
| temperate-mountain-pass | 7 | 3 / 0 | [0.601, 0.605] | 7255.80 | 2418.72 |

The first four cover 21 of the 24 remaining active cliff instances and 93.08% of their visible alpha-weighted area. Mountain-pass is the only remaining active cliff ID after that batch; the other catalog slots have zero instances in these two scenes.

Areas are display-1x alpha-weighted px², not bounding-box pixels: source alpha × transmission of every later actual drawing, including frame and later layers. The diagnostic removes root-scoped grading, retains source clipping/facing/shadows, and includes every later occluder. It is a visual-impact measurement, not a gameplay visibility rule. New silhouettes may change these areas.

### Full-subject integrity

- Straight-south extends to x330; its old quarter ends at314, cutting16px off the right side
- Mountain-pass starts at x919; its old quarter starts at940, cutting21px off the left side and placing455 strong-alpha pixels into ramp-level-2
- The ramp reference contains only its continuous two-step mass, without the foreign isolated fragment
- Five faint pixels (alpha≤19) from the next row expand the old talus slot bbox to y940 and make the true subject undersized. Do not imitate that old runtime padding
- All five full-subject references preserve original source pixels. Diagnostic crops are not registration transforms

## Active camps

| ID | Slot | C01-01 / 02 | Scale | Visible area | Status |
|---|---:|---:|---|---:|---|
| field-tent-cluster | 4 | 2 / 0 | [0.419, 0.72] | 4374.49 | Next four |
| vilsa-camp-ground | 1 | 1 / 0 | [0.9, 0.9] | 3844.73 | Next four |
| village-square-foundation | 3 | 3 / 5 | [0.3, 0.82] | 2406.88 | Assigned separately; now bound after snapshot |
| supply-tent-workyard | 5 | 1 / 0 | [0.7, 0.7] | 2316.33 | Next four |
| abandoned-cart-stop | 13 | 0 / 4 | [0.302, 0.357] | 1870.25 | Already new at this snapshot |
| gray-camp-ground | 0 | 1 / 0 | [0.9, 0.9] | 1273.41 | Next four |
| watch-post-ground | 11 | 2 / 4 | [0.3, 0.3] | 134.45 | Low visible impact; defer |

Recommended unassigned batch: field-tent-cluster, vilsa-camp-ground, supply-tent-workyard, gray-camp-ground. All four occur in C01-01. Watch-post-ground has six calls but only 134.45 visible alpha-weighted px², about 2.73% of its source area; call count alone would over-prioritize it.

Gray and Vilsa camps remain authored foundation pieces (scale0.9). Field-tent-cluster is standing (one authored0.72 and one procedural0.419); supply-tent-workyard is standing (authored0.7). Keep original gray versus red-brown banner identities. None of these four complete subjects crosses its source quarter. The tower painted in watch-post-ground is currently consumed as a building foundation, not an independent rule tower.

## Generator / exporter contracts

- Cliffs: `cliff-modules-temperate`, 4×4, logical128×128 / HD256×256, modules, original anchor(64,127). Keep whole broad top planes and connected cliff mass. Existing adapter supports exact semantic IDs and fixed slot pairing
- Camps: `camps-foundations`, 4×4, logical144×144 / HD288×288, modules, original anchor(72,139). Retain each consumer’s current foundation/standing layer; there is no new general contract to add
- New native RGBA sources need an explicitly reviewed sourceFoot, including asymmetry. Fit the complete source canvas with one scale and origin. Project anchor=origin+scale×sourceFoot; do not force an old anchor by clipping the new canvas. Do not infer feet from a tight alpha box
- Keep every untouched slot byte-identical, semantic footprint metadata intact, and all gameplay/navigation/trigger/cell placement unchanged

## Rural status

All16 rural-life slots are registered. These two scenes use 13 distinct rural IDs in 31 instances, all new-source. There is no unregistered active rural slot to add to this next batch. The full per-ID inventory is in active-source-inventory.json.

## Evidence and reproduction

- both-complete-scenes-labeled.png: exact full source-image extents, no viewport clipping
- c01-01/ and c01-02/: complete actual scene PNGs and source-hashed production provenance
- north/east-cliff-context and gray/vilsa-camp-context: exact same-camera 2× nearest crops
- references/: full native subject references, keyed references, and old runtime cells
- next-source-contract.json: exact IDs, cell boxes, source SHA256s, original anchors, integrity findings
- active-source-inventory.json: all measured47 cliff/camp instances, draw index, scale, source area, visible area; also world modular-component inventory
- Reproduce in a clean654b382 checkout using existing review_gray_flag_scene.ts and review_standing_depth.ts with STANDING_REVIEW_OUTPUT set to separate c01-01/c01-02 folders, then run audit_active_sources.py with EMPIRE_REPO and AUDIT_OUTPUT environment variables

The first measurement attempt was discarded because the integration atlas changed while masks were rendering. This package is the rerun in an isolated immutable checkout; all recorded source hashes verified unchanged after rendering. Temporary masks and frozen full atlases are deliberately excluded. No production edits, new adapter, rule changes, or remote push were made for this audit.
