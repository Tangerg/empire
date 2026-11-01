#!/usr/bin/env python3
"""Frozen physical tree witnesses; native depth/picking remains a separate gate."""
import json
from pathlib import Path
from PIL import Image
ROOT = Path(__file__).resolve().parents[2] / 'godot'
DOCS = ROOT / 'docs/art-provenance/c06-layered-v1/foreground-depth-audit'

def main():
    ground = json.loads((ROOT / 'data/scenes/c01-06-assembly-preview-ground.json').read_text())
    catalog = json.loads((ROOT / 'data/scenes/c01-06-assembly-preview-objects.json').read_text())
    reviewed = json.loads((DOCS / 'material-registration.json').read_text())
    results = []
    for case in reviewed['cases']:
        identity = 'northwest-bare-tree-' + case['id']
        instances = [o for o in ground['objects'] if o['id'] == identity]
        assert len(instances) == 1, identity
        obj = instances[0]
        assert obj['pointer'] == 'pass-through' and obj['selectionCell'] is None
        assert obj['ownsMapStructureBody'] is False
        asset = catalog['objects'][obj['assetId']]
        assert asset['anchorPixels'] == case['rootBaseAnchorPixels']
        assert asset['groundFrontPixels'] == case['frontmostRootGroundFrontPixels']
        assert abs(obj['scale'] / ground['worldScale'] - case['suggestedSourceScale']) < 1e-9
        image = Image.open(ROOT / asset['resourcePath'].removeprefix('res://')).convert('RGBA')
        assert list(image.size) == case['actualSize'] == asset['pixelSize']
        alpha = [image.getpixel(tuple(p))[3] for p in [asset['anchorPixels'], asset['groundFrontPixels']]]
        assert all(a >= 240 for a in alpha), (identity, alpha)
        target = [20 + 32 * obj['contact'][0], 30 + 28 * obj['contact'][1]]
        assert all(abs(a-b) < 1e-8 for a,b in zip(target, case['sourceRootBaseTarget']))
        front_y = target[1] + (asset['groundFrontPixels'][1] - asset['anchorPixels'][1]) * case['suggestedSourceScale']
        assert 156 < front_y < 212, (identity, front_y)
        results.append({'id': identity, 'physicalRootAlpha': alpha, 'frontSourceY': front_y})
    scene = json.loads((ROOT / 'data/scenes/c01-06-assembly-preview.json').read_text())
    assert scene['runtimeReady'] is False
    assert json.loads((ROOT / 'data/scenes/c01-06.json').read_text()).get('assembledGroundPath') is None
    print(json.dumps({'trees': results, 'scope': 'physical registration only; native-probe separately tests body alpha, front/back depth and forced-background negative controls'}))
if __name__ == '__main__':
    main()
