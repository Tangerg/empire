#!/usr/bin/env python3
"""Read-only verification of actual gravekeeper idle/heal observations and official saves.

Raw mode consumes frame-NNN.png/json; archive mode consumes a proof, JSONL trace
and lossless video. Neither mode generates captures, saves, videos or game state.
"""
import argparse
import copy
from fractions import Fraction
import hashlib
import json
from pathlib import Path
import subprocess
from PIL import Image, ImageChops

ROOT = Path(__file__).resolve().parent
LEGAL = ROOT.parent / 'legal'
SOURCE = '869078976dd21f5d82932dc4151bdd03fa973c1a'
GODOT_TREE = 'cfe370846c78c10100fbcfc8d19965efd96be697'
TOOLS_TREE = 'afff6c24fcf143fdff3a5a4facf2f373d1ad3290'
ASSEMBLIES = {
    'Empire.dll': '2a6f890c63326c3490c09eb21bbf4c336eda5ebb08aa5f320badb16e57ff6ff7',
    'Empire.Core.dll': '449a03ce3fe8681b8ee90eeae72b355b523375b263ed511f2853d3a99d558662',
    'GodotSharp.dll': '2142e62894176d7340f50581f8b2fe84e36ae64e1a85f1b2bd7737a7e8a5c8ba',
}
IDLE = 'gravekeeper-breathe-v1'
CAST = 'gravekeeper-cast-v1'
IDLE_ENDS = (700, 1200, 1700, 2400)
IDLE_OFFSETS = ([-231, -480], [-231, -482], [-231, -482], [-231, -480])
CAST_ENDS = (90, 190, 300, 400, 500, 600)
CAST_OFFSETS = ([-185, -401], [-187, -401], [-185, -401], [-190, -401], [-187, -401], [-188, -401])
VIEWPORT = (960, 640)
KINDS = ('idle-right', 'idle-left', 'recovery-left')


def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def load(path):
    return json.loads(path.read_text())


def typed_equal(a, b):
    if isinstance(a, bool) != isinstance(b, bool):
        return False
    if isinstance(a, dict) and isinstance(b, dict):
        return a.keys() == b.keys() and all(typed_equal(a[k], b[k]) for k in a)
    if isinstance(a, list) and isinstance(b, list):
        return len(a) == len(b) and all(typed_equal(x, y) for x, y in zip(a, b))
    return a == b


def save_content(save):
    result = copy.deepcopy(save)
    times = {}
    for owner in ('campaign', 'battle'):
        timestamp = result[owner].pop('savedAt')
        require(isinstance(timestamp, str) and bool(timestamp), f'Missing {owner}.savedAt')
        times[owner] = timestamp
    return result, times


def verify_saves(kind, actual_input, actual_output, expected_input, expected_output):
    require(typed_equal(actual_input, expected_input), 'Native input differs from the exact legal input save')
    actual, output_times = save_content(actual_output)
    expected, _ = save_content(expected_output)
    _, input_times = save_content(actual_input)
    require(typed_equal(actual, expected), 'Official output differs beyond the two writer timestamps')
    require(all(output_times[k] != input_times[k] for k in input_times),
            'Both output writer timestamps must differ from native input; copied input is not F5 evidence')
    before = {u['id']: u for u in actual_input['battle']['state']['units']}
    after = {u['id']: u for u in actual_output['battle']['state']['units']}
    require(before[4]['type'] == after[4]['type'] == 'c01.gravekeeper', 'Source is not generic keeper4')
    if kind == 'recovery-left':
        require(before[3]['hp'] == 39 and after[3]['hp'] == 69, 'Legal target3 heal must be39→69')
        require(after[4]['facing'] == 'west', 'Legal recovered keeper must face west')
    else:
        facing = 'east' if kind == 'idle-right' else 'west'
        require(before[4]['facing'] == after[4]['facing'] == facing, 'Legal idle facing changed')
    return {'completeSaveEqualExceptTwoWriterTimestamps': True,
            'freshOfficialSaveTimestamps': True, 'omittedPaths': ['campaign.savedAt', 'battle.savedAt'],
            'writerTimestamps': {k: {'input': input_times[k], 'output': output_times[k]} for k in input_times}}


def frame_at(milliseconds, ends):
    return next((i for i, end in enumerate(ends) if milliseconds < end - 1e-7), len(ends) - 1)


