#!/usr/bin/env python3
"""Read-only independent verification of packaged native archer hit evidence.

No game process, Godot, build, archive writer or image editor is invoked. The only
writes are this verifier's JSON/Markdown reports in the selected output directory.
Full mode requires original QA PNG/JSON and frozen DLLs; archive-only mode checks
durable repo evidence without claiming to repeat those original-source checks.
"""
from __future__ import annotations

import argparse
from copy import deepcopy
from fractions import Fraction
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
from typing import Any

from PIL import Image


class VerificationError(RuntimeError):
    pass


def require(condition: bool, message: str) -> None:
    if not condition:
        raise VerificationError(message)


def sha_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha(path: Path) -> str:
    return sha_bytes(path.read_bytes())


def read_json(path: Path) -> Any:
    return json.loads(path.read_text())


def canonical(value: Any) -> str:
    return json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=False, allow_nan=False)


def differences(left: Any, right: Any, path: str = '') -> list[str]:
    if type(left) is not type(right):
        return [path]
    if isinstance(left, dict):
        found = []
        for key in sorted(left.keys() | right.keys()):
            where = f'{path}.{key}' if path else key
            found += [where] if key not in left or key not in right else differences(left[key], right[key], where)
        return found
    if isinstance(left, list):
        if len(left) != len(right):
            return [path + '.length']
        return [entry for index, (a, b) in enumerate(zip(left, right))
                for entry in differences(a, b, f'{path}[{index}]')]
    return [] if left == right else [path]


def normalize_save(value: Any) -> Any:
    result = deepcopy(value)
    # Only these two exact writer-owned fields may be excluded.
    for section in ('campaign', 'battle'):
        require('savedAt' in result[section], f'Missing required {section}.savedAt')
        del result[section]['savedAt']
    return result


def read_exact(stream: Any, length: int) -> bytes:
    parts = []
    remaining = length
    while remaining:
        part = stream.read(remaining)
        if not part:
            break
        parts.append(part)
        remaining -= len(part)
    return b''.join(parts)


def probe_video(path: Path) -> dict:
    process = subprocess.run(['ffprobe', '-v', 'error', '-select_streams', 'v:0', '-count_frames',
                              '-show_entries', 'stream=codec_name,pix_fmt,width,height,r_frame_rate,avg_frame_rate,nb_read_frames,nb_frames,duration',
                              '-of', 'json', str(path)], capture_output=True, text=True, check=True)
    streams = json.loads(process.stdout)['streams']
    require(len(streams) == 1, f'{path.name}: expected one video stream')
    return streams[0]


