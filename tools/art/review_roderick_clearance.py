"""Verify the complete same-source, three-decoration Roderick comparison.

Usage: python tools/art/review_roderick_clearance.py EVIDENCE_DIRECTORY
Requires complete before-03/10 and after-03/10 captures, all four alpha reports,
16-scene contracts and the 60-run replay streams. No production changes.
"""
from pathlib import Path
from collections import Counter
import argparse
import gzip
import hashlib
import json
import xml.etree.ElementTree as ET

N = '{http://www.w3.org/2000/svg}'
SPECS = {
    '03': [('mixed-forest-autumn', 'fern-bed', [80.18315, 197.76], [80.18315, 197.76])],
    '10': [('oak-ancient', 'fern-bed', [561.13346, 261.76], [561.13346, 261.76]),
           ('temperate-low-ledge', 'temperate-low-ledge', [623.7925, 229.76], [623.7925, 216])],
}


def verify_roderick_clearance(directory):
    def read(path):
        return path.read_bytes() if path.exists() else gzip.decompress(Path(str(path) + '.gz').read_bytes())

    def data(path):
        return json.loads(read(path))

    comparisons = []
    for code, specs in SPECS.items():
        before, after = [data(directory / f'{phase}-{code}' / 'runtime-scene-provenance.json') for phase in ['before', 'after']]
        png = lambda p: {k: v for k, v in p['sources'].items() if k.endswith('.png')}
        assert png(before) == png(after), 'art snapshot changed'
        old, new = before['renders'][0], after['renders'][0]
        for field in ['actors', 'stateSha256', 'viewport', 'unitCount', 'buildingCount']:
            assert old[field] == new[field], (code, field)
        assert (old['unitCount'], old['buildingCount']) == ((19, 9) if code == '03' else (21, 9))
        roots = [ET.fromstring(read(directory / f'{phase}-{code}' / 'runtime-standing-full.svg')) for phase in ['before', 'after']]
        rule_drawings, scenery, frames = [], [], []
        for side, (root, metadata) in enumerate(zip(roots, [old, new])):
            # Frozen archive relocation may use a different relative prefix; identity is the PNG filename.
            for image in root.iter(N + 'image'):
                href = image.get('href')
                if href and not href.startswith('data:'):
                    image.set('href', Path(href).name)
            layer = root.find(N + 'g[@class="board-world"]').find(N + 'g[@class="layer layer-units"]')
            drawings = dict(zip([piece['id'] for piece in metadata['standingDrawOrder']], list(layer)))
            rule_drawings.append({key: ET.tostring(value) for key, value in drawings.items() if not key.startswith('scenery:')})
            changed = set()
            for spec in specs:
                cell_id, foot = spec[side], spec[side + 2]
                matches = [piece for piece in metadata['namedPieces'] if piece.get('cellId') == cell_id and piece.get('worldFoot') == foot]
                assert len(matches) == 1, (code, spec, side, matches)
                changed.add(matches[0]['sourceId'])
            scenery.append(Counter(ET.tostring(value) for key, value in drawings.items() if key.startswith('scenery:') and key not in changed))
            frames.append(ET.tostring(next(g for g in root.iter(N + 'g') if g.get('class') == 'candidate-scene-foreground')))
        assert rule_drawings[0] == rule_drawings[1], 'actor/building drawing changed'
        assert scenery[0] == scenery[1], 'unrelated scenery changed'
        assert frames[0] == frames[1], 'outer frame changed'
        actors = [{a['key']: a for a in data(directory / f'{phase}-{code}-alpha' / 'occlusion.json')['actors']} for phase in ['before', 'after']]
        assert actors[0].keys() == actors[1].keys()
        changes = []
        for key, a in actors[0].items():
            b = actors[1][key]
            for field in ['cell', 'worldFoot', 'spriteArea']:
                assert a[field] == b[field], (code, key, field)
            assert b['occludedFraction'] <= a['occludedFraction'], (code, key, 'worsened')
            if a['occludedFraction'] != b['occludedFraction']:
                changes.append({'actor': key, 'cell': a['cell'], 'before': a['occludedFraction'], 'after': b['occludedFraction']})
        assert {a['actor'] for a in changes} == ({'roderick'} if code == '03' else {'roderick', 'pass-shield'})
        comparisons.append({'level': 'c01-' + code, 'samePngSources': len(png(before)), 'unchangedRuleDrawings': len(rule_drawings[0]),
                            'actorCount': old['unitCount'], 'buildingCount': old['buildingCount'], 'exactDecorationChanges': len(specs), 'occlusionChanges': changes})
    contracts = [data(directory / f'contract-{phase}.json') for phase in ['before', 'after']]
    assert len(contracts[0]) == len(contracts[1]) == 16
    differences = [{'level': a['id'], 'fields': [key for key in a if a[key] != b[key]]} for a, b in zip(*contracts) if a != b]
    assert differences == [{'level': 'c01-03', 'fields': ['underUnitsSha256']}, {'level': 'c01-10', 'fields': ['underUnitsSha256']}], differences
    replay = [read(directory / f'replay-{phase}.txt') for phase in ['before', 'after']]
    assert replay[0] == replay[1] and replay[0].count(b'### ') == 60, 'replay changed'
    result = {'baseline': '714538b099cbe35f22865fb3b6d97a9743ca0c89', 'kind': 'actual complete offline BoardView/source-alpha comparison; not live GPU',
              'levels': comparisons, 'sceneContractDifferences': differences, 'replayRuns': 60, 'replaySha256': hashlib.sha256(replay[0]).hexdigest(),
              'limitations': ['Initial standing pose only; alpha overlap is not a recognition score',
                             'Complete diagnostic capture uses empty hidden set; visibility is separately checked against actual engine and hidden-overlay backend guards',
                             'Other existing forest, cliff and rule-building occlusions remain unchanged']}
    (directory / 'review.json').write_text(json.dumps(result, indent=2) + '\n')
    print('passed: exact three decoration changes, 40 actor masks, 16 scene contracts and 60 identical replay streams')
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('evidence_directory', type=Path)
    verify_roderick_clearance(parser.parse_args().evidence_directory.resolve())
