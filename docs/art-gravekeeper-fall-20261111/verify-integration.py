"""Read-only exact source/data scope verification for gravekeeper fall integration."""
import argparse
import collections
import hashlib
import json
from pathlib import Path
import subprocess

BASELINE = 'ea0eb0513e48ed650c2c04d259bfc4baee7f3021'


def verify(root, packet):
    def git(*args):
        return subprocess.check_output(['git', '-C', str(root), *args], text=True)

    meta = json.loads((packet / 'extraction-review.json').read_text())
    source = json.loads((root / 'godot/data/selected-assets.json').read_text())
    baseline = json.loads(git('show', f'{BASELINE}:godot/data/selected-assets.json'))
    clip = source['actorClips'].pop('gravekeeper.fall')
    assert clip == {'unit': 'milliseconds', 'appearanceId': 'gravekeeper-fall-v1',
                    'death': {'mode': 'after-clip', 'fadeMs': 160},
                    'frames': [{'frame': n, 'span': s} for n, s in zip(meta['frameOrder'], meta['playbackSpansMs'])]}
    new = [a for a in source['assets'] if a.get('id') == 'gravekeeper-fall-v1']
    assert len(new) == 1
    asset = new[0]
    source['assets'].remove(asset)
    assert source == baseline, 'unrelated manifest data changed'
    expected = {'frameOrder': meta['frameOrder'], 'frameRegions': [f['frameRegion'] for f in meta['frames']],
                'frameAnchors': [f['frameAnchor'] for f in meta['frames']], 'presentationScale': .1,
                'physicalWidth': 2154, 'physicalHeight': 405, 'sha256': meta['runtimePngSha256'],
                'pixelDensity': 1, 'textureFilter': 'linear', 'topicId': 'C01-UNIT-GRAVEKEEPER',
                'frames': 6, 'png': 'painted-actions-v1/gravekeeper-fall-v1.png',
                'resourcePath': 'res://assets/painted-actions-v1/gravekeeper-fall-v1.png'}
    for key, value in expected.items():
        assert asset[key] == value, f'accepted registration changed: {key}'
    live = root / 'godot/assets/painted-actions-v1/gravekeeper-fall-v1.png'
    assert live.read_bytes() == (packet / 'gravekeeper-fall-v1.png').read_bytes()
    assert hashlib.sha256(live.read_bytes()).hexdigest() == meta['runtimePngSha256']
    binding = json.loads((root / 'godot/data/art-bindings.json').read_text())
    old_binding = json.loads(git('show', f'{BASELINE}:godot/data/art-bindings.json'))
    assert binding['CANDIDATE_01_UNIT_ART']['c01.gravekeeper']['actionClips']['death'] == 'gravekeeper.fall'
    binding['CANDIDATE_01_UNIT_ART']['c01.gravekeeper']['actionClips']['death'] = 'static.death'
    assert binding == old_binding, 'unrelated art binding changed'
    uid_files = list((root / 'godot').rglob('*.cs.uid'))
    uid_contents = collections.Counter(p.read_text().strip() for p in uid_files)
    assert uid_contents['uid://d6keeperfall3'] == 1
    assert len(uid_contents) == len(uid_files), 'duplicate script UIDs'
    check_path = root / 'godot/Presentation/AnimationGravekeeperFallChecks.cs'
    assert (root / 'godot/Presentation/AnimationChecks.cs').read_text().count('await CheckGravekeeperFallPlayback();') == 1
    changed = git('diff', '--name-only', BASELINE).splitlines()
    check_names = {'AnimationChecks.cs', 'AnimationDeathPolicyChecks.cs', 'AnimationGravekeeperFallChecks.cs', 'AnimationGravekeeperCastChecks.cs',
                   'AnimationGravekeeperIdleChecks.cs', 'AnimationGravekeeperRecoilChecks.cs',
                   'AnimationReactionChecks.cs', 'AnimationTorrenFallChecks.cs'}
    changed_code = [p for p in changed if not p.startswith(('docs/', 'godot/docs/'))
                    and p.endswith(('.cs', '.ts', '.gd', '.gdshader'))]
    assert set(changed_code) <= {'godot/Presentation/' + n for n in check_names}, 'non-check source changed'
    assert git('diff', '--check', BASELINE) == ''
    imported = Path(str(live) + '.import')
    assert imported.is_file(), 'PNG import companion missing'
    imported_text = imported.read_text()
    assert 'source_file="res://assets/painted-actions-v1/gravekeeper-fall-v1.png"' in imported_text
    assert 'compress/mode=0' in imported_text and 'process/size_limit=0' in imported_text
    return {'status': 'PASS-static-integration-review', 'baseline': BASELINE,
            'normalizedProductionJsonExactlyEqualsBaseline': True,
            'onlyAssetAddition': 'gravekeeper-fall-v1', 'onlyClipAddition': 'gravekeeper.fall',
            'onlyBindingChange': 'CANDIDATE_01_UNIT_ART.c01.gravekeeper.actionClips.death',
            'acceptedAtlasByteEqual': True, 'atlasSha256': hashlib.sha256(live.read_bytes()).hexdigest(),
            'assetMetadataMatchesSourceAcceptance': True,
            'newCheckSha256': hashlib.sha256(check_path.read_bytes()).hexdigest(),
            'newCheckRegisteredOnce': True, 'newUidOccurrenceCount': uid_contents['uid://d6keeperfall3'],
            'allCsUidFileCount': len(uid_files), 'allCsUidsUnique': True,
            'noChangedRuntimeRuleOrPreviewFile': True, 'trackedChangedFiles': changed,
            'pngImportCompanionPresent': True, 'gitDiffCheckClean': True,
            'notRun': ['build', 'editor import', 'native tests', 'native gameplay', 'performance']}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-root', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--packet', type=Path, default=Path(__file__).resolve().parent)
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    report = json.dumps(verify(args.reference_root.resolve(), args.packet.resolve()), indent=2) + '\n'
    if args.report:
        args.report.write_text(report)
    print(report)
