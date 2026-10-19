# stone-bridge-vertical, crossings-fortifications index 3

## Source identity
One narrow straight stone bridge, long travel axis exactly vertical in the image, with a continuous pale-gray paved deck of broad rectangular blocks. Four larger square stone corner piers, two at each landing, connect to two LOW side parapets. Top and bottom travel entrances remain OPEN with visible landing lips. Preserve the short near-end masonry thickness and small dark open space below the bottom deck lip. No gatehouse, door, high arch across the walking path, flags, water or terrain base.

Original caveat: Original body complete and separated from adjacent source slots; no neighbor fragment in the runtime cell. Do not interpret the small under-deck opening as a gate across the walkable deck.

Native review: Complete north-south stone deck, four large corner piers and two low parapets preserved. Both travel landings remain visible as low broad stone lips; no door, gatehouse or wall rises across the path. Short underside thickness and the open gap below the near deck lip remain. Broad matte gray blocks replace the original speckled microtexture. No ground/water tile or rotated travel axis.

## Deck registration and actual consumer
The travel axis is north-south, top to bottom in the image. SourceFoot [627, 594] is the independently inspected DECK CENTER / traversal contact, not the bottom of the support silhouette. Separate visual landing/support landmarks: {"north_landing_center": [627, 202], "south_landing_center": [627, 986], "north_deck_left_right_x": [527, 727], "south_deck_left_right_x": [506, 752], "near_left_support_base_contact": [419, 1118], "near_right_support_base_contact": [836, 1118], "near_under_deck_opening_center": [627, 1079]}. These are approximate visual registration measurements, not alpha bbox statistics or a guarantee of exact technical ports.
Original atlas contract is96logical/192HD, default anchor(48,91), footprint [2, 3], obstruction 1. Current runtime calls these IDs with lying:true and pivots at atlas center(48,48); no code changed here. c01-12 uses two wood5-cell runs and two stone3-cell runs at their run midpoints. Full contract and exact consumer references are saved in vertical-bridge-source-contract.json. A5-cell run is160logicalpx, so the old96cell art cannot certify full run coverage. Engineering must review native deck center and landing span separately; no gameplay change, arbitrary stretching or per-variant bbox fitting is implied.

## QA and provenance
1254RGBA native, raw and master byte-identical. Alpha residue at1 permitted unchanged;alpha2+ on perimeter rejected. Exact complete original subject and style reference inspected. No source rotation, mirroring, source-slot fragment imitation, ground slab, water tile or gate across the deck. No programmatic paint/alpha cleanup. No repository edits. Horizontal approved bridge is independent and unchanged.


Native output and alpha preserved byte-for-byte. Source foot is a visual estimate. Strong-alpha LTRB margins: [331, 117, 327, 130]. Strict perimeter alpha-zero check: FAIL, faint native edge alpha retained and explicitly recorded. No programmatic paint, color changes or alpha cleanup. Runtime scale, padding, anchoring and gameplay-size validation remain for parent integration.

Current source perimeter gate passes: native alpha-1 residue is retained unchanged; alpha 2 or greater would fail.
