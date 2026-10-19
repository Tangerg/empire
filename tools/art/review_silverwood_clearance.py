"""Validate the complete before/after Silverwood capture and write its labeled board.

Pass the evidence directory containing before/, after/, two contract JSON files
and two compressed replay streams. Rerender each raw occlusion.json with
review_scene_actor_clearance.py when changing scene geometry or source art.
"""
from pathlib import Path
import argparse
import gzip
import hashlib
import json
import xml.etree.ElementTree as ET

NS = '{http://www.w3.org/2000/svg}'
REMAINING = {'controlled-longbow-1', 'controlled-longbow-2', 'campaign-tasha'}


def read(path):
    return json.loads(path.read_text())


def review(directory):
    before = read(directory / 'before/runtime-scene-provenance.json')
    after = read(directory / 'after/runtime-scene-provenance.json')
    a, b = before['renders'][0], after['renders'][0]
    assert a['actors'] == b['actors'], 'actor identity, source or deployment changed'
    assert a['stateSha256'] == b['stateSha256'], 'battle state changed'
    assert a['viewport'] == b['viewport'] == [0, 0, 916, 648]
    assert a['unitCount'] == b['unitCount'] == 22
    assert a['buildingCount'] == b['buildingCount'] == 8
    png = lambda p: {k: v for k, v in p['sources'].items() if k.endswith('.png')}
    assert png(before) == png(after), 'source PNG snapshot differs'
    unchanged_drawings = []
    trees = [ET.parse(directory / name / 'runtime-standing-full.svg').getroot() for name in ['before', 'after']]
    ordered = []
    for root, metadata in zip(trees, [a, b]):
        standing = root.find(NS + 'g[@class="board-world"]').find(NS + 'g[@class="layer layer-units"]')
        ordered.append({piece['id']: ET.tostring(node) for piece, node in zip(metadata['standingDrawOrder'], standing)})
    for name in ordered[0]:
        if not name.startswith('scenery:'):
            assert ordered[0][name] == ordered[1][name], f'rule-owned drawing changed: {name}'
            unchanged_drawings.append(name)
    frames = [next(g for g in root.iter(NS + 'g') if g.get('class') == 'candidate-scene-foreground') for root in trees]
    assert ET.tostring(frames[0]) == ET.tostring(frames[1]), 'outer frame changed'
    contracts = [read(directory / (name + '-contract.json')) for name in ['before', 'after']]
    changes = []
    for old, new in zip(*contracts):
        fields = [key for key in old if old[key] != new[key]]
        if fields:
            changes.append({'level': old['id'], 'fields': fields})
    assert changes == [{'level': 'c01-15', 'fields': ['underUnitsSha256']}]
    replays = [gzip.decompress((directory / (name + '-replays.txt.gz')).read_bytes()) for name in ['before', 'after']]
    assert replays[0] == replays[1], 'event stream or outcome changed'
    assert replays[0].count(b'### ') == 60
    old_actors = {v['key']: v for v in read(directory / 'before/occlusion.json')['actors']}
    new_actors = {v['key']: v for v in read(directory / 'after/occlusion.json')['actors']}
    assert old_actors.keys() == new_actors.keys() and len(new_actors) == 22
    comparison = []
    for key, old in old_actors.items():
        new = new_actors[key]
        assert new['cell'] == old['cell']
        assert new['spriteArea'] == old['spriteArea'], f'source sprite differs: {key}'
        assert new['occludedFraction'] <= old['occludedFraction'] + .000001, f'clearance worsened: {key}'
        ceiling = .71 if key in REMAINING else .30
        assert new['occludedFraction'] <= ceiling, f'clearance regression: {key}'
        comparison.append({'key': key, 'cell': old['cell'], 'before': old['occludedFraction'],
                           'after': new['occludedFraction'], 'remainingRuleBuildingOverlap': key in REMAINING})
    report = {
        'kind': 'same-camera complete actual BoardView offline comparison; no live GPU claim',
        'baseline': before['checkpoint'], 'candidate': after['checkpoint'],
        'unchangedPngSources': png(before), 'unchangedRuleDrawings': unchanged_drawings,
        'initialStateSha256': a['stateSha256'], 'viewport': a['viewport'],
        'unchangedOuterFrame': True, 'sceneContractCount': len(contracts[0]), 'sceneContractChanges': changes,
        'replayRunCount': 60, 'replaySha256': hashlib.sha256(replays[0]).hexdigest(),
        'occlusion': comparison,
        'limitations': ['Raw sprite alpha overlap omits CSS shadow filters and is not a perceptual score',
                       'Three actors remain partly hidden by unchanged rule buildings',
                       'Old mixed decoration and actor sources remain; not overall art completion',
                       'Sparse checkout used support assets; source PNG hashes bind this comparison'],
    }
    (directory / 'review.json').write_text(json.dumps(report, indent=2) + '\n')
    panel = '''<svg xmlns="http://www.w3.org/2000/svg" width="1832" height="744" viewBox="0 0 1832 744">
<rect width="1832" height="744" fill="#18231d"/>
<g fill="#f4eedb" font-family="sans-serif"><text x="20" y="28" font-size="20">Silverwood c01-15 · complete actual BoardView · same camera and source PNGs</text>
<text x="20" y="59" font-size="17">BEFORE: procedural woodland edge canopies</text>
<text x="936" y="59" font-size="17">AFTER: 26 local understory replacements + 2 slope contact shifts</text></g>
<image href="before/runtime-standing-full@1x.png" x="0" y="72" width="916" height="648"/>
<image href="after/runtime-standing-full@1x.png" x="916" y="72" width="916" height="648"/>
<text x="20" y="739" fill="#f4eedb" font-family="sans-serif" font-size="14">22 actors, 8 terrain buildings and mother-root preserved · 3 actors still overlap rule buildings · offline SVG evidence, not live GPU</text></svg>'''
    (directory / 'full-before-after.svg').write_text(panel)
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('evidence_directory', type=Path)
    args = parser.parse_args()
    report = review(args.evidence_directory)
    print(f"passed: {len(report['unchangedPngSources'])} unchanged PNG sources, 16 scenes, 60 replay streams, 22 actors")
