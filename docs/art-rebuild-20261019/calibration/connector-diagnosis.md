# Legacy road and river connector diagnosis

The renderer and atlas agree on NESW bits and `mask * 4 + variant`. This is not the broken positional-topic mapping seen in the ancient campaign.

`environment-builder-v1/tools/build_environment_builder_v1.py:256–273` multiplies the interior mask by noise centered on128, then draws connected ports at alpha255 in five-pixel-deep HD rectangles. `colored_overlay` derives a dark shoulder from this discontinuous mask. The separate road-edge atlas adds another independently warped ring. The visible repeated bars and boxed road ends exist in the atlas cells before rendering.

A new material alone cannot remove this alpha geometry defect. A proposed fix must review connector continuity and shoreline semantics separately from gameplay topology, keep raw masters and original atlases, and prove NESW port compatibility. No connector pixel changes were made in this diagnosis.
