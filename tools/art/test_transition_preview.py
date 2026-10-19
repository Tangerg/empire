import unittest
from preview_transition import island_mask


class TransitionPreviewTests(unittest.TestCase):
    def test_center_and_corner_masks_use_manifest_bit_order(self):
        self.assertEqual(island_mask(2, 2), 255)
        self.assertEqual(island_mask(1, 1), 28)
        self.assertEqual(island_mask(3, 3), 193)
        self.assertEqual(island_mask(2, 1), 124)