def actor4(trace):
    found = [a for a in trace['actors'] if a['id'] == 4]
    require(len(found) == 1, 'Every observation must contain keeper4 exactly once')
    return found[0]


def phase_intervals(frames, step):
    boundaries = {0.0, 2400.0}
    for i in range(len(frames)):
        boundaries.update((edge - i * step) % 2400 for edge in (0, *IDLE_ENDS))
    edges = sorted(boundaries)
    accepted = []
    for left, right in zip(edges, edges[1:]):
        phase = (left + right) / 2
        if all(frame_at((phase + i * step) % 2400, IDLE_ENDS) == f for i, f in enumerate(frames)):
            accepted.append([left, right])
    return accepted


def finite_timing_candidates(traces, actors, step):
    boundaries = {0.0, step}
    for i in range(len(traces)):
        for edge in (0, *CAST_ENDS, 800):
            value = edge - i * step
            if -1e-7 <= value <= step + 1e-7:
                boundaries.add(min(step, max(0.0, value)))
    ordered = sorted(boundaries)
    candidates = ordered + [(a+b)/2 for a, b in zip(ordered, ordered[1:])]
    accepted = []
    for origin in candidates:
        valid = True
        for i, (t, a) in enumerate(zip(traces, actors)):
            elapsed = origin + i * step
            expected = {(CAST, frame_at(elapsed, CAST_ENDS))} if elapsed < 600 - 1e-7 else {(IDLE, 0)}
            # IEEE-double accumulation can sample500ms infinitesimally below its
            # internal half-open boundary. Do not grant a whole-frame tolerance
            # or relax the source600ms/receipt800ms lifecycle endpoints.
            for index, edge in enumerate(CAST_ENDS[:-1]):
                if abs(elapsed - edge) < 1e-7:
                    expected.update({(CAST, index), (CAST, index+1)})
            if (a['art'], a['frame']) not in expected or t['presenting'] != (elapsed < 800 - 1e-7):
                valid = False
                break
        if valid:
            accepted.append(origin)
    return sorted(set(accepted))


