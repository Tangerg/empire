# watch-keep, landmarks-large index 9

## Source identity and visual review
ONE compact stone watch-keep courtyard. Exactly THREE major ROUND towers in original positions: ONE very tall central/rear round keep, crowned by a low dark slate conical roof inside its crenellated ring; ONE shorter front-left round tower with a dark slate conical roof; and ONE broad lower front-right ROUND OPEN crenellated tower with no roof. Preserve exactly TWO small gray pennants, one on each roofed tower, and one long muted blue-gray vertical banner down the right-facing side of the tall central keep. Connected crenellated curtain walls enclose the small open inner courtyard; their square corner piers are walls, not extra major towers. At the base of the tall keep retain ONE little timber entrance porch with a low slate gable roof. Keep the main closed wooden arched outer gate at front-left between the lower towers and its short stone approach. Retain the separate inner-facing arched opening in the back-right wall. Preserve the narrow stone/rock foundation and courtyard floor. No additional major tower, colorful long faction banners, moat, people or trees.

Source caveats: Main source flag begins y623, four pixels above the old nominal y627 row boundary. Complete crop recovers it. Different from both Lorne and Vilsa castles: one dominant central cylindrical keep with two lower round towers, not their four-tower groupings.

Native review: Three major round towers preserved: dominant tall roofed central keep, lower roofed front-left tower and open foreground round tower. Exactly two gray roof pennants and one long blue-gray central banner. Small timber porch, front outer gate, back-right inner-facing arch and enclosed courtyard retained. Low rounded/square curtain-wall corner turns are wall supports, not another dominant tower. Source flag tip is complete rather than clipped to the old row.

## Original runtime semantics
4x4 atlas, 160 logical / 320 HD cells, anchor (80,155) / (160,310), large-landmark modules, bottom-anchor ySort. Original footprint [4, 4] and obstruction 3 remain metadata, not sprite bounds or new collision geometry. Current src and scene JSON have no exact selector for this ID; c01-01 denies this atlas. Existing formal interactive structure states and bindings remain independent.

## Authored source foot and functional foundation
Source foot [627, 1191]: projected whole-compound baseline at x627,y1191, aligned to the nearest foreground tower foundation; distinct from the lower-left gate stair and internal porch. It is an approximate visual registration estimate, not an alpha-bbox statistic. Physical constituent contacts and internal landmarks are separately estimated: {"foreground_round_tower_foundation_contact": [752, 1196], "front_gate_stair_contact": [374, 1128], "inner_porch_threshold": [672, 824], "back_right_arch_threshold": [908, 812]}. The original enclosed floor, entry steps, terrace or narrow stone support are retained; there is no invented broad terrain slab.
Parent reports its first landmark pair was reviewed with the full 1254-square native canvas scaled by 152/1254 into a 160 cell at origin (4,4). At that same common scale, this source has strong bounds [13.576, 9.939, 149.576, 150.909] and the authored source foot projects to [80.0, 148.364]. This is a calibration report, not a change to the catalog anchor. No tight-alpha fitting, foot normalization, source rotation or mirror was used. Scene placement/scale remains parent review.

## Provenance and QA
Exact original full board and complete subject crop inspected, source/consumer contracts and prompt saved. Source crops are diagnostic only: original fractional 313.5px quarters are never reused as new source coordinates. Native output, raw and master are byte-identical. No programmatic paint, resize, color or alpha cleanup. Requested 100px margin is separately tested; silhouette completeness and perimeter alpha <=1 are the current source gate. No repository edits.


Native output and alpha preserved byte-for-byte. Source foot is a visual estimate. Strong-alpha LTRB margins: [79, 49, 53, 42]. Strict perimeter alpha-zero check: FAIL, faint native edge alpha retained and explicitly recorded. No programmatic paint, color changes or alpha cleanup. Runtime scale, padding, anchoring and gameplay-size validation remain for parent integration.

Current source perimeter gate passes: native alpha-1 residue is retained unchanged; alpha 2 or greater would fail.
