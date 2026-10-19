"""Publish a reviewed sequence through the existing canonical and HD documents.

This command validates file geometry and semantic order. It does not replace
human native-size, identity, motion or scene-cohesion review.
"""
import argparse
import json
from pathlib import Path
import shutil
from PIL import Image
from assemble_approved_slice import alpha_report

ROOT = Path(__file__).resolve().parents[2]
PACK = Path('packages/story-candidate-01/assets/final-fantasy-v1')


def publish(topic, export_dir, root=ROOT):
    pack = root / PACK
    manifest_path = pack / 'manifest-final-fantasy-v1.json'
    hd_path = pack / 'manifest-tactical-runtime-hd.json'
    manifest = json.loads(manifest_path.read_text())
    hd_manifest = json.loads(hd_path.read_text())
    matches = [a for a in manifest['assets'] if a['topicId'] == topic]
    hd_matches = [a for a in hd_manifest['assets'] if a['topicId'] == topic]
    if len(matches) != 1 or len(hd_matches) != 1:
        raise ValueError('exactly one canonical and HD record required')
    record, hd = matches[0], hd_matches[0]
    sequence = json.loads((export_dir / 'sequence.json').read_text())
    config = sequence['config']
    expected = record.get('frameOrder', record.get('stateOrder'))
    if list(config['frames']) != expected or len(expected) != record['frames']:
        raise ValueError('existing semantic order must remain unchanged')
    width, height = config['frameSize']
    pivot = sequence['logicalPivot']
    if record['category'] not in ('combat-unit', 'mission-unit', 'interactive-structure'):
        raise ValueError('only established sequence categories are supported')
    base = f"runtime-approved-v5/{record['category']}/{Path(record['png']).stem}"
    sources = []
    for density in (1, 2):
        source = export_dir / f'sheet@{density}x.png'
        with Image.open(source) as image:
            if image.size != (width * len(expected) * density, height * density):
                raise ValueError('exported sheet geometry does not match its sequence')
            alpha_report(image)
        sources.append((source, pack / (base + ('@2x' if density == 2 else '') + '.png')))
    # All input validation precedes writes. Footprint and gameplay fields remain
    # those already owned by the canonical record.
    record.update(png=base + '.png', width=width * len(expected), height=height,
                  frameWidth=width, frameHeight=height, anchor=pivot,
                  qualityTier='approved-chunky-v5-export-verified')
    if record['category'] in ('combat-unit', 'mission-unit'):
        record['preserveAuthoredPalette'] = True
    hd.update(png=base + '@2x.png', width=width * len(expected) * 2,
              height=height * 2, pixelDensity=2)
    for source, destination in sources:
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, destination)
    for path, value in ((manifest_path, manifest), (hd_path, hd_manifest)):
        path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n')
    return {'topicId': topic, 'png': record['png'], 'hdPng': hd['png'],
            'anchor': pivot, 'footprint': record['footprint'],
            'visualAcceptance': 'requires separate reviewed evidence', 'liveGameVerified': False}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('topic')
    parser.add_argument('export_dir', type=Path)
    args = parser.parse_args()
    print(json.dumps(publish(args.topic, args.export_dir), indent=2))
