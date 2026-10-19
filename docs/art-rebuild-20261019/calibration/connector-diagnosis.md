# Legacy road and river connector diagnosis

The renderer and atlas agree on NESW bits and `mask * 4 + variant`. This is not the broken positional-topic mapping seen in the ancient campaign.

`environment-builder-v1/tools/build_environment_builder_v1.py:256–273` multiplies the interior mask by noise centered on128, then draws connected ports at alpha255 in five-pixel-deep HD rectangles. `colored_overlay` derives a dark shoulder from this discontinuous mask. The separate road-edge atlas adds another independently warped ring. The visible repeated bars and boxed road ends exist in the atlas cells before rendering.

A new material alone cannot remove this alpha geometry defect. A proposed fix must review connector continuity and shoreline semantics separately from gameplay topology, keep raw masters and original atlases, and prove NESW port compatibility. No connector pixel changes were made in this diagnosis.

## First isolated fix exposed a second geometry issue

The corrected connector alpha removes transverse bars. On c01-02's two-cell-wide river, canonical narrow NESW T shapes still leave unfilled square land holes where four cells meet. Water regions need an area/shore interpretation rather than the narrow road-centerline interpretation. The first calibration preserves this failure as evidence; it is not accepted for production. The new blue material is also more saturated than the restrained scene target.
