import unittest
from PIL import Image
from assemble_approved_slice import alpha_report


class ApprovedCutoutTests(unittest.TestCase):
    def fixture(self):
        image = Image.new('RGBA',(8,8))
        image.paste((90,80,70,253),(2,2,6,6))
        return image

    def test_true_alpha_and_visible_bounds(self):
        result = alpha_report(self.fixture())
        self.assertEqual(result['visibleBounds'],(2,2,6,6))
        self.assertEqual(result['transparentPixels'],48)

    def test_alpha_one_is_preserved(self):
        image = self.fixture(); image.putpixel((0,0),(1,2,3,1))
        self.assertEqual(alpha_report(image)['edgeMax'],1)
        self.assertEqual(image.getpixel((0,0)),(1,2,3,1))

    def test_visible_edge_is_rejected(self):
        image = self.fixture(); image.putpixel((0,0),(1,2,3,2))
        with self.assertRaises(ValueError): alpha_report(image)

    def test_rgb_rejected(self):
        with self.assertRaises(ValueError): alpha_report(self.fixture().convert('RGB'))

    def test_opaque_canvas_rejected(self):
        with self.assertRaises(ValueError): alpha_report(Image.new('RGBA',(8,8),(1,2,3,255)))


if __name__ == '__main__': unittest.main()
