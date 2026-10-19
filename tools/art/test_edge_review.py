import json
from pathlib import Path
import shutil
import tempfile
import unittest
import xml.etree.ElementTree as ET
from PIL import Image
from export_overrides import ROOT
from review_edge_decoration import compare_edge_crop, validate_edges

EVIDENCE = ROOT / 'docs/art-rebuild-20261019/approved-v5/edge-runtime'


class EdgeReviewTests(unittest.TestCase):
    def setUp(self):
        self.provenance = json.loads((EVIDENCE / 'runtime-provenance.json').read_text())

    def test_actual_native_road_bridge_foot_and_near_water_masks(self):
        result = validate_edges(EVIDENCE, self.provenance)
        self.assertEqual(result['protectedInitialActorCount'], 20)
        for check in result['checks']:
            self.assertTrue(all(n == 0 for n in check['maximumAlphaInProtectedAreas'].values()))

    def test_safety_guard_rejects_each_deliberate_overlap(self):
        with tempfile.TemporaryDirectory() as temporary:
            temporary = Path(temporary)
            for name in ('field-full', 'bank-only-full', 'road-core-full'):
                for density in (1, 2):
                    shutil.copyfile(EVIDENCE / f'{name}@{density}x.png', temporary / f'{name}@{density}x.png')
            with Image.open(temporary / 'road-core-full@1x.png') as road:
                index = next(i for i, value in enumerate(road.getchannel('A').tobytes()) if value == 255)
                road_point = (index % road.width, index // road.width)
            bx, by = self.provenance['edgeSafety']['bridgeCells'][0]
            feet = tuple(self.provenance['edgeSafety']['protectedInitialFeet'][0])
            for name, point in [('field-full', road_point), ('field-full', (bx * 32 + 16, by * 32 + 16)),
                                ('field-full', feet), ('bank-only-full', (352, 16))]:
                with self.subTest(name=name, point=point):
                    path = temporary / f'{name}@1x.png'
                    before = path.read_bytes()
                    with Image.open(path) as current:
                        changed = current.convert('RGBA')
                    changed.putpixel(point, (50, 60, 70, 255))
                    changed.save(path)
                    with self.assertRaisesRegex(ValueError, 'safety overlap'):
                        validate_edges(temporary, self.provenance)
                    path.write_bytes(before)

    def test_actual_fractional_crops_and_rejected_coordinate_shift(self):
        x, y, w, h = self.provenance['cropWorldBox']
        for density in (1, 2):
            with Image.open(EVIDENCE / f'field-full@{density}x.png') as full, Image.open(EVIDENCE / f'field-seam-crop@{density}x.png') as crop:
                expected = full.convert('RGBA').crop(tuple(n * density for n in (x, y, x + w, y + h)))
                actual = crop.convert('RGBA')
            result = compare_edge_crop(expected, actual)
            self.assertLessEqual(result['maximumAlphaDifference'], 2)
            self.assertLessEqual(result['maximumPremultipliedColorDifference'], 2)
            shifted = Image.new('RGBA', actual.size)
            shifted.paste(actual, (1, 0))
            with self.assertRaisesRegex(ValueError, 'crop exceeded'):
                compare_edge_crop(expected, shifted)

    def test_complete_production_comparison_changes_only_ground_edges(self):
        directory = EVIDENCE / 'production'
        provenance = json.loads((directory / 'runtime-scene-provenance.json').read_text())
        before, after = provenance['renders']
        for key in ('viewport', 'actors', 'standingDrawOrder', 'standingSources', 'stateSha256'):
            self.assertEqual(before[key], after[key], key)
        self.assertEqual(before['viewport'], [0, 0, 852, 584])
        self.assertEqual([item['edgeDecorationCount'] for item in (before, after)], [0, 14])
        for item in (before, after):
            self.assertEqual(item['unitCount'], 20)
            self.assertEqual(item['buildingCount'], 9)
            for density in (1, 2):
                with Image.open(directory / item['filename'].replace('.svg', f'@{density}x.png')) as raster:
                    self.assertEqual(raster.size, (852 * density, 584 * density))
        without, with_edges = [ET.parse(directory / item['filename']).getroot() for item in (before, after)]
        removed = 0
        for layer in with_edges.iter():
            if 'layer-ground' not in layer.get('class', '').split():
                continue
            for piece in list(layer):
                if any('candidate-ground-edge' in descendant.get('class', '').split() for descendant in piece.iter()):
                    layer.remove(piece)
                    removed += 1
        self.assertEqual(removed, 14)
        self.assertEqual(ET.tostring(without), ET.tostring(with_edges))


if __name__ == '__main__':
    unittest.main()
