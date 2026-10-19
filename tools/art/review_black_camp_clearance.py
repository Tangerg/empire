"""Verify the complete c01-04 one-decoration residual-clearance comparison.

Requires before/after actual BoardView captures, native actor-occlusion reports,
scene JSON snapshots, all 16 scene/standing contracts and 60 replay streams.
"""
from collections import Counter
from pathlib import Path
import argparse
import gzip
import hashlib
import json
import xml.etree.ElementTree as ET

from PIL import Image

N = '{http://www.w3.org/2000/svg}'
CONTACT = [464.29394, 357.76]
ZONE = {'id': 'clearance-14-10', 'bounds': [14, 10, 14, 10],
        'role': 'south-patrol-understory', 'authoredTerrainScenery': ['forest']}
PLACEMENT = {'id': 'fern-bed', 'x': 14.509185625, 'y': 10.18,
             'layer': 'under-units', 'scale': 0.48, 'flip': False}


def verify(directory):
    def read(path):
        file = directory / path
        return file.read_bytes() if file.exists() else gzip.decompress(Path(str(file) + '.gz').read_bytes())

    def data(path):
        return json.loads(read(path))

    scenes = [data(f'scene-{phase}.json') for phase in ['before', 'after']]
    assert scenes[1] == {**scenes[0], 'zones': [*scenes[0]['zones'], ZONE],
                         'placements': [*scenes[0]['placements'], PLACEMENT]}, 'unexpected scene edit'
    assert len(scenes[0]['placements']) == 3, 'prior clearances missing'
    provenance = [data(f'{phase}/runtime-scene-provenance.json') for phase in ['before', 'after']]
    png_sources = [{k: v for k, v in p['sources'].items() if k.endswith('.png')} for p in provenance]
    assert png_sources[0] == png_sources[1], 'art sources changed'
    renders = [p['renders'][0] for p in provenance]
    before, after = renders
    for field in ['actors', 'stateSha256', 'viewport', 'unitCount', 'buildingCount',
                  'sourceLayerCounts', 'displayedLayerCounts', 'edgeDecorationCount']:
        assert before[field] == after[field], field
    assert (before['unitCount'], before['buildingCount']) == (19, 25)
    rule_drawings, scenery, surroundings, replacement = [], [], [], []
    for side, (phase, metadata) in enumerate(zip(['before', 'after'], renders)):
        root = ET.fromstring(read(f'{phase}/runtime-standing-full.svg'))
        width, height = [int(root.get(axis)) for axis in ['width', 'height']]
        assert [0, 0, width, height] == metadata['viewport'] == [0, 0, 788, 584]
        for density in [1, 2]:
            with Image.open(directory / phase / f'runtime-standing-full@{density}x.png') as image:
                assert image.size == (width * density, height * density), 'incomplete full viewport'
        for image in root.iter(N + 'image'):
            href = image.get('href')
            if href and not href.startswith('data:'):
                image.set('href', Path(href).name)
        layer = root.find(N + 'g[@class="board-world"]').find(N + 'g[@class="layer layer-units"]')
        order = metadata['standingDrawOrder']
        assert len(order) == len(layer), 'incomplete recorded standing order'
        drawings = dict(zip([piece['id'] for piece in order], list(layer)))
        changed = [piece for piece in metadata['namedPieces']
                   if piece.get('cellId') == ['oak-ancient', 'fern-bed'][side]
                   and piece.get('worldFoot') == CONTACT]
        assert len(changed) == 1, 'expected one exact canopy/fern contact'
        replacement.append(changed[0])
        rule_drawings.append({key: ET.tostring(value) for key, value in drawings.items()
                              if not key.startswith('scenery:')})
        scenery.append(Counter(ET.tostring(value) for key, value in drawings.items()
                               if key.startswith('scenery:') and key != changed[0]['sourceId']))
        # Compare everything outside the shared standing layer, including terrain
        # surfaces, outer frame and overlays. Source IDs may renumber scenery.
        layer.clear()
        surroundings.append(ET.tostring(root))
    assert rule_drawings[0] == rule_drawings[1], 'rule building or actor drawing changed'
    assert scenery[0] == scenery[1], 'unrelated scenery changed'
    assert surroundings[0] == surroundings[1], 'non-standing scene changed'

    alpha = [data(f'{phase}/actor-occlusion.json') for phase in ['before', 'after']]
    assert all(a['rasterSize'] == [788, 584] and a['actorCount'] == 19 for a in alpha)
    actors = [{a['key']: a for a in report['actors']} for report in alpha]
    assert actors[0].keys() == actors[1].keys()
    comparisons = []
    for key, old in actors[0].items():
        new = actors[1][key]
        for field in ['cell', 'worldFoot', 'sourceSilhouettePixelArea', 'imageSources']:
            assert old[field] == new[field], (key, field)
        assert new['occludedFraction'] <= old['occludedFraction'], (key, 'worse coverage')
        comparisons.append({'actor': key, 'cell': old['cell'], 'before': old['occludedFraction'],
                            'after': new['occludedFraction']})
    changes = [a for a in comparisons if a['before'] != a['after']]
    assert [a['actor'] for a in changes] == ['south-patrol'], changes
    assert actors[1]['south-patrol']['occludedFraction'] < 0.1, 'patrol still obstructed'
    assert actors[0]['south-patrol']['occludedFraction'] > 0.8, 'baseline lost the regression'
    assert actors[0]['scout-1']['occludedFraction'] == actors[1]['scout-1']['occludedFraction']
    assert any('struct-joint-granary@2x.png' in p['imageSources']
               and p['pairwiseSilhouetteOverlapFraction'] > .6
               for p in actors[1]['scout-1']['coveringObjects']), 'rule-granary limitation missing'

    contracts = [data(f'contract-{phase}.json') for phase in ['before', 'after']]
    assert len(contracts[0]) == len(contracts[1]) == 16
    differences = [{'level': a['id'], 'fields': [key for key in a if a[key] != b[key]]}
                   for a, b in zip(*contracts) if a != b]
    assert differences == [{'level': 'c01-04', 'fields': ['underUnitsSha256']}], differences
    standing = [data(f'standing-{phase}.json') for phase in ['before', 'after']]
    assert len(standing[0]) == len(standing[1]) == 16
    standing_differences = [{'level': a['levelId'], 'fields': [key for key in a if a[key] != b[key]]}
                           for a, b in zip(*standing) if a != b]
    assert standing_differences == [{'level': 'c01-04', 'fields': ['svgSha256']}], standing_differences
    replay = [read(f'replay-{phase}.txt') for phase in ['before', 'after']]
    assert replay[0] == replay[1] and replay[0].count(b'### ') == 60, 'replay changed'
    result = {
        'baseline': 'c082e89', 'kind': 'complete actual BoardView, native-display source-alpha comparison',
        'sceneChanges': {'zones[3]': ZONE, 'placements[3]': PLACEMENT},
        'derivedReplacement': dict(zip(['before', 'after'], replacement)),
        'priorThreeClearancesUnchanged': True, 'exactNewDecorationChanges': 1,
        'samePngSourceCount': len(png_sources[0]), 'unchangedRuleDrawings': len(rule_drawings[0]),
        'actorCount': 19, 'buildingCount': 25, 'viewport': before['viewport'],
        'allActorOcclusion': comparisons, 'sceneContractDifferences': differences,
        'standingContractDifferences': standing_differences, 'replayRuns': 60,
        'replaySha256': hashlib.sha256(replay[0]).hexdigest(),
        'limitations': ['Initial-pose offline alpha coverage; not live GPU or a recognition score',
                        'All actors included in diagnostic capture; hidden-unit behavior tested separately',
                        'Scout remains behind rule-owned granary; party actor-to-actor overlap unchanged'],
    }
    (directory / 'review.json').write_text(json.dumps(result, indent=2) + '\n')
    print('passed: one decoration, 19 actor masks, 25 rule buildings, 16 scene/standing contracts, 60 identical replays')
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('evidence_directory', type=Path)
    verify(parser.parse_args().evidence_directory.resolve())
