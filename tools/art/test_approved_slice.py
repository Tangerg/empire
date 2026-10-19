import unittest
from PIL import Image
from assemble_approved_slice import alpha_report
from export_transition_overrides import replace_mask_material


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

    def test_material_replacement_preserves_all_masks_and_unused_cell(self):
        baseline=Image.new('RGBA',(512,96),(30,40,50,77))
        baseline.putpixel((12,13),(10,20,30,0))
        result,_,_=replace_mask_material(baseline,Image.new('RGB',(128,128),(90,100,110)),32)
        self.assertEqual(result.getchannel('A').tobytes(),baseline.getchannel('A').tobytes())
        self.assertEqual(result.crop((480,64,512,96)).tobytes(),baseline.crop((480,64,512,96)).tobytes())
        self.assertEqual(result.getpixel((0,0)),(90,100,110,77))

    def test_transparent_terrain_material_rejected(self):
        with self.assertRaises(ValueError):
            replace_mask_material(Image.new('RGBA',(512,96)),Image.new('RGBA',(128,128)),32)


if __name__ == '__main__': unittest.main()