def verify_trace(kind, traces):
    require(kind in KINDS and bool(traces), 'Unknown kind or empty trace')
    idle = kind.startswith('idle-')
    stride = 3 if idle else 1
    mode = 'idle' if idle else 'motion'
    require(all(t['captureMode'] == mode and t['captureStride'] == stride for t in traces), 'Wrong capture mode/stride')
    require(all(t['observedFrame'] == (i+1)*stride for i, t in enumerate(traces)), 'Observed frames do not advance by exact stride')
    require(all(b['processFrame'] - a['processFrame'] == stride for a, b in zip(traces, traces[1:])), 'Process-frame stride changed')
    require(all(b['elapsedMs'] >= a['elapsedMs'] for a, b in zip(traces, traces[1:])), 'Wall-clock observations run backward')
    actors = [actor4(t) for t in traces]
    mirrored = kind != 'idle-right'
    for a in actors:
        require(a['position'] == actors[0]['position'] and a['supportPlane'] == actors[0]['supportPlane'], 'Keeper root/support moved')
        require(a['position'] == a['supportPlane'], 'Keeper left its support plane')
        require(a['mirrored'] is mirrored, 'Keeper facing differs from requested native direction')
        require(a['art'] in ((IDLE,) if idle else (CAST, IDLE)), 'Unexpected keeper appearance')
        scale = .0792 if a['art'] == IDLE else .0921053
        require(a['scale'] == [-scale if mirrored else scale, scale], 'Keeper scale is not the accepted uniform scale')
        offsets = IDLE_OFFSETS if a['art'] == IDLE else CAST_OFFSETS
        require(type(a['frame']) is int and 0 <= a['frame'] < len(offsets), 'Invalid keeper pose index')
        require(a['offset'] == offsets[a['frame']], 'Keeper frame registration changed')
    step = 1000 * stride / 60
    transitions = [{'observation': i, 'art': a['art'], 'frame': a['frame']}
                   for i, a in enumerate(actors) if i == 0 or (a['art'], a['frame']) != (actors[i-1]['art'], actors[i-1]['frame'])]
    result = {'observations': len(traces), 'captureStride': stride, 'simulationFramesPerSecond': 60,
              'videoFramesPerSecond': 60 // stride, 'simulationSecondsFirstToLast': (len(traces)-1)*stride/60,
              'actorPoseTransitions': {'4': transitions}, 'position': actors[0]['position'],
              'supportPlane': actors[0]['supportPlane'], 'mirrored': mirrored}
    if idle:
        require(len(traces) == 103, 'Idle capture must contain103 complete observations')
        require(all(t['presenting'] is False for t in traces), 'Idle capture entered a presentation')
        frames = [a['frame'] for a in actors]
        require(set(frames) == {0, 1, 2, 3}, 'Idle capture does not include all four poses')
        compatible = phase_intervals(frames, step)
        require(bool(compatible), 'Idle pose sequence does not fit700/500/500/700ms over2400ms')
        wraps = [i for i in range(1, len(frames)) if frames[i-1] == 3 and frames[i] == 0]
        require(len(wraps) >= 2 and result['simulationSecondsFirstToLast'] > 4.8, 'Idle does not show two loop wraps')
        ids = [a['id'] for a in traces[0]['actors']]
        require(len(ids) == len(set(ids)), 'Duplicate actor IDs')
        reference = {a['id']: (a['position'], a['supportPlane']) for a in traces[0]['actors']}
        require(all({a['id']: (a['position'], a['supportPlane']) for a in t['actors']} == reference for t in traces),
                'An idle scene actor moved or its membership changed')
        result.update(initialPhaseCompatibleIntervalsMs=compatible, wrapObservations=wraps)
    else:
        require(all(t['presenting'] is True for t in traces[:-1]) and traces[-1]['presenting'] is False,
                'Recovery must capture through its first completed receipt observation')
        require([(t['art'], t['frame']) for t in transitions] == [(CAST, i) for i in range(6)] + [(IDLE, 0)],
                'Recovery must include all six cast poses followed by declared idle0')
        compatible = finite_timing_candidates(traces, actors, step)
        require(bool(compatible), 'Recovery does not fit source600ms and recipient/receipt800ms timing at fixed60Hz')
        result.update(firstObservationActionTimeCandidatesMs=compatible,
                      recoveryObservation=next(i for i, a in enumerate(actors) if a['art'] == IDLE),
                      completionObservation=len(traces)-1,
                      sourceRecoveryMs=600, recipientCompletionMs=800,
                      timingResolutionMs=step,
                      effectTraceLimitation='Trace records presenting and actor poses, not recipient FX or opacity.')
    return result


def verify_identity(source_lines, hashes):
    require(source_lines == [SOURCE, GODOT_TREE, TOOLS_TREE], 'Native source/tree identity differs from tested build')
    require(hashes == ASSEMBLIES, 'Native assembly hash evidence differs from frozen tested build')


def read_hashes(path):
    result = {}
    for line in path.read_text().splitlines():
        digest, name = line.split(maxsplit=1)
        name = Path(name).name
        require(name not in result, 'Duplicate assembly hash name')
        result[name] = digest
    return result


def verify_rgb(pixels, expected):
    require(sha(pixels) == expected, 'Decoded RGB pixels differ from original capture hash')


