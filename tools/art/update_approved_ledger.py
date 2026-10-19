"""Build the approved-style scope ledger without counting legacy candidates or copies."""
import csv
import json
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DOC = Path('docs/art-rebuild-20261019')


def build(root=ROOT):
    progress = json.loads((root / DOC / 'approved-v5/progress.json').read_text())
    baseline = json.loads((root / DOC / 'canonical-topic-manifest.json').read_text())
    manifests = {}
    rows = []
    for old in baseline:
        path = old['manifest']
        if path not in manifests:
            manifests[path] = {a['topicId']: a for a in json.loads((root / path).read_text())['assets']}
        current = manifests[path][old['topicId']]
        topic = current['topicId']
        stage = progress['topicStages'].get(topic)
        bound = current['qualityTier'] == 'approved-chunky-v5-export-verified'
        if bound != bool(stage):
            raise ValueError(f'formal manifest/progress mismatch: {topic}')
        stem = topic.split('-', 1)[1].lower()
        if stem.startswith('unit-'):
            relative = Path('animation') / stem.removeprefix('unit-')
        elif stem.startswith('mission-'):
            relative = Path('mission') / stem.removeprefix('mission-')
        elif stem.startswith('struct-'):
            relative = Path('buildings') / stem.removeprefix('struct-')
        elif stem.startswith('bprop-'):
            relative = Path('props') / stem
        elif stem.startswith('equip-'):
            relative = Path('equipment') / stem
        else:
            relative = None
        raw = []
        if relative:
            directory = root / Path(path).parent / 'masters/approved-v5' / relative
            raw = [str(p.relative_to(root)) for p in sorted(directory.glob('master.png'))]
            raw += [str(p.relative_to(root)) for p in sorted(directory.glob('*/master.png'))]
        rows.append({'topicId': topic, 'campaign': old['campaign'], 'category': current['category'],
                     'label': old['label'], 'baselineManifest': path, 'selectedRawMasters': raw,
                     'raw': 'preserved' if raw else 'pending',
                     'export': 'reviewed' if stage else 'pending',
                     'formalBinding': 'bound' if bound else 'pending', 'liveGame': 'pending',
                     'runtimePath': str(Path(path).parent / current['png']),
                     'stageEvidence': stage})
    counts = Counter(row['category'] for row in rows if row['formalBinding'] == 'bound')
    if sum(counts.values()) != progress['formalRuntimeBoundTopics']:
        raise ValueError('summary count does not match distinct canonical identities')
    environment = []
    for atlas in json.loads((root / DOC / 'environment-atlas-manifest.json').read_text()):
        stage = progress.get('environmentCells', {}).get(atlas['id'], {})
        environment.append({'atlasId': atlas['id'], 'baselineComponentCount': atlas['componentCount'],
                            'registeredApprovedCells': stage.get('formalRuntimeBoundCells', []),
                            'liveGame': 'pending', 'stageEvidence': stage or None})
    return {'style': 'approved chunky concept-v2 and layered1166e2e direction',
            'baseline': '2276a89f75b5a6734e6347f62941d4a0e5b37d03',
            'counting': 'One canonical identity, not frame/HD/copy. Environment components and authored team variants are separate. Old detailed-style exports add zero.',
            'summary': {'canonicalScope': len(rows), 'formalBound': sum(counts.values()),
                        'boundByCategory': dict(sorted(counts.items())),
                        'rawPreservedTopics': sum(bool(r['selectedRawMasters']) for r in rows),
                        'liveGameVerified': 0, 'environmentAtlasScope': len(environment),
                        'environmentComponentScope': sum(e['baselineComponentCount'] for e in environment),
                        'registeredEnvironmentCells': sum(len(e['registeredApprovedCells']) for e in environment)},
            'environmentAccounting': {
                'baselinePhysicalComponents': sum(e['baselineComponentCount'] for e in environment),
                'registeredIndividualSourceCells': sum(len(e['registeredApprovedCells']) for e in environment),
                'warning': 'Baseline components include derived mask/connector variants. Registered individual cells exclude separate world-material, connector, water-area and crossing bindings. Their ratio is not overall art completion; do not add environment cells to canonical topics.',
                'separateBindings': list(progress.get('materialStages', {})),
            },
            'sceneCohesion': progress.get('integrationGate', {'status': 'pending'}),
            'authoredTeamVariants': {'red-swordsman': 'four poses formally bound; not an extra canonical topic'},
            'replacementMaterialBindings': progress.get('materialStages', {}),
            'canonicalTopics': rows, 'environmentAtlases': environment}


if __name__ == '__main__':
    ledger = build()
    output = ROOT / DOC / 'approved-v5/scope-ledger.json'
    output.write_text(json.dumps(ledger, ensure_ascii=False, indent=2) + '\n')
    with output.with_suffix('.csv').open('w') as handle:
        fields = ['topicId', 'campaign', 'category', 'label', 'raw', 'export', 'formalBinding', 'liveGame', 'runtimePath']
        writer = csv.DictWriter(handle, fieldnames=fields, extrasaction='ignore', lineterminator='\n')
        writer.writeheader()
        writer.writerows(ledger['canonicalTopics'])
    print(json.dumps(ledger['summary'], indent=2))
