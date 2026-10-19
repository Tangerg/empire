"""The same byte/geometry guarantees apply to both registered Blob47 consumers."""
import test_forest_masks


class EarthMaskTests(test_forest_masks.ForestMaskTests):
    atlas_id = 'transition-meadow-earth'
    material_name = 'earth'
