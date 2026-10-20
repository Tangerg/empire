# Four river battle clearings

Exactly four decorative forest canopies at (19,5), (2,9), (19,9), (2,13) are
replaced with grounded fern/stump vegetation. Their tactical forest terrain,
all 20 actors and 9 buildings, objectives, deployment, rules and movement remain
unchanged. No other standing piece, outer-frame piece or ground is changed.

The complete before/after BoardView screenshots retain the visible remaining
problems. They are offline SVG/Inkscape evidence with production app styles, not
live GPU/browser verification. Full source SVGs and recorded draws are included.

Individual native sprite-alpha overlap improved:
- Torren: 70.9% to 5.4%.
- Bridge cleric: 99.4% to 25.9%.
- Refugee 2: 97.6% to 0.4%; refugee 3: 51.4% to 0%.
- Upper-right shield: 83.3% to 3.0%.
- Laiya: 61.3% to 15.1%; east bow: 45.8% to 0%.

No actor's measured overlap increased. These are alpha overlap measurements,
not a perceptual recognition score; CSS shadows are excluded. Remaining overlap
from the west farmhouse (guard 90%, Elin 63%) and east farmhouse (bow 58%) is
explicitly retained, not hidden or declared solved. The dense rectangular outer
frame and straight river also still need the larger authored-layout pass.

14 focused tests and typecheck passed. The new tests fence exactly four forest
cell replacements and preserve every other standing piece, terrain and state.
Aggregate regression belongs to integration on the final main snapshot.

Reproduction:
SCENE_REVIEW_LEVEL=c01-02 STANDING_REVIEW_OUTPUT=<capture> npx vite-node tools/art/review_gray_flag_scene.ts
python3 tools/art/review_scene_actor_clearance.py <capture> <metrics>
Use render_ground_review.rasterize for the complete runtime-standing-full.svg.