def verify_video(path, frames, fps, proof=None):
    probe = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-select_streams', 'v:0',
        '-show_entries', 'stream=width,height,avg_frame_rate,nb_frames', '-of', 'json', str(path)]))['streams'][0]
    require((probe['width'], probe['height']) == VIEWPORT, 'Video viewport differs')
    require(Fraction(probe['avg_frame_rate']) == fps and int(probe['nb_frames']) == len(frames), 'Video fps/frame count differs')
    stills = {}
    sheets = []
    if proof is not None:
        for name, index in proof.get('stills', {}).items():
            require(type(index) is int and 0 <= index < len(frames), 'Invalid still observation')
            stills.setdefault(index, []).append(path.parent / name)
        for spec in proof.get('diagnosticSheets', []):
            require(all(type(i) is int and 0 <= i < len(frames) for i in spec['indices']), 'Invalid sheet observation')
            require(type(spec['scale']) is int and spec['scale'] > 0 and spec['columns'] > 0, 'Invalid diagnostic layout')
            sheets.append((spec, Image.open(path.parent / spec['file']).convert('RGB')))
    decoder = subprocess.Popen(['ffmpeg', '-v', 'error', '-i', str(path), '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-'], stdout=subprocess.PIPE)
    try:
        for index, frame in enumerate(frames):
            pixels = decoder.stdout.read(VIEWPORT[0]*VIEWPORT[1]*3)
            require(len(pixels) == VIEWPORT[0]*VIEWPORT[1]*3, 'Video ended before all observations')
            verify_rgb(pixels, frame['rgbSha256'])
            image = Image.frombytes('RGB', VIEWPORT, pixels)
            for still in stills.get(index, []):
                require(sha(still.read_bytes()) == frame['pngSha256'], 'Native still PNG bytes changed')
                with Image.open(still) as original:
                    require(original.size == VIEWPORT and original.mode == 'RGBA', 'Native still changed format')
                    require(ImageChops.difference(original.convert('RGB'), image).getbbox() is None, 'Native still RGB changed')
            for spec, sheet in sheets:
                for tile, observation in enumerate(spec['indices']):
                    if observation != index:
                        continue
                    box, scale = spec['crop'], spec['scale']
                    require(0 <= box[0] < box[2] <= VIEWPORT[0] and 0 <= box[1] < box[3] <= VIEWPORT[1], 'Diagnostic crop outside viewport')
                    width, height = (box[2]-box[0])*scale, (box[3]-box[1])*scale
                    expected = image.crop(box).resize((width, height), Image.Resampling.NEAREST)
                    label = spec.get('labelHeight', 24)
                    x, y = tile % spec['columns']*width, tile // spec['columns']*(height+label)+label
                    require(ImageChops.difference(expected, sheet.crop((x, y, x+width, y+height))).getbbox() is None, 'Diagnostic tile differs from nearest-neighbor source crop')
        require(not decoder.stdout.read(1) and decoder.wait() == 0, 'Video contains extra frames or decoding failed')
    finally:
        if decoder.poll() is None:
            decoder.kill()
            decoder.wait()
    return sha(path.read_bytes())


