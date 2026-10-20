"""Raster proof that actual runtime clips follow terrain, also when baked alone.

Run: python -m unittest discover -s tools/art -p test_spacious_river_contract.py
"""
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET

import numpy as np
from PIL import Image
from render_ground_review import rasterize
from svg_viewports import NS

ROOT = Path(__file__).resolve().parents[2]
N = "{" + NS + "}"


def runtime_cases(directory):
    source = directory / "runtime-shapes.ts"
    imports = {
        "engine": "packages/battle-engine/src/index.ts",
        "catalog": "packages/test-content/src/index.ts",
        "candidate": "packages/story-candidate-01/src/index.ts",
        "shape": "packages/story-candidate-01/src/presentation/candidate-01-playable-shape.ts",
        "scene": "packages/story-candidate-01/src/presentation/candidate-01-map-scene.ts",
        "field": "packages/story-candidate-01/src/presentation/candidate-01-field-material.ts",
        "viewport": "tools/art/candidate_review_viewport.ts",
    }
    paths = {name: json.dumps(str(ROOT / path)) for name, path in imports.items()}
    source.write_text(f"""
import {{ mapFromLevel }} from {paths['engine']};
import {{ createTestCatalog }} from {paths['catalog']};
import {{ CANDIDATE_01_CONTENT_PACK, candidate01Level }} from {paths['candidate']};
import {{ candidate01PlayableShape, candidate01ClipGround }} from {paths['shape']};
import {{ candidate01MapSceneryLayers }} from {paths['scene']};
import {{ candidate01FieldMaterialPieces }} from {paths['field']};
import {{ candidateReviewViewport }} from {paths['viewport']};
const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const map = mapFromLevel(content, candidate01Level('c01-02'));
const syntheticRows = ['_.___', '..._.', '._...', '___._'];
const synthetic = {{ ...map, width: 5, height: 4,
  tiles: syntheticRows.flatMap(row => [...row].map(c => c === '_' ? 'c01.outside' : 'plain')) }};
const result = [map, synthetic].map((map, index) => {{
  const shape = candidate01PlayableShape(content, map);
  const ground = index === 0
    ? candidate01MapSceneryLayers({{ content, map, levelId: 'c01-02', viewport: candidateReviewViewport(map) }}).ground
    : candidate01ClipGround(candidate01FieldMaterialPieces(map.width * 32, map.height * 32), shape, 'probe');
  return {{ width: map.width, height: map.height,
    active: map.tiles.map(id => !content.terrains.get(id).tags.includes('outside')),
    pieces: ground.filter(piece => piece.markup.includes('meadow-base@2x.png')) }};
}});
const water = candidate01MapSceneryLayers({{ content, map, levelId: 'c01-02', viewport: candidateReviewViewport(map) }}).ground
  .filter(piece => piece.markup.includes('data-runtime-raster="area-water"'));
result.push({{ width: map.width, height: map.height,
  active: map.tiles.map(id => id === 'water' || id === 'bridge'), pieces: water }});
console.log(JSON.stringify(result));
""")
    completed = subprocess.run(
        [str(ROOT / "node_modules/.bin/vite-node"), str(source)],
        cwd=ROOT, check=True, text=True, capture_output=True,
    )
    return json.loads(completed.stdout)


def solid_piece(piece):
    wrapper = ET.fromstring(
        f'<svg xmlns="{NS}"><g transform="translate({piece["x"]} {piece["y"]})">'
        f'{piece["markup"]}</g></svg>'
    )
    for image in wrapper.iter(N + "image"):
        image.tag = N + "rect"
        image.attrib.pop("href", None)
        image.attrib.pop("{http://www.w3.org/1999/xlink}href", None)
        image.set("fill", "white")
    return wrapper[0]


def raster_alpha(directory, name, case, pieces, densities, offset=0):
    svg = ET.Element(N + "svg", {
        "width": str(case["width"] * 32),
        "height": str(case["height"] * 32),
        "viewBox": f'{offset} {offset} {case["width"] * 32} {case["height"] * 32}',
    })
    for piece in pieces:
        svg.append(solid_piece(piece))
    path = directory / f"{name}.svg"
    ET.ElementTree(svg).write(path, encoding="unicode")
    rasterize(directory, densities)
    path.unlink()
    result = {}
    for density in densities:
        with Image.open(directory / f"{name}@{density}x.png") as image:
            result[density] = np.array(image.convert("RGBA"))[:, :, 3]
    return result


