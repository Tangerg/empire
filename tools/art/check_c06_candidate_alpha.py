"""Read-only C06 candidate alpha regression. Not runtime/gameplay acceptance."""
from pathlib import Path
import json
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2] / 'godot'
ART = ROOT / 'assets/native/c06-layered-v1'
REPORT = ROOT / 'docs/art-provenance/c06-layered-v1'


def check():
    old = np.array(Image.open(ART / 'red-cottage-dual-door-night-v2.png'))
    new = np.array(Image.open(ART / 'red-cottage-dual-door-alpha-v3.png'))
    assert old.shape == new.shape == (1024, 1536, 4)
    proof = json.loads((REPORT / 'southwest-house-alpha-regression.json').read_text())
    negatives = [row['pixel'] for row in proof['negativeClearSamples']]
    assert all(new[y, x, 3] == 0 for x, y in negatives), 'background haze returned'
    assert sum(old[y, x, 3] > 16 for x, y in negatives) >= 6, 'old-art negative control lost'
    feet = np.array([[280, 799], [795, 988], [1340, 803]])
    assert all(new[y, x, 3] >= 200 for x, y in feet), 'real masonry feet lost'
    historical = json.loads((REPORT / 'final-six-material-fits.json').read_text())
    case = next(row for row in historical['cases'] if row['objectId'] == 'southwest-red-house')
    transform = case['feetAndCompleteLowerOutline']
    scale, shift = transform['sourceScale'], np.array(transform['sourceTranslation'])
    polygon = np.array(case['originalGroundPolygonPixels'])
    assert np.abs(feet * scale + shift - polygon[[-1, -2, -3]]).max() <= 5
    # Original polygon is convex. Each outward-normal support bounds the whole
    # real lower material chain, including both doorstep projections.
    edges = np.roll(polygon, -1, axis=0) - polygon
    normals = np.concatenate([np.column_stack([edges[:, 1], -edges[:, 0]]), [[1, 0], [-1, 0], [0, 1], [0, -1]]])
    supports = np.array([max(polygon @ normal) for normal in normals])
    columns = np.where((new[700:, :, 3] >= 200).any(axis=0))[0]
    chain = np.array([[x, np.where(new[:, x, 3] >= 200)[0][-1]] for x in columns])
    outside = np.maximum(0, ((chain * scale + shift) @ normals.T - supports) / np.abs(normals).sum(axis=1)).max()
    assert outside <= 5, 'complete lower masonry exceeds unchanged envelope'
    for stem in ['c01-06-houses-preview', 'c01-06-assembly-preview']:
        ground = json.loads((ROOT / 'data/scenes' / (stem + '-ground.json')).read_text())
        house = next(row for row in ground['objects'] if row['id'] == 'southwest-red-house')
        assert house['assetId'] == 'red-cottage-dual-door-alpha-v3'
        assert house['scale'] == transform['instanceWorldScale']
        assert np.max(np.abs(np.array(house['contact']) - transform['fractionalContact'])) < 1e-12
    print(f'C06 candidate alpha: {len(negatives)} clear-background samples; old negative control; 3 real feet; {len(chain)} lower-edge points, max outside {outside:.6f}px. Placement unchanged. NOT gameplay/full wall acceptance.')


if __name__ == '__main__':
    check()
