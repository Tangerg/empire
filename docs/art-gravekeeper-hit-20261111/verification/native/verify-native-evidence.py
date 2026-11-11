#!/usr/bin/env python3
"""Read-only verification of actual gravekeeper hit observations and official saves.

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
IDENTITY = ROOT / 'frozen-identity.json'
IDLE = 'gravekeeper-breathe-v1'
HIT = 'gravekeeper-recoil-v1'
SHOT = 'archer-shot-v1'
IDLE_OFFSETS = ([-231, -480], [-231, -482], [-231, -482], [-231, -480])
HIT_ENDS = (40, 150, 240, 320)
SHOT_ENDS = (100, 200, 320, 400, 500, 600)
SHOT_OFFSETS = ([-158,-419],[-123,-422],[-139,-423],[-138,-423],[-125,-403],[-125,-417])
HIT_OFFSETS = ([-244, -553], [-282, -550], [-248, -553], [-241, -554])
VIEWPORT = (960, 640)
KINDS = ('hit-right', 'hit-left')
ASSEMBLY_NAMES = ('Empire.dll', 'Empire.Core.dll', 'GodotSharp.dll')
LEGAL_FILES = ('before.json', 'ui-settled.json', 'receipt.json', 'ui-settled-extra-actions.json')


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
    facing = 'east' if kind == 'hit-right' else 'west'
    require(before[4]['facing'] == after[4]['facing'] == facing, 'Keeper facing differs from legal hit orientation')
    require(before[4]['hp'] == 37 and after[4]['hp'] == 4, 'Legal keeper hit must be37→4HP')
    return {'completeSaveEqualExceptTwoWriterTimestamps': True,
            'freshOfficialSaveTimestamps': True, 'omittedPaths': ['campaign.savedAt', 'battle.savedAt'],
            'writerTimestamps': {k: {'input': input_times[k], 'output': output_times[k]} for k in input_times}}


def verify_comparison_report(report, expected, actual, expected_hash, actual_hash):
    require(report['passed'] is True and report['completeSaveEqualExceptTwoWriterTimestamps'] is True
            and report['freshOfficialSaveTimestamps'] is True, 'Original save comparison did not pass')
    require(report['expectedSha256'] == expected_hash and report['actualSha256'] == actual_hash, 'Save comparison file hashes differ')
    require(report['omittedPaths'] == ['$.campaign.savedAt','$.battle.savedAt'] and report['differences'] == [], 'Save comparison scope differs')
    require(set(report['timestamps']) == {'campaign.savedAt','battle.savedAt'}, 'Save comparison timestamp set differs')
    for owner in ('campaign','battle'):
        require(report['timestamps'][owner+'.savedAt'] == {'expected':expected[owner]['savedAt'],'actual':actual[owner]['savedAt'],
                'changed':True} and expected[owner]['savedAt'] != actual[owner]['savedAt'], 'Save comparison timestamps differ')


def frame_at(milliseconds, ends):
    return next((i for i, end in enumerate(ends) if milliseconds < end - 1e-7), len(ends) - 1)


def actor4(trace):
    found = [a for a in trace['actors'] if a['id'] == 4]
    require(len(found) == 1, 'Every observation must contain keeper4 exactly once')
    return found[0]


def pose_transitions(traces):
    result = {}
    for actor_id in sorted(a['id'] for a in traces[0]['actors']):
        actors = [next(a for a in t['actors'] if a['id'] == actor_id) for t in traces]
        result[str(actor_id)] = [{'observation': i, 'art': a['art'], 'frame': a['frame']}
            for i, a in enumerate(actors) if i == 0 or (a['art'], a['frame']) != (actors[i-1]['art'], actors[i-1]['frame'])]
    return result


def finite_timing_candidates(traces, actors, step):
    first = next(i for i, a in enumerate(actors) if a['art'] == HIT)
    left, right = (first-1)*step, first*step
    # An onset can lie anywhere between the two sampled observations. Candidate
    # boundaries and interval interiors cover the entire finite phase space.
    boundaries = {left, right}
    source = [next(a for a in t['actors'] if a['id'] == 18) for t in traces]
    for i in range(len(traces)):
        for edge in (0, *HIT_ENDS, 420, -320, *(end-320 for end in SHOT_ENDS)):
            value = i*step - edge
            if left <= value <= right:
                boundaries.add(value)
    ordered = sorted(boundaries)
    candidates = ordered + [(a+b)/2 for a,b in zip(ordered, ordered[1:])]
    accepted = []
    for origin in candidates:
        valid = True
        for i, (trace, actor) in enumerate(zip(traces, actors)):
            shot_elapsed = i*step-origin+320
            if shot_elapsed < -1e-7:
                shot_expected = {('C01-FFV1-UNIT-ARCHER',1)}
            elif shot_elapsed < 600-1e-7:
                shot_expected = {(SHOT,frame_at(shot_elapsed,SHOT_ENDS))}
            else:
                shot_expected = {('archer-breathe-v1',0)}
            for index,edge in enumerate(SHOT_ENDS[:-1]):
                if abs(shot_elapsed-edge) < 1e-7:
                    shot_expected.update({(SHOT,index),(SHOT,index+1)})
            if (source[i]['art'],source[i]['frame']) not in shot_expected:
                valid = False
                break
            elapsed = i*step-origin
            if elapsed < -1e-7:
                expected = {(IDLE, frame) for frame in range(4)}
            elif elapsed < 320-1e-7:
                expected = {(HIT, frame_at(elapsed, HIT_ENDS))}
            else:
                expected = {(IDLE, 0)}
            for index, edge in enumerate(HIT_ENDS[:-1]):
                if abs(elapsed-edge) < 1e-7:
                    expected.update({(HIT,index), (HIT,index+1)})
            if (actor['art'],actor['frame']) not in expected or trace['presenting'] != (elapsed < 420-1e-7):
                valid = False
                break
        if valid:
            accepted.append(origin)
    return sorted(set(accepted))


def verify_trace(kind, traces):
    require(kind in KINDS and bool(traces), 'Unknown kind or empty trace')
    require(all(t['captureMode'] == 'motion' and type(t['captureStride']) is int and t['captureStride'] == 1 for t in traces), 'Wrong motion capture mode/stride')
    require(all(type(t['observedFrame']) is int and t['observedFrame'] == i+1 for i,t in enumerate(traces)), 'Observed frames are not contiguous')
    require(all(type(t['processFrame']) is int for t in traces), 'Process counter is not an integer')
    require(all(b['processFrame']-a['processFrame'] == 1 for a,b in zip(traces,traces[1:])), 'Process-frame stride changed')
    require(all(b['elapsedMs'] >= a['elapsedMs'] for a,b in zip(traces,traces[1:])), 'Wall-clock observations run backward')
    require(all(t['presenting'] is True for t in traces[:-1]) and traces[-1]['presenting'] is False,
            'Motion must end at its first completed presentation observation')
    ids = [a['id'] for a in traces[0]['actors']]
    require(4 in ids and 18 in ids and len(ids) == len(set(ids)), 'Required keeper/source IDs missing or duplicated')
    require(all(sorted(a['id'] for a in t['actors']) == sorted(ids) for t in traces), 'Actor membership changed')
    actors = [actor4(t) for t in traces]
    mirrored = kind == 'hit-left'
    for actor in actors:
        require(actor['position'] == actors[0]['position'] and actor['supportPlane'] == actors[0]['supportPlane'], 'Keeper root/support moved')
        require(actor['position'] == actor['supportPlane'], 'Keeper left its support plane')
        require(actor['mirrored'] is mirrored, 'Keeper facing differs from requested direction')
        require(actor['art'] in (HIT,IDLE), 'Unexpected keeper appearance')
        scale = .0792 if actor['art'] == IDLE else .0688
        require(actor['scale'] == [-scale if mirrored else scale,scale], 'Keeper scale changed')
        offsets = IDLE_OFFSETS if actor['art'] == IDLE else HIT_OFFSETS
        require(type(actor['frame']) is int and 0 <= actor['frame'] < len(offsets), 'Invalid keeper pose index')
        require(actor['offset'] == offsets[actor['frame']], 'Keeper registration changed')
    transitions = pose_transitions(traces)
    hits = [a for a in transitions['4'] if a['art'] == HIT]
    require([a['frame'] for a in hits] == [0,1,2,3], 'All four recoil poses must appear exactly once in order')
    first, last = hits[0]['observation'], hits[-1]['observation']
    require(first > 0 and actors[0]['art'] == IDLE, 'Missing pre-impact keeper observation')
    require(actors[-1]['art'] == IDLE and actors[-1]['frame'] == 0, 'Missing recovered idle0 endpoint')
    require(all(a['art'] == HIT for a in actors[first:last+1]), 'Recoil was interrupted or repeated')
    recovery = next(i for i in range(last+1,len(actors)) if actors[i]['art'] == IDLE)
    source = [next(a for a in t['actors'] if a['id'] == 18) for t in traces]
    require(any(a['position'] != source[0]['position'] for a in source), 'Actual one-step source movement not observed')
    require([a['frame'] for a in transitions['18'] if a['art'] == SHOT] == list(range(6)), 'Missing or reordered source shot poses')
    require(source[-1]['art'] == 'archer-breathe-v1', 'Source did not recover to breathing')
    source_start = next(i for i,a in enumerate(source) if a['art'] == SHOT)
    for i,a in enumerate(source):
        require(a['position'] == a['supportPlane'], 'Source actor left its support plane')
        if i >= source_start:
            require(a['position'] == source[-1]['position'], 'Source moved during shot/recovery')
        if a['art'] == SHOT:
            require(a['scale'] == [-.0893 if a['mirrored'] else .0893,.0893] and a['offset'] == SHOT_OFFSETS[a['frame']], 'Source shot scale/registration changed')
    step = 1000/60
    compatible = finite_timing_candidates(traces,actors,step)
    require(bool(compatible), 'Recoil/receipt samples do not fit40/110/90/80ms recoil and420ms impact')
    return {'observations':len(traces), 'captureStride':1, 'simulationFramesPerSecond':60,
        'videoFramesPerSecond':60, 'simulationSecondsFirstToLast':(len(traces)-1)/60,
        'actorPoseTransitions':transitions, 'position':actors[0]['position'],
        'supportPlane':actors[0]['supportPlane'], 'mirrored':mirrored,
        'recoilOnsetObservation':first, 'recoveryObservation':recovery, 'completionObservation':len(traces)-1,
        'recoilOnsetRelativeToFirstObservationCandidatesMs':compatible, 'timingResolutionMs':step,
        'sourceContractsMs':{'oneStepAiMove':110,'sourceRelease':320,'sourceRecovery':600,'recoil':320,'impact':420},
        'sourceShotOnsetObservation':source_start,
        'sourceShotOnsetRelativeToFirstObservationCandidatesMs':[x-320 for x in compatible],
        'effectTraceLimitation':'Trace records presenting and actor poses, not recipient FX or opacity. Source contracts are not direct sub-frame measurements.'}


def verify_identity(source_lines, hashes, identity):
    require(source_lines == [identity[k] for k in ('sourceCommit','godotSourceTree','toolsSourceTree')], 'Native source/tree identity differs from frozen tested build')
    require(set(hashes) == set(ASSEMBLY_NAMES) and hashes == identity['assemblySha256'], 'Native DLL hashes differ from frozen tested build')


def verify_legal(kind, root, identity):
    side = kind.removeprefix('hit-')
    for name in LEGAL_FILES:
        relative = f'{side}/{name}'
        require(sha((root/relative).read_bytes()) == identity['legalFileSha256'][relative], f'Legal witness bytes changed: {relative}')
    receipt = load(root/side/'receipt.json')
    require(receipt['computer'] is True, 'Hit was not an AI action')
    moves = [e for e in receipt['events'] if e['type'] == 'move']
    require(len(moves) == 1 and moves[0]['unit'] == 18 and len(moves[0]['path']) == 2, 'Legal source is not a one-step archer18 move')
    path = moves[0]['path']
    require(abs(path[0]['x']-path[1]['x'])+abs(path[0]['y']-path[1]['y']) == 1, 'Legal source step is not adjacent')
    hits = [e for e in receipt['events'] if e['type'] == 'attack']
    require(len(hits) == 1 and all(typed_equal(hits[0].get(k),v) for k,v in {'attacker':18,'defender':4,'damage':33,'killed':False,'weapon':'c01.ranger-bow'}.items()), 'Legal nonfatal33-damage keeper hit differs')
    extra = load(root/side/'ui-settled-extra-actions.json')
    require(len(extra) == 1 and extra[0]['computer'] is True and extra[0]['action'] == {'kind':'endTurn'}, 'Legal settlement is not the normal next AI endTurn')
    return root/side/'before.json', root/side/'ui-settled.json'


def read_hashes(path):
    result = {}
    for line in path.read_text().splitlines():
        digest, name = line.split(maxsplit=1)
        name = Path(name).name
        require(name not in result, 'Duplicate assembly hash name')
        result[name] = digest
    return result


def archive_path(root, name):
    require(isinstance(name,str) and Path(name).name == name and name not in ('.','..'), 'Archive path must be a plain filename')
    path = root/name
    require(not path.is_symlink(), 'Archive evidence may not be a symlink')
    return path


def verify_manifest(root):
    manifest = root/'artifact-sha256.txt'
    entries = {}
    for line in manifest.read_text().splitlines():
        digest,name = line.split(maxsplit=1)
        require(name not in entries, 'Duplicate archive manifest name')
        require(len(digest) == 64 and all(c in '0123456789abcdef' for c in digest), 'Invalid archive hash')
        entries[name] = digest
        require(sha(archive_path(root,name).read_bytes()) == digest, f'Archive artifact hash differs: {name}')
    names = {p.name for p in root.iterdir() if p.is_file() and p.name != 'artifact-sha256.txt'}
    require(names == set(entries), 'Archive manifest does not cover exactly all artifact files')


def verify_trace_bytes(raw_jsonl, frames):
    require(raw_jsonl.endswith(b'\n') and b'\r' not in raw_jsonl, 'JSONL separator format changed')
    lines = raw_jsonl[:-1].split(b'\n')
    require(len(lines) == len(frames), 'Archive trace hash count differs')
    require(all(sha(line) == frame['traceSha256'] for line,frame in zip(lines,frames)), 'Original native trace bytes differ')
    return [json.loads(line) for line in lines]


def verify_rgb(pixels, expected):
    require(sha(pixels) == expected, 'Decoded RGB pixels differ from original capture hash')


def verify_video(path, frames, fps, proof=None):
    if proof is not None:
        require(sha(path.read_bytes()) == proof['videoSha256'], 'Video bytes differ from archived proof')
    probe = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-select_streams', 'v:0',
        '-show_entries', 'stream=width,height,avg_frame_rate,nb_frames', '-of', 'json', str(path)]))['streams'][0]
    require((probe['width'], probe['height']) == VIEWPORT, 'Video viewport differs')
    require(Fraction(probe['avg_frame_rate']) == fps and int(probe['nb_frames']) == len(frames), 'Video fps/frame count differs')
    stills = {}
    sheets = []
    if proof is not None:
        for name, index in proof.get('stills', {}).items():
            require(type(index) is int and 0 <= index < len(frames), 'Invalid still observation')
            stills.setdefault(index, []).append(archive_path(path.parent,name))
        for spec in proof.get('diagnosticSheets', []):
            require(all(type(i) is int and 0 <= i < len(frames) for i in spec['indices']), 'Invalid sheet observation')
            require(type(spec['scale']) is int and spec['scale'] == 3 and type(spec['columns']) is int and spec['columns'] > 0, 'Invalid diagnostic layout')
            sheets.append((spec, Image.open(archive_path(path.parent,spec['file'])).convert('RGB')))
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
                    label = spec['labelHeight']
                    require(type(label) is int and label == 32, 'Diagnostic label geometry changed')
                    expected_size = (spec['columns']*width, ((len(spec['indices'])+spec['columns']-1)//spec['columns'])*(height+label))
                    require(sheet.size == expected_size, 'Diagnostic sheet dimensions changed')
                    x, y = tile % spec['columns']*width, tile // spec['columns']*(height+label)+label
                    require(ImageChops.difference(expected, sheet.crop((x, y, x+width, y+height))).getbbox() is None, 'Diagnostic tile differs from nearest-neighbor source crop')
        require(not decoder.stdout.read(1) and decoder.wait() == 0, 'Video contains extra frames or decoding failed')
    finally:
        if decoder.poll() is None:
            decoder.kill()
            decoder.wait()
        decoder.stdout.close()
    return sha(path.read_bytes())


def verify(args):
    identity = load(args.identity)
    proof = load(args.proof) if args.proof else None
    if proof is not None:
        verify_manifest(args.proof.parent)
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
        frames = proof['frames']
        traces = verify_trace_bytes(args.traces.read_bytes(),frames)
        require(len(frames) == len(traces), 'Archive frame/trace counts differ')
        require([f['file'] for f in frames] == [f'frame-{i:03d}.png' for i in range(len(frames))], 'Archive frame indices differ')
    timeline = verify_trace(args.kind, traces)
    evidence_root = args.source.parent if args.source else args.proof.parent
    native_exit = args.native_exit or evidence_root / 'native.exit'
    session_log = args.session_log or evidence_root / 'native.log'
    require(native_exit.read_text().strip() == '0', 'Native process did not exit successfully')
    session = session_log.read_text()
    completion = f"; {len(traces)} saved frames over {traces[-1]['observedFrame']} rendered frames, stride {timeline['captureStride']}."
    require('Native motion capture complete:' in session and completion in session, 'Native session lacks matching completed capture record')
    require(not any(line.startswith(('ERROR:', 'SCRIPT ERROR:')) or 'ObjectDB instances leaked' in line
                    or 'resources still in use at exit' in line for line in session.splitlines()), 'Native session contains error/leak diagnostics')
    if args.source and args.hashes:
        source_lines, hashes = args.source.read_text().splitlines(), read_hashes(args.hashes)
    else:
        require(proof is not None, 'Raw verification requires source and assembly-hash evidence')
        source_lines = [proof['sourceCommit'], proof['godotSourceTree'], proof['toolsSourceTree']]
        hashes = proof['assemblySha256']
    verify_identity(source_lines, hashes, identity)
    expected_input_path, expected_output_path = verify_legal(args.kind, args.legal_root, identity)
    saves = verify_saves(args.kind, load(args.input_save), load(args.output_save),
                         load(expected_input_path), load(expected_output_path))
    verify_comparison_report(load(evidence_root/'save-comparison.json'),load(expected_output_path),load(args.output_save),
        sha(expected_output_path.read_bytes()),sha(args.output_save.read_bytes()))
    result = {'passed': True, 'kind': args.kind, 'sourceCommit': source_lines[0], 'godotSourceTree': source_lines[1],
              'toolsSourceTree': source_lines[2], 'assemblySha256': hashes, 'viewport': list(VIEWPORT),
              **timeline, **saves, 'inputSaveSha256': sha(args.input_save.read_bytes()),
              'outputSaveSha256': sha(args.output_save.read_bytes()),
              'expectedInputSha256': sha(expected_input_path.read_bytes()),
              'expectedOutputSha256': sha(expected_output_path.read_bytes()),
              'frames': frames, 'nativeProcessExitCode': 0, 'nativeSessionSha256': sha(session_log.read_bytes()),
              'nativeExitSha256': sha(native_exit.read_bytes()), 'identitySha256': sha(args.identity.read_bytes()),
              'rawTraceRecordsOpacity': False, 'realTimePerformanceClaim': False}
    if args.video:
        result['videoSha256'] = verify_video(args.video, frames, timeline['videoFramesPerSecond'], proof)
        result['losslessDecodedRgbComparison'] = 'Every decoded RGB frame equals the original native PNG RGB hash; PNG alpha is not preserved by RGB video.'
    if proof:
        require(set(proof['sourceFileSha256']) == {'native-input.json','native-output.json','native-source.txt','native-assemblies-sha256.txt','native-assemblies.sha256','native.log','native.exit','save-comparison.json'}, 'Required provenance hash set differs')
        require(proof['originalSourceFileSha256'] == {k:h for k,h in proof['sourceFileSha256'].items() if k != 'native-assemblies.sha256'}, 'Archived raw provenance hash set differs')
        require(set(proof['additionalStills']) == {'native-startup.png'}, 'Required startup screenshot differs')
        require(len(proof['stills']) > 0 and len(proof['diagnosticSheets']) == 3, 'Required inspection files missing')
        for name, expected_hash in proof.get('sourceFileSha256', {}).items():
            require(sha(archive_path(args.proof.parent,name).read_bytes()) == expected_hash, f'Archived source evidence bytes changed: {name}')
        for name, record in proof.get('additionalStills', {}).items():
            require(sha(archive_path(args.proof.parent,name).read_bytes()) == record['pngSha256'], f'Additional native still bytes changed: {name}')
        for record in proof.get('diagnosticSheets', []):
            if 'sha256' in record:
                require(sha(archive_path(args.proof.parent,record['file']).read_bytes()) == record['sha256'], 'Diagnostic sheet bytes changed')
        if args.traces:
            require(sha(args.traces.read_bytes()) == proof['traceSha256'], 'Complete JSONL trace bytes changed')
        for key in ('kind', 'sourceCommit', 'godotSourceTree', 'toolsSourceTree', 'assemblySha256', 'viewport',
                    'nativeExitSha256', 'identitySha256', 'expectedInputSha256', 'expectedOutputSha256',
                    'observations', 'captureStride', 'simulationFramesPerSecond', 'videoFramesPerSecond',
                    'simulationSecondsFirstToLast', 'nativeProcessExitCode', 'nativeSessionSha256',
                    'rawTraceRecordsOpacity', 'realTimePerformanceClaim',
                    'inputSaveSha256', 'outputSaveSha256', 'frames'):
            require(typed_equal(result[key], proof[key]), f'Archived proof field differs: {key}')
        require(result['actorPoseTransitions'] == proof['actorPoseTransitions'], 'Archived actor pose transitions differ')
        for key in ('recoilOnsetObservation','recoveryObservation','completionObservation','recoilOnsetRelativeToFirstObservationCandidatesMs','timingResolutionMs','sourceContractsMs','effectTraceLimitation','sourceShotOnsetObservation','sourceShotOnsetRelativeToFirstObservationCandidatesMs'):
            require(typed_equal(result[key],proof[key]), f'Archived timing field differs: {key}')
        if args.video:
            require(result['videoSha256'] == proof['videoSha256'], 'Video bytes differ from archived proof')
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--identity', type=Path, default=IDENTITY)
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