def decode_compare(video: Path, pngs: list[Path] | None, proof: dict) -> dict:
    count = len(proof["frames"])
    require(pngs is None or len(pngs) == count, f"{video.name}: source PNG count differs")
    stream = probe_video(video)
    require((stream['width'], stream['height']) == (960, 640), f'{video.name}: wrong dimensions')
    require(int(stream['nb_read_frames']) == count == int(stream['nb_frames']), f'{video.name}: frame count mismatch')
    require(Fraction(stream['r_frame_rate']) == Fraction(stream['avg_frame_rate']) == 60, f'{video.name}: video rate differs')
    require(stream['pix_fmt'] == 'gbrp', f'{video.name}: unexpected encoded pixel format')
    require(abs(float(stream['duration']) - count / 60) < 0.000001, f'{video.name}: duration differs')
    require(sha(video) == proof['videoSha256'], f'{video.name}: proof video hash differs')
    records = []
    process = subprocess.Popen(['ffmpeg', '-v', 'error', '-i', str(video), '-map', '0:v:0',
                                '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-'],
                               stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    try:
        for index, recorded in enumerate(proof['frames']):
            require(recorded['file'] == f'frame-{index:03d}.png', f'{video.name}: proof frame order differs')
            decoded = read_exact(process.stdout, 960 * 640 * 3)
            require(len(decoded) == 960 * 640 * 3, f'{video.name}: incomplete decoded frame {index}')
            require(sha_bytes(decoded) == recorded['rgbSha256'], f'{video.name}: decoded RGB hash differs at {index}')
            if pngs is not None:
                png = pngs[index]
                with Image.open(png) as original:
                    require(original.size == (960, 640), f'{png}: wrong PNG dimensions')
                    pixels = original.convert('RGB').tobytes()
                require(decoded == pixels, f'{video.name}: RGB pixels differ at frame {index}')
                require(recorded['file'] == png.name and recorded['pngSha256'] == sha(png),
                        f'{png}: proof PNG hash differs')
            records.append({'frame': index, 'file': recorded['file'], 'pngSha256': recorded['pngSha256'],
                            'rgbSha256': sha_bytes(decoded), 'decodedRgbHashMatch': True,
                            'originalPngVerified': pngs is not None,
                            'decodedRgbExact': True if pngs is not None else None})
        require(process.stdout.read(1) == b'', f'{video.name}: unexpected extra decoded frame data')
        error = process.stderr.read().decode()
        code = process.wait(timeout=30)
        require(code == 0 and not error.strip(), f'{video.name}: decoder error {code}: {error}')
    finally:
        if process.poll() is None:
            process.kill()
            process.wait()
    return {'ffprobe': stream, 'decoderExit': code, 'allDecodedRgbExact': True if pngs is not None else None,
            'allDecodedRgbHashesMatch': True, 'alphaCompared': False,
            'originalPngHashesVerified': pngs is not None, 'frames': records}


def trace_actor(rows: list[dict], actor_id: int, expected: dict, asset: dict) -> dict:
    values = []
    for index, row in enumerate(rows):
        found = [actor for actor in row['actors'] if actor['id'] == actor_id]
        require(len(found) == 1, f'Actor {actor_id} not uniquely recorded at frame {index}')
        values.append(found[0])
    transitions = []
    for index, value in enumerate(values):
        now = (value['art'], value['frame'])
        if index == 0 or now != (values[index - 1]['art'], values[index - 1]['frame']):
            transitions.append({'observation': index, 'art': now[0], 'frame': now[1]})
    require(transitions == expected['poseTransitions'], f'Actor {actor_id}: proof transitions differ')
    recoil = [phase for phase in transitions if phase['art'] == 'archer-recoil-v1']
    require([phase['frame'] for phase in recoil] == [0, 1, 2, 3], f'Actor {actor_id}: four recoil phases missing or reordered')
    start = recoil[0]['observation']
    following = next(phase for phase in transitions if phase['observation'] > recoil[-1]['observation'])
    end = following['observation']
    require(following['art'] == 'archer-breathe-v1' and following['frame'] == 0,
            f'Actor {actor_id}: recoil does not restore idle')
    samples = values[start:end]
    support = samples[0]['supportPlane']
    mirrored = samples[0]['mirrored']
    scale = asset['presentationScale'] / asset['pixelDensity']
    expected_scale = [-scale if mirrored else scale, scale]
    require(expected['mirrored'] is mirrored and expected['recoilSupportPlane'] == support
            and expected['recoilSupportAndPositionConstant'] is True, f'Actor {actor_id}: proof grounding differs')
    for frame_index, value in enumerate(samples, start):
        require(value['art'] == 'archer-recoil-v1', f'Actor {actor_id}: unexpected recoil interruption')
        require(value['scale'] == expected_scale and value['mirrored'] is mirrored,
                f'Actor {actor_id}: scale/facing changed at {frame_index}')
        require(value['position'] == value['supportPlane'] == support,
                f'Actor {actor_id}: root/support changed at {frame_index}')
        anchor = asset['frameAnchors'][value['frame']]
        require(value['offset'] == [-part * asset['pixelDensity'] for part in anchor],
                f'Actor {actor_id}: frame anchor differs at {frame_index}')
    require(values[end]['position'] == values[end]['supportPlane'] == support
            and values[end]['scale'] == expected_scale and values[end]['mirrored'] is mirrored,
            f'Actor {actor_id}: idle handoff changes registration')
    ranges = []
    for phase_index, phase in enumerate(recoil):
        stop = recoil[phase_index + 1]['observation'] if phase_index < 3 else end
        ranges.append({'frame': phase['frame'], 'firstObservationIndex': phase['observation'],
                       'lastObservationIndex': stop - 1, 'count': stop - phase['observation']})
    return {'id': actor_id, 'mirrored': mirrored, 'scale': expected_scale, 'constantSupportPlane': support,
            'recoilSamples': len(samples), 'poseRanges': ranges, 'idleReturnObservationIndex': end,
            'manifestAnchorsMatch': True, 'rootSupportAndIdleHandoffStable': True,
            'poseTransitions': transitions}


def verify_sheet(path: Path, pngs: list[Path], crop: tuple, indices: list[int]) -> dict:
    width, height = (crop[2] - crop[0]) * 3, (crop[3] - crop[1]) * 3
    with Image.open(path) as image:
        image = image.convert('RGB')
        require(image.size == (5 * width, 2 * (height + 24)), f'{path.name}: sheet size differs')
        for panel, index in enumerate(indices):
            x, y = panel % 5 * width, panel // 5 * (height + 24) + 24
            with Image.open(pngs[index]) as source:
                expected = source.convert('RGB').crop(crop).resize((width, height), Image.Resampling.NEAREST)
            actual = image.crop((x, y, x + width, y + height))
            require(actual.tobytes() == expected.tobytes(), f'{path.name}: source panel {index} differs')
    return {'file': path.name, 'allImagePanelsExactSourceCrops': True,
            'cropBox': crop, 'sourceFrameIndices': indices, 'magnification': '3x nearest-neighbor'}


def compare_save(archive: Path, raw: Path | None, expected: Path, proof_hash: str) -> dict:
    if raw is not None:
        require(archive.read_bytes() == raw.read_bytes(), f'{archive.name}: raw QA copy differs')
    require(sha(archive) == proof_hash, f'{archive.name}: proof save hash differs')
    actual_document, expected_document = read_json(archive), read_json(expected)
    paths = differences(actual_document, expected_document)
    require(set(paths) <= {'campaign.savedAt', 'battle.savedAt'}, f'{archive.name}: unauthorized save differences: {paths}')
    a, b = canonical(normalize_save(actual_document)), canonical(normalize_save(expected_document))
    require(a == b, f'{archive.name}: complete normalized documents differ')
    return {'actualFile': archive.name, 'expectedFile': str(expected), 'rawQaBytesMatch': True if raw is not None else None,
            'actualSha256': sha(archive), 'expectedSha256': sha(expected),
            'differingPathsBeforeExclusion': paths, 'excludedFields': ['campaign.savedAt', 'battle.savedAt'],
            'completeDocumentEqualAfterOnlyTwoExclusions': True, 'normalizedDocumentSha256': sha_bytes(a.encode())}


def verify_sequence(kind: str, qa: Path, archive: Path, asset: dict, assemblies: dict) -> dict:
    count, actor_ids, route, expected_name, accepted = {
        'southern': (95, (3, 18), 'southern-bows', 'ui-settled.json', True),
        'dual': (99, (9, 14), 'enemy-west', 'after.json', False),
    }[kind]
    proof = read_json(archive / f'native-{kind}-proof.json')
    motion_dirs = list((qa / f'native-{kind}').glob('motion-*'))
    require(len(motion_dirs) == 1, f'{kind}: expected exactly one original motion directory')
    source = motion_dirs[0]
    pngs = sorted(source.glob('frame-*.png'))
    require([path.name for path in pngs] == [f'frame-{index:03d}.png' for index in range(count)], f'{kind}: missing or excess PNGs')
    require(len(list(source.glob('frame-*.json'))) == count, f'{kind}: raw trace count differs')
    original_rows = [read_json(path.with_suffix('.json')) for path in pngs]
    rows = [json.loads(line) for line in (archive / f'native-{kind}-frames.jsonl').read_text().splitlines()]
    require(rows == original_rows, f'{kind}: archived traces differ from original observations')
    require(len(rows) == proof['savedFrames'] == count == len(proof['frames']), f'{kind}: proof count differs')
    require(proof['viewport'] == [960, 640] and proof['captureStride'] == 1, f'{kind}: capture geometry differs')
    require([row['observedFrame'] for row in rows] == list(range(1, count + 1)), f'{kind}: observed-frame gap')
    start_process = rows[0]['processFrame']
    require([row['processFrame'] for row in rows] == list(range(start_process, start_process + count)), f'{kind}: process-frame gap')
    require(all(row['captureStride'] == 1 and row['captureMode'] == 'motion' for row in rows), f'{kind}: mixed stride/mode')
    require(all(row['presenting'] is True for row in rows[:-1]) and rows[-1]['presenting'] is False,
            f'{kind}: missing normal settled endpoint')
    require(all(b['elapsedMs'] >= a['elapsedMs'] for a, b in zip(rows, rows[1:])), f'{kind}: wall observations out of order')
    require(proof['assembliesSha256'] == assemblies, f'{kind}: DLL proof differs')
    require(proof['sourceCommit'] == '8893ad5555ab02fbc1fa08b876c5918fb30ced64', f'{kind}: implementation identity differs')
    require(proof['visualAcceptedScope'] is accepted, f'{kind}: visual acceptance incorrectly scoped')
    require(proof['performanceBenchmark'] is False and proof['originalPngAlphaInVideo'] is False,
            f'{kind}: performance/alpha claim differs')
    clock_text = proof['clockInterpretation']
    require('wall-clock observation including readback' in clock_text
            and 'not the presentation clock' in clock_text
            and 'No performance or exact first-frame clock offset claim' in clock_text,
            f'{kind}: clock limits missing')
    require(proof['fixedSimulationFps'] == proof['videoFps'] == 60
            and proof['encodedDurationSeconds'] == count / 60, f'{kind}: rate/duration declaration differs')
    launch_text = (qa / f'launch-{kind}.sh').read_text()
    require('--fixed-fps 60' in launch_text and '--resolution 960x640' in launch_text, f'{kind}: recorded launcher disagrees')
    log = (archive / f'native-{kind}-session.txt').read_text()
    require(log == (qa / f'native-{kind}.log').read_text(), f'{kind}: session log differs')
    require(f'{count} saved frames over {count} rendered frames, stride 1' in log
            and 'timestamps are observations, not a performance benchmark' in log, f'{kind}: session capture statement missing')
    require(not re.search(r'(^ERROR:|^SCRIPT ERROR:|leaked|segmentation fault|unhandled exception)', log, re.M | re.I),
            f'{kind}: native session diagnostics failed')
    decoded = decode_compare(archive / f'native-{kind}-lossless.mp4', pngs, proof)
    actors = {str(actor_id): trace_actor(rows, actor_id, proof['actors'][str(actor_id)], asset) for actor_id in actor_ids}
    require({actor['mirrored'] for actor in actors.values()} == {False, True}, f'{kind}: both trace facings not represented')
    input_path = archive / f'native-{kind}-input-state.json'
    require(input_path.read_bytes() == (qa / f'native-{kind}-input.json').read_bytes()
            == (qa / 'legal-final' / route / 'before.json').read_bytes(), f'{kind}: captured complete input save differs')
    input_comparison = compare_save(input_path, qa / f'native-{kind}-input.json',
                                    archive / 'legal-hit' / route / 'before.json', proof['inputSaveSha256'])
    official_after = archive / 'legal-hit' / route / expected_name
    require(official_after.read_bytes() == (qa / 'legal-final-settled' / route / expected_name).read_bytes(),
            f'{kind}: archived official replay result differs from QA source')
    save = compare_save(archive / f'native-{kind}-saved-state.json', qa / f'native-{kind}-output.json',
                        official_after, proof['actualUiSaveSha256'])
    comparison = proof['actualUiSaveComparison']
    require(comparison['expectedLegalRoute'] == route and comparison['expectedFile'] == expected_name
            and comparison['normalAiEndTurnAfterCapture'] is (kind == 'southern'), f'{kind}: proof save comparison scope differs')
    images = []
    if kind == 'southern':
        require('Damage numerals' in proof['visibilityLimit'] and 'Not an unobstructed audit' in proof['visibilityLimit'],
                'southern: overlay limitation omitted')
        for actor_id, crop, indices in [(3, (333, 257, 405, 338), [0, 24, 27, 33, 39, 43, 49, 69, 86, 94]),
                                       (18, (442, 232, 540, 324), [5, 24, 41, 69, 71, 78, 83, 88, 90, 94])]:
            images.append(verify_sheet(archive / f'native-southern-unit-{actor_id}-phases-3x.png', pngs, crop, indices))
        for index, label in [(27, 'elin-recoil'), (43, 'elin-idle-return'), (71, 'enemy-recoil'),
                             (88, 'enemy-idle-return'), (94, 'complete')]:
            require((archive / f'native-southern-{label}.png').read_bytes() == pngs[index].read_bytes(),
                    f'southern: keyframe {label} differs')
        for number, label in [(2, 'elin-health'), (3, 'saved-result')]:
            require((archive / f'native-southern-{label}.png').read_bytes()
                    == (qa / 'native-southern' / f'native-{number:03d}.png').read_bytes(), f'southern: still {label} differs')
    else:
        require('roof-occluded' in proof['visibilityLimit'] and 'excluded from two-facing art acceptance' in proof['visibilityLimit'],
                'dual: roof exclusion omitted')
        require((archive / 'native-dual-roof-limit.png').read_bytes() == pngs[75].read_bytes(), 'dual: roof keyframe differs')
    return {'savedFrames': count, 'visualAcceptedScope': accepted, 'originalMotionDirectory': str(source),
            'decodedVideo': decoded, 'traceRowsEqualOriginal': True,
            'observedFrameRange': [1, count], 'processFrameRange': [start_process, rows[-1]['processFrame']],
            'captureStride': 1, 'wallObservationElapsedMsRange': [rows[0]['elapsedMs'], rows[-1]['elapsedMs']],
            'encodedDurationSeconds': count / 60, 'clockClaimsProperlyLimited': True,
            'allButFinalPresentingAndFinalSettled': True, 'actors': actors,
            'inputCapturedSaveByteEqual': True, 'inputOfficialSaveComparison': input_comparison,
            'inputSha256': sha(input_path), 'savedState': save,
            'phaseSheetChecks': images, 'keyframeOriginalBytesVerified': True,
            'visibilityLimit': proof['visibilityLimit'],
            'sessionWarnings': [line for line in log.splitlines() if line.startswith('WARNING:')]}


def run(qa: Path, repo: Path) -> dict:
    archive = repo / 'docs/art-archer-hit-20261110/verification'
    asset = next(asset for asset in read_json(repo / 'godot/data/selected-assets.json')['assets']
                 if asset.get('id') == 'archer-recoil-v1')
    require(asset['pixelDensity'] == 1 and asset['presentationScale'] == .072
            and asset['frameOrder'] == ['recoil-onset', 'recoil-peak', 'recoil-recover', 'recoil-ready'],
            'Production recoil manifest geometry differs')
    image = repo / 'godot/assets/painted-actions-v1/archer-recoil-v1.png'
    require(sha(image) == asset['sha256'], 'Production recoil atlas hash differs')
    assemblies = {}
    for line in (qa / 'native-build-sha256.txt').read_text().splitlines():
        digest, path = line.split()
        name = Path(path).name
        require(sha(repo / path) == sha(qa / 'final-assemblies' / name) == digest, f'{name}: frozen/current DLL mismatch')
        assemblies[name] = digest
    require(set(assemblies) == {'Empire.dll', 'Empire.Core.dll', 'GodotSharp.dll'}, 'Incomplete assembly manifest')
    legal_dlls = {Path(path).name: digest for digest, path in
                  (line.split() for line in (archive / 'legal-hit/runtime-assemblies-sha256.txt').read_text().splitlines())}
    require(legal_dlls == assemblies, 'Legal replay assembly manifest differs')
    results = {kind: verify_sequence(kind, qa, archive, asset, assemblies) for kind in ('southern', 'dual')}
    require(all(sha(repo / 'godot/.godot/mono/temp/bin/Debug' / name) == digest
                and sha(qa / 'final-assemblies' / name) == digest for name, digest in assemblies.items()),
            'Frozen/current DLL changed during verification')
    return {'passed': True, 'readOnlyReview': True, 'gameBuildOrGuiStarted': False,
            'sourceImplementationCommit': '8893ad5555ab02fbc1fa08b876c5918fb30ced64',
            'currentFrozenAndLegalReplayDllHashesEqual': True, 'assembliesSha256': assemblies,
            'totalRgbVerifiedFrames': 194, 'acceptedVisualCaptureFrames': 95,
            'diagnosticOnlyCaptureFrames': 99, 'sequences': results,
            'reviewerVisualInspection': {
                'scope': 'Both native southern phase sheets (all four recoil poses per direction and idle/shot handoffs), plus full-frame roof exclusion image.',
                'finding': 'Visible southern hood/head, shoulder recoil, bow/quiver identity and foot-contact portions are coherent; no visible whole-body translation, scale jump or mirrored hand swap. Central damage text and Elin completion badge prevent unobstructed hands/torso/sole inspection. Roof route cannot establish full-body grounding from pixels.',
                'notClaimed': 'This verifier does not claim a separate visual inspection of every one of the 194 full frames; every frame was computationally RGB-compared. It does not repeat the lawful battle replay, GUI interaction, aggregate gate or performance benchmark.'
            },
            'limitations': [
                'Only RGB pixels are stored/compared in the lossless video; original PNG alpha is excluded explicitly.',
                'ElapsedMs uses Time.GetTicksMsec after viewport readback, not the animation timeline. 60 fps is the launch/encoding setting, not a measured realtime rate or a first-frame zero-time guarantee.',
                'The 95-frame southern sequence supports visible-region visual acceptance with existing text/badge/impact overlays. The 99-frame roof sequence remains diagnostic only.',
                'Trace equality validates recorded root/support/scale/facing/offsets. Occluded anatomy cannot be recovered or authenticated from those metadata values.',
                'Complete saves match official archived replay outputs after deleting exactly campaign.savedAt and battle.savedAt. This read-only review does not rerun the legal derivation.'
            ]}


def run_archive_only(repo: Path) -> dict:
    """Recheck durable archive without claiming access to original captures or DLLs."""
    archive = repo / 'docs/art-archer-hit-20261110/verification'
    asset = next(asset for asset in read_json(repo / 'godot/data/selected-assets.json')['assets']
                 if asset.get('id') == 'archer-recoil-v1')
    recorded_assemblies = {Path(path).name: digest for digest, path in
                          (line.split() for line in (archive / 'legal-hit/runtime-assemblies-sha256.txt').read_text().splitlines())}
    results = {}
    for kind, count, ids, route, expected_name, accepted in [
            ('southern', 95, (3, 18), 'southern-bows', 'ui-settled.json', True),
            ('dual', 99, (9, 14), 'enemy-west', 'after.json', False)]:
        proof = read_json(archive / f'native-{kind}-proof.json')
        rows = [json.loads(line) for line in (archive / f'native-{kind}-frames.jsonl').read_text().splitlines()]
        require(proof['savedFrames'] == len(rows) == count == len(proof['frames']), f'{kind}: archive count differs')
        require([row['observedFrame'] for row in rows] == list(range(1, count + 1)), f'{kind}: observation gap')
        require([row['processFrame'] for row in rows] == list(range(rows[0]['processFrame'], rows[0]['processFrame'] + count)),
                f'{kind}: process-frame gap')
        require(all(row['captureStride'] == 1 and row['captureMode'] == 'motion' for row in rows), f'{kind}: stride/mode differs')
        require(all(row['presenting'] is True for row in rows[:-1]) and rows[-1]['presenting'] is False, f'{kind}: endpoint differs')
        require(proof['assembliesSha256'] == recorded_assemblies, f'{kind}: recorded DLL hashes disagree')
        require(proof['visualAcceptedScope'] is accepted and proof['performanceBenchmark'] is False
                and proof['originalPngAlphaInVideo'] is False, f'{kind}: archive scope differs')
        require('wall-clock observation including readback' in proof['clockInterpretation']
                and 'No performance or exact first-frame clock offset claim' in proof['clockInterpretation'],
                f'{kind}: clock limits missing')
        require(proof['captureStride'] == 1 and proof['viewport'] == [960, 640]
                and proof['fixedSimulationFps'] == proof['videoFps'] == 60
                and proof['encodedDurationSeconds'] == count / 60, f'{kind}: archive rate/geometry differs')
        video = decode_compare(archive / f'native-{kind}-lossless.mp4', None, proof)
        actors = {str(actor_id): trace_actor(rows, actor_id, proof['actors'][str(actor_id)], asset) for actor_id in ids}
        input_result = compare_save(archive / f'native-{kind}-input-state.json', None,
                                    archive / 'legal-hit' / route / 'before.json', proof['inputSaveSha256'])
        save_result = compare_save(archive / f'native-{kind}-saved-state.json', None,
                                   archive / 'legal-hit' / route / expected_name, proof['actualUiSaveSha256'])
        results[kind] = {'savedFrames': count, 'visualAcceptedScope': accepted, 'decodedVideo': video,
                         'processFrameRange': [rows[0]['processFrame'], rows[-1]['processFrame']],
                         'actors': actors, 'completeInputSaveComparison': input_result,
                         'completeOutputSaveComparison': save_result}
    return {'passed': True, 'verificationMode': 'archive-only', 'gameBuildOrGuiStarted': False,
            'totalRgbHashVerifiedFrames': 194, 'acceptedVisualCaptureFrames': 95, 'diagnosticOnlyCaptureFrames': 99,
            'originalQaPngComparisonPerformed': False, 'originalQaJsonComparisonPerformed': False,
            'frozenOrCurrentDllBytesCompared': False, 'recordedDllHashesConsistent': True,
            'assembliesSha256': recorded_assemblies, 'sequences': results,
            'limits': 'Archive-only independently decodes RGB and checks durable proof hashes, trace contracts and entire official saves. It does not redo the original PNG/JSON comparison, frozen/current DLL byte comparison or visual review recorded by the full QA pass.'}


def report_markdown(result: dict) -> str:
    if not result['passed']:
        return '# Independent native evidence review\n\nFAILED: ' + result['error'] + '\n'
    if result.get('verificationMode') == 'archive-only':
        return ('# Independent native archive recheck\n\nPASS: all 194 decoded RGB frame hashes, both stride-1 traces, four recoil phases per actor, and complete input/output saves match the durable archive contracts. Only the two savedAt fields are excluded from official-save comparison.\n\nScope remains 95 southern visual-acceptance frames and 99 roof-route diagnostic frames.\n\n' + result['limits'] + '\n')
    lines = ['# Independent native evidence review', '',
             '**PASS within the stated evidence limits.** Read-only verification: no GUI, Godot process, build, production edit or commit.', '',
             '## Independent frame and trace verification', '',
             '- Decoded both archived H.264 RGB videos with ffmpeg. All 194 decoded RGB frames exactly equal the original QA PNG RGB bytes, in order. Per-frame PNG/RGB hashes and video hashes also match their proof files.',
             '- This means 95 southern frames verified plus 99 roof-route diagnostic frames verified. It is not a claim that all 194 passed visual acceptance.',
             '- Both videos are 960×640 and 60/1 encoded fps. No PNG alpha is claimed in the video.', '']
    for kind, item in result['sequences'].items():
        lines += [f'### {kind}', '',
                  f"- {item['savedFrames']} observations; process frames {item['processFrameRange'][0]}–{item['processFrameRange'][1]}, capture stride 1, no missing PNG/JSON indices or process-frame gaps.",
                  f"- Archived JSONL equals every original QA actor JSON; all observations except the final one are presenting, and the final observation is settled.",
                  f"- Recorded wall elapsedMs range: {item['wallObservationElapsedMsRange'][0]}–{item['wallObservationElapsedMsRange'][1]}; encoded duration {item['encodedDurationSeconds']:.6f} s. These values are not treated as the presentation clock or realtime performance."]
        for actor_id, actor in item['actors'].items():
            ranges = ', '.join(f"pose {phase['frame']}: {phase['firstObservationIndex']:03d}–{phase['lastObservationIndex']:03d}" for phase in actor['poseRanges'])
            lines += [f"- Actor {actor_id}: {ranges}; idle returns at {actor['idleReturnObservationIndex']:03d}. Mirrored={actor['mirrored']}; scale={actor['scale']}; support={actor['constantSupportPlane']}."]
        lines += ['- Every recoil sample matches its production frame anchor, uniform scale and facing. Root and projected support are equal and constant through recoil and the first returned idle.',
                  '- Complete actual UI save equals the official replay result after excluding only campaign.savedAt and battle.savedAt; captured input is byte-identical to the original QA route input and equals the re-derived official input after the same two timestamp exclusions.',
                  f"- Visual acceptance scope: {'southern visible-region pass, with overlay limits' if item['visualAcceptedScope'] else 'diagnostic only, excluded from complete two-facing art acceptance'}.", '']
    lines += ['## Visual review and provenance', '',
              '- Independently inspected both archived southern phase sheets and the full native roof-exclusion image. Both directions visibly contain onset, peak, recovery and ready with coherent hood, quiver, bow and idle/counter handoffs. No visible whole-body displacement or size pop was found.',
              '- All 20 image panels in the two phase sheets equal the stated original-frame crops at exactly 3× nearest-neighbor magnification. Archived keyframes and still screenshots are byte-identical to their QA originals.',
              '- Damage numbers cover central chest/arm details in the recoil samples; impact effects touch the feet; Elin’s completion badge covers part of the lower leg/bow. This is not an unobstructed audit of hands, torso or every sole pixel.',
              '- Unit 9 is substantially roof-hidden in the dual route. Its trace is useful but cannot replace visible anatomy/contact evidence. The proof correctly sets visualAcceptedScope=false there and true only for the limited southern scope.',
              '- Empire.dll, Empire.Core.dll and GodotSharp.dll exactly match the recorded capture hashes, frozen QA copies, current runtime copies and legal-replay manifest, both before and after verification.', '',
              '## Clock and scope limits', '',
              '- The capture code records Time.GetTicksMsec after native viewport readback. Both proof files correctly describe elapsedMs as wall-clock observation including readback, disclaim an exact first-frame clock offset and set performanceBenchmark=false.',
              '- The 60 fps launcher/encoding configuration and 1.583333/1.650000-second video durations do not establish wall-clock smoothness, zero-time alignment or a runtime performance benchmark.',
              '- This independent pass decodes every frame computationally and inspects representative complete pose sheets. It does not claim a separate manual review of all 194 full screenshots, rerun legal gameplay derivation, cancellation/death/reduced-motion checks, aggregate checks or GUI interaction.', '',
              'Detailed per-frame hashes, complete-save canonical hashes, pose ranges and DLL hashes are in independent-native-review.json. Reproduce with independent-native-verify.py, retaining the archived evidence and original QA PNG/JSON directories.', '']
    return '\n'.join(lines)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument('--qa', '--qa-root', dest='qa', type=Path, default=Path(__file__).resolve().parent)
    parser.add_argument('--repo', type=Path)
    parser.add_argument('--archive-only', action='store_true', help='Verify repo video/proof/trace/save evidence without original QA PNGs or DLL copies.')
    parser.add_argument('--output-dir', type=Path, help='Report destination; defaults to --qa-root.')
    arguments = parser.parse_args()
    qa = arguments.qa.resolve()
    repo = arguments.repo.resolve() if arguments.repo else qa.parent / 'empire-work'
    output = arguments.output_dir.resolve() if arguments.output_dir else qa
    output.mkdir(parents=True, exist_ok=True)
    try:
        result = run_archive_only(repo) if arguments.archive_only else run(qa, repo)
    except Exception as error:
        result = {'passed': False, 'error': str(error), 'gameBuildOrGuiStarted': False}
    name = 'independent-native-archive-review' if arguments.archive_only else 'independent-native-review'
    (output / (name + '.json')).write_text(json.dumps(result, indent=2, ensure_ascii=False) + '\n')
    (output / (name + '.md')).write_text(report_markdown(result))
    print(json.dumps({key: result[key] for key in ('passed', 'totalRgbVerifiedFrames', 'totalRgbHashVerifiedFrames', 'acceptedVisualCaptureFrames',
                                                  'diagnosticOnlyCaptureFrames', 'error') if key in result}, indent=2))
    return 0 if result['passed'] else 1


if __name__ == '__main__':
    sys.exit(main())
