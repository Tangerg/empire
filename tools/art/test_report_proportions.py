import unittest
from report_proportions import rectangle_overlap
from render_calibration_scene import PROPORTION_SETS


class ProportionTests(unittest.TestCase):
    def test_overlap_is_visual_rectangle_only(self):
        self.assertEqual(rectangle_overlap([0,0,10,10],[5,5,15,15]),25)
        self.assertEqual(rectangle_overlap([0,0,10,10],[10,0,20,10]),0)
    def test_authorized_sets_are_explicit(self):
        self.assertEqual(set(PROPORTION_SETS),{'S0','P1','H1'})
        self.assertEqual(PROPORTION_SETS['P1'],{'keep':144,'human':28,'pine':76,'road':22})
        self.assertEqual(PROPORTION_SETS['H1']['road'],30)


if __name__=='__main__':unittest.main()
