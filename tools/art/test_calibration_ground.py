import tempfile
import unittest
from pathlib import Path
from PIL import Image
from calibration_ground import append_ground, element, NS


class GroundStudyTests(unittest.TestCase):
    def test_world_period_protected_cells_and_retained_canvas(self):
        with tempfile.TemporaryDirectory() as directory:
            base, patch = Path(directory)/'base.png', Path(directory)/'patch.png'
            Image.new('RGB', (16,16), (80,90,55)).save(base)
            image = Image.new('RGBA', (20,20))
            image.paste((40,70,30,255), (4,5,16,15)); image.save(patch)
            svg, defs = element('svg', {}), element('defs', {})
            map_data = {'width': 2, 'height': 2, 'tiles': ['plain','water','road','bridge']}
            report = append_ground(svg, defs, map_data, base, patch)
            self.assertEqual(report['protectedWaterBridgeCells'], [[1,0],[1,1]])
            self.assertEqual(report['worldPeriodLogical'], 128)
            self.assertEqual(len(report['patches']), 2)
            self.assertEqual(report['patches'][0]['visibleCenterWorld'], [128,176])
            self.assertGreater(report['patches'][0]['fullCanvasWorld'][2], 160)
            clip = defs.find(f'{{{NS}}}clipPath')
            self.assertEqual(len(clip), 2)
            self.assertEqual([child.get('x') for child in clip], ['0','0'])
            self.assertEqual(map_data['tiles'], ['plain','water','road','bridge'])

    def test_rejects_transparent_base_and_opaque_patch(self):
        with tempfile.TemporaryDirectory() as directory:
            base, patch = Path(directory)/'base.png', Path(directory)/'patch.png'
            Image.new('RGBA',(4,4),(0,0,0,0)).save(base)
            data={'width':1,'height':1,'tiles':['plain']}
            with self.assertRaises(ValueError):
                append_ground(element('svg',{}),element('defs',{}),data,base)
            Image.new('RGB',(4,4)).save(base); Image.new('RGB',(4,4)).save(patch)
            with self.assertRaises(ValueError):
                append_ground(element('svg',{}),element('defs',{}),data,base,patch)


if __name__ == '__main__': unittest.main()
