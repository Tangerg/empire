"""Pure alpha/order guards for the offline scene-clearance measurement."""
import unittest
import numpy as np
from review_scene_actor_clearance import later_overlaps


class SceneActorClearanceTests(unittest.TestCase):
    def test_opaque_front_piece_and_world_offsets(self):
        order = [{"id": "actor", "cropOrigin": [10, 20]}, {"id": "tree", "cropOrigin": [11, 20]}]
        result = later_overlaps(0, order, [np.ones((2, 2)), np.ones((2, 2))])
        self.assertEqual(result["occludedFraction"], .5)
        self.assertEqual(result["covering"][0]["pairwiseOverlap"], .5)

    def test_only_later_objects_count_and_semitransparent_cover_is_unioned(self):
        order = [{"id": str(i), "cropOrigin": [0, 0]} for i in range(4)]
        masks = [np.ones((2, 2)), np.ones((2, 2)), np.full((2, 2), .5), np.full((2, 2), .5)]
        result = later_overlaps(1, order, masks)
        self.assertEqual(result["occludedFraction"], .75)
        self.assertEqual([piece["id"] for piece in result["covering"]], ["2", "3"])

    def test_actor_transparency_is_weighted_and_disjoint_pieces_do_not_cover(self):
        order = [{"id": "actor", "cropOrigin": [0, 0]}, {"id": "tree", "cropOrigin": [0, 0]},
                 {"id": "far-tree", "cropOrigin": [100, 100]}]
        result = later_overlaps(0, order, [np.array([[1., .5]]), np.array([[0., 1.]]), np.ones((2, 2))])
        self.assertEqual(result["occludedFraction"], .333333)
        self.assertEqual(len(result["covering"]), 1)

    def test_empty_actor_is_an_error(self):
        with self.assertRaisesRegex(ValueError, "empty actor sprite"):
            later_overlaps(0, [{"id": "actor", "cropOrigin": [0, 0]}], [np.zeros((2, 2))])


if __name__ == "__main__":
    unittest.main()
