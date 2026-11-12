# Torren eight-pose walking cycle

The built-in image generator used the existing `torren-stage01-v2.png` as the identity/style reference. The requested action was a full alternating two-step walk, not a run or attack, with fixed sword/shield sides and a transparent background. The first candidate was rejected for a translucent grey backdrop and repeated same-leg support. A second edit corrected transparency and the opposite support leg; a final targeted edit corrected pose 4's left-foot forward swing and pose 8's right-foot pre-contact. `source-sheet.png` is the final generated original, not a resized or hand-painted reconstruction.

The eight reviewed poses are contact, compression, passing and swing for each leg, in reading order. Extraction retains original RGBA pixels around the eight substantial alpha components and adds transparent packing space. Only 22,918 detached pixels of maximum alpha 8/255 are discarded; `verify-extraction.py` reproduces and verifies every retained pixel, source hash, runtime hash, region and anchor. No limbs or torso pixels are composited, rescaled or warped.

`selected-assets.json` remains the sole live owner of frame geometry and registration. The frozen extraction review is evidence, not a second runtime catalog. Uniform scale is 0.1. Anchors are registered from the belt/pelvis projection relative to the existing idle reference, not independently snapped to the lowest boot; swing feet may extend beyond the root. These visually estimated landmarks carry several source pixels of uncertainty and require live idle/move/attack handoff review.

Only `campaign-torren` selects `torren.walk`. The eight quarter-cell intervals preserve the original two-cell cycle and existing movement speed. Other units and named actors retain `legacy.move`. The canonical move, duration, logical contact, event stream, collision and depth ownership are unchanged.

Native regression and desktop acceptance results are recorded after running them; source inspection alone does not prove motion quality.