def verify(args):
    proof = load(args.proof) if args.proof else None
    if args.capture_dir:
        jsons = sorted(args.capture_dir.glob('frame-*.json'))
        pngs = sorted(args.capture_dir.glob('frame-*.png'))
        require(len(jsons) == len(pngs) > 0, 'Capture PNG/trace count differs or capture is empty')
        require([p.stem for p in pngs] == [f'frame-{i:03d}' for i in range(len(pngs))], 'Capture PNG sequence is incomplete')
        require([p.stem for p in jsons] == [p.stem for p in pngs], 'Capture PNG/trace names differ')
        traces, frames = [load(p) for p in jsons], []
        for p, j in zip(pngs, jsons):
            with Image.open(p) as image:
                require(image.size == VIEWPORT and image.mode == 'RGBA', 'Native capture is not960×640RGBA')
                pixels = image.convert('RGB').tobytes()
            frames.append({'file': p.name, 'pngSha256': sha(p.read_bytes()), 'rgbSha256': sha(pixels), 'traceSha256': sha(j.read_bytes())})
    else:
        require(proof is not None and args.traces is not None and args.video is not None,
                'Archive mode requires proof, JSONL traces and video')
        lines = args.traces.read_bytes().splitlines()
        traces = [json.loads(line) for line in lines]
        frames = proof['frames']
        require(len(lines) == len(frames), 'Archive trace hash count differs')
        require(all(sha(line) == frame['traceSha256'] for line, frame in zip(lines, frames)), 'Original native trace bytes differ')
        require(len(frames) == len(traces), 'Archive frame/trace counts differ')
        require([f['file'] for f in frames] == [f'frame-{i:03d}.png' for i in range(len(frames))], 'Archive frame indices differ')
    timeline = verify_trace(args.kind, traces)
    evidence_root = args.source.parent if args.source else args.proof.parent
    native_exit = args.native_exit or evidence_root / 'native.exit'
    session_log = args.session_log or evidence_root / 'native.log'
    require(native_exit.read_text().strip() == '0', 'Native process did not exit successfully')
    session = session_log.read_text()
    completion = f"; {len(traces)} saved frames over {traces[-1]['observedFrame']} rendered frames, stride {timeline['captureStride']}."
    require(completion in session, 'Native session lacks matching completed capture record')
    require(not any(line.startswith(('ERROR:', 'SCRIPT ERROR:')) or 'ObjectDB instances leaked' in line
                    or 'resources still in use at exit' in line for line in session.splitlines()), 'Native session contains error/leak diagnostics')
    if args.source and args.hashes:
        source_lines, hashes = args.source.read_text().splitlines(), read_hashes(args.hashes)
    else:
        require(proof is not None, 'Raw verification requires source and assembly-hash evidence')
        source_lines = [proof['sourceCommit'], proof['godotSourceTree'], proof['toolsSourceTree']]
        hashes = proof['assemblySha256']
    verify_identity(source_lines, hashes)
    expected_input_path = args.legal_root / f'{args.kind}-input.json'
    expected_output_path = (args.legal_root / 'source-package/heal-left/after.json'
                            if args.kind == 'recovery-left' else expected_input_path)
    saves = verify_saves(args.kind, load(args.input_save), load(args.output_save),
                         load(expected_input_path), load(expected_output_path))
    result = {'passed': True, 'kind': args.kind, 'sourceCommit': SOURCE, 'godotSourceTree': GODOT_TREE,
              'toolsSourceTree': TOOLS_TREE, 'assemblySha256': hashes, 'viewport': list(VIEWPORT),
              **timeline, **saves, 'inputSaveSha256': sha(args.input_save.read_bytes()),
              'outputSaveSha256': sha(args.output_save.read_bytes()),
              'expectedInputSha256': sha(expected_input_path.read_bytes()),
              'expectedOutputSha256': sha(expected_output_path.read_bytes()),
              'frames': frames, 'nativeProcessExitCode': 0, 'nativeSessionSha256': sha(session_log.read_bytes()),
              'rawTraceRecordsOpacity': False, 'realTimePerformanceClaim': False}
    if args.video:
        result['videoSha256'] = verify_video(args.video, frames, timeline['videoFramesPerSecond'], proof)
        result['losslessDecodedRgbComparison'] = 'Every decoded RGB frame equals the original native PNG RGB hash; PNG alpha is not preserved by RGB video.'
    if proof:
        for name, expected_hash in proof.get('sourceFileSha256', {}).items():
            require(sha((args.proof.parent / name).read_bytes()) == expected_hash, f'Archived source evidence bytes changed: {name}')
        for name, record in proof.get('additionalStills', {}).items():
            require(sha((args.proof.parent / name).read_bytes()) == record['pngSha256'], f'Additional native still bytes changed: {name}')
        for record in proof.get('diagnosticSheets', []):
            if 'sha256' in record:
                require(sha((args.proof.parent / record['file']).read_bytes()) == record['sha256'], 'Diagnostic sheet bytes changed')
        if args.traces:
            require(sha(args.traces.read_bytes()) == proof['traceSha256'], 'Complete JSONL trace bytes changed')
        for key in ('kind', 'sourceCommit', 'godotSourceTree', 'toolsSourceTree', 'assemblySha256', 'viewport',
                    'observations', 'captureStride', 'simulationFramesPerSecond', 'videoFramesPerSecond',
                    'simulationSecondsFirstToLast', 'nativeProcessExitCode', 'nativeSessionSha256',
                    'rawTraceRecordsOpacity', 'realTimePerformanceClaim',
                    'inputSaveSha256', 'outputSaveSha256', 'frames'):
            require(typed_equal(result[key], proof[key]), f'Archived proof field differs: {key}')
        require(result['actorPoseTransitions']['4'] == proof['actorPoseTransitions']['4'], 'Archived keeper pose transitions differ')
        if args.video:
            require(result['videoSha256'] == proof['videoSha256'], 'Video bytes differ from archived proof')
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--kind', choices=KINDS, required=True)
    parser.add_argument('--capture-dir', type=Path)
    parser.add_argument('--proof', type=Path)
    parser.add_argument('--traces', type=Path)
    parser.add_argument('--video', type=Path)
    parser.add_argument('--native-exit', type=Path)
    parser.add_argument('--session-log', type=Path)
    parser.add_argument('--source', type=Path)
    parser.add_argument('--hashes', type=Path)
    parser.add_argument('--input-save', type=Path, required=True)
    parser.add_argument('--output-save', type=Path, required=True)
    parser.add_argument('--legal-root', type=Path, default=LEGAL)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    result = verify(args)
    text = json.dumps(result, indent=2) + '\n'
    if args.output:
        with args.output.open('x') as destination:
            destination.write(text)
    print(text, end='')


if __name__ == '__main__':
    main()
