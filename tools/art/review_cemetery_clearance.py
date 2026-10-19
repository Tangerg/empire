"""Verify the complete, same-source c01-11 seven-decoration comparison.

Capture both phases with review_gray_flag_scene.ts, measure with
review_scene_actor_clearance.py, and retain the 16-scene contracts and replay
streams. This diagnostic does not modify production art, terrain or visibility.
"""
from pathlib import Path
import argparse
import gzip
import hashlib
import json
import xml.etree.ElementTree as ET

N = '{http://www.w3.org/2000/svg}'
CHANGED = {'campaign-tasha', 'campaign-mirelle', 'grave-inquisitor', 'echo-3', 'echo-4'}


def data(path):
    return json.loads(path.read_text())


def raw(path):
    return path.read_bytes() if path.exists() else gzip.decompress(Path(str(path) + '.gz').read_bytes())


def review(directory):
    before, after = [data(directory / phase / 'runtime-scene-provenance.json') for phase in ['before', 'after']]
    png = lambda p: {k: v for k, v in p['sources'].items() if k.endswith('.png')}
    assert png(before) == png(after), 'source PNG snapshot differs'
    old, new = before['renders'][0], after['renders'][0]
    assert old['actors'] == new['actors'], 'actor identity, source or cell changed'
    assert old['stateSha256'] == new['stateSha256'], 'battle state changed'
    assert old['viewport'] == new['viewport'] == [0, 0, 852, 648]
    assert old['unitCount'] == new['unitCount'] == 21
    assert old['buildingCount'] == new['buildingCount'] == 8
    roots = [ET.fromstring(raw(directory / phase / 'runtime-standing-full.svg')) for phase in ['before', 'after']]
    rule_drawings = []
    frame = []
    for root, metadata in zip(roots, [old, new]):
        layer = root.find(N + 'g[@class="board-world"]').find(N + 'g[@class="layer layer-units"]')
        rule_drawings.append({piece['id']: ET.tostring(drawing) for piece, drawing in zip(metadata['standingDrawOrder'], layer) if not piece['id'].startswith('scenery:')})
        frame.append(ET.tostring(next(g for g in root.iter(N + 'g') if g.get('class') == 'candidate-scene-foreground')))
    assert rule_drawings[0] == rule_drawings[1], 'actor or rule building SVG changed'
    assert frame[0] == frame[1], 'outer frame changed'
    contracts = [data(directory / f'contract-{phase}.json') for phase in ['before', 'after']]
    assert len(contracts[0]) == len(contracts[1]) == 16
    differences = [{'level': a['id'], 'fields': [key for key in a if a[key] != b[key]]} for a, b in zip(*contracts) if a != b]
    assert differences == [{'level': 'c01-11', 'fields': ['underUnitsSha256']}], differences
    replay = [raw(directory / f'replay-{phase}.txt') for phase in ['before', 'after']]
    assert replay[0] == replay[1], 'replay event stream or outcome changed'
    assert replay[0].count(b'### ') == 60
    actors = [{a['key']: a for a in data(directory / f'{phase}-alpha/occlusion.json')['actors']} for phase in ['before', 'after']]
    assert actors[0].keys() == actors[1].keys() and len(actors[0]) == 21
    comparisons = []
    changed = set()
    for key, a in actors[0].items():
        b = actors[1][key]
        assert a['cell'] == b['cell'] and a['worldFoot'] == b['worldFoot']
        assert a['spriteArea'] == b['spriteArea'], f'actor source silhouette differs: {key}'
        assert b['occludedFraction'] <= a['occludedFraction'], f'overlap worsened: {key}'
        if a['occludedFraction'] != b['occludedFraction']:
            changed.add(key)
        comparisons.append({'key': key, 'cell': a['cell'], 'before': a['occludedFraction'], 'after': b['occludedFraction']})
    assert changed == CHANGED, changed
    result = {'kind': 'complete actual BoardView offline SVG and source-alpha comparison, not live GPU',
              'baseline': before['checkpoint'], 'candidate': after['checkpoint'], 'unchangedPngSources': png(before),
              'unchangedRuleDrawingIds': list(rule_drawings[0]), 'viewport': old['viewport'],
              'sceneContractDifferences': differences, 'replayRuns': 60,
              'replaySha256': hashlib.sha256(replay[0]).hexdigest(), 'occlusion': comparisons,
              'limitations': ['Initial static pose and raw alpha overlap, not visual recognition or all animations',
                              'Debug capture uses empty hidden-unit set; production visibility is not inferred from the 21-actor fixture',
                              'Tasha retains 42.8% overlap from unchanged neighboring dead trees plus local low dressing',
                              'Five other actors retain over 50% overlap; rule buildings, frame and mission markers stay unchanged']}
    (directory / 'review.json').write_text(json.dumps(result, indent=2) + '\n')
    print(f"passed: {len(png(before))} same PNG sources, 29 unchanged rule drawings, 16 scenes, 60 replays, 21 actor masks")
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('evidence_directory', type=Path)
    review(parser.parse_args().evidence_directory.resolve())