def assert_terrain_coverage(test, case, alpha, density):
    active = np.array(case["active"], dtype=bool).reshape(case["height"], case["width"])
    pixel_active = np.repeat(np.repeat(active, 32 * density, axis=0), 32 * density, axis=1)
    test.assertEqual(int(np.count_nonzero(alpha[~pixel_active])), 0, "paint leaked into outside terrain")
    centres = alpha[16 * density::32 * density, 16 * density::32 * density]
    test.assertTrue(np.all(centres[active] == 255), "an active terrain cell disappeared from the rendered shape")


class SpaciousRiverContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temporary = tempfile.TemporaryDirectory()
        cls.directory = Path(cls.temporary.name)
        cls.cases = runtime_cases(cls.directory)

    @classmethod
    def tearDownClass(cls):
        cls.temporary.cleanup()

    def test_actual_ground_and_river_cover_owned_centres_and_no_unowned_pixels_at_1x_and_2x(self):
        for index, case in enumerate(self.cases):
            with self.subTest(case=index):
                self.assertTrue(case["pieces"], "actual field pieces were not exported")
                rendered = raster_alpha(self.directory, f"case-{index}", case, case["pieces"], (1, 2))
                for density, alpha in rendered.items():
                    with self.subTest(density=density):
                        assert_terrain_coverage(self, case, alpha, density)

    def test_river_cell_union_stays_opaque_at_camera_scale_and_fractional_pan(self):
        case = self.cases[2]
        alpha = raster_alpha(self.directory, "camera-water", case, case["pieces"], (1.25,), -0.2)[1.25]
        # Straight northern water is one continuous surface, including its old
        # internal cell boundaries. Fractional pan must not create dark tile seams.
        for row in range(1, 5):
            for x in (17.5 * 40, 18 * 40, 18.5 * 40):
                for y in (row * 40, (row + 0.5) * 40):
                    self.assertEqual(int(alpha[round(y), round(x)]), 255)

    def test_each_field_piece_survives_isolated_baking_without_external_definitions(self):
        case = self.cases[0]
        self.assertGreater(len(case["pieces"]), 1, "the enlarged field must exercise translated pieces")
        combined = np.zeros((case["height"] * 32, case["width"] * 32), dtype=np.uint8)
        for index, piece in enumerate(case["pieces"]):
            alpha = raster_alpha(self.directory, f"isolated-{index}", case, [piece], (1,))[1]
            combined = np.maximum(combined, alpha)
        assert_terrain_coverage(self, case, combined, 1)

    def test_coverage_guard_rejects_outside_paint_and_a_missing_active_cell(self):
        case = self.cases[1]
        active = np.array(case["active"], dtype=bool).reshape(case["height"], case["width"])
        correct = np.repeat(np.repeat(active.astype(np.uint8) * 255, 32, axis=0), 32, axis=1)
        assert_terrain_coverage(self, case, correct, 1)
        leaking = correct.copy()
        y, x = np.argwhere(~active)[0]
        leaking[y * 32 + 16, x * 32 + 16] = 255
        with self.assertRaisesRegex(AssertionError, "outside terrain"):
            assert_terrain_coverage(self, case, leaking, 1)
        missing = correct.copy()
        y, x = np.argwhere(active)[0]
        missing[y * 32 + 16, x * 32 + 16] = 0
        with self.assertRaisesRegex(AssertionError, "active terrain cell"):
            assert_terrain_coverage(self, case, missing, 1)

    def test_raster_guard_rejects_removing_the_actual_piece_clip(self):
        case = self.cases[1]
        pieces = []
        for piece in case["pieces"]:
            svg = ET.fromstring(f'<svg xmlns="{NS}">{piece["markup"]}</svg>')
            clipped = [node for node in svg.iter() if "clip-path" in node.attrib]
            self.assertTrue(clipped, "mutation did not find the runtime clip")
            for node in clipped:
                del node.attrib["clip-path"]
            pieces.append({**piece, "markup": "".join(ET.tostring(node, encoding="unicode") for node in svg)})
        alpha = raster_alpha(self.directory, "unclipped-mutation", case, pieces, (1,))[1]
        with self.assertRaisesRegex(AssertionError, "outside terrain"):
            assert_terrain_coverage(self, case, alpha, 1)


if __name__ == "__main__":
    unittest.main()
