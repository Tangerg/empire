#!/usr/bin/env python3
"""Package completed native captures without altering game frames or game state.

Requires Python 3, Pillow, ffmpeg and ffprobe. Raw frame PNGs remain outside Git.
The output contains a lossless RGB movie, every original trace, selected untouched
PNG screenshots, labeled nearest-neighbor diagnostic sheets and provenance.
"""
import argparse
import importlib.util
from types import SimpleNamespace
from fractions import Fraction
import hashlib
import json
from pathlib import Path
import shutil
import shlex
import subprocess

from PIL import Image, ImageDraw

KINDS = ('hit-right', 'hit-left')
ASSEMBLIES = ('Empire.dll', 'Empire.Core.dll', 'GodotSharp.dll')
VIEWPORT = (960, 640)
HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('native_evidence', HERE / 'verify-native-evidence.py')
v = importlib.util.module_from_spec(spec)
spec.loader.exec_module(v)


def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def file_sha(path):
    return sha(path.read_bytes())


def read_hashes(path):
    result = {}
    for line in path.read_text().splitlines():
        digest, filename = line.split(maxsplit=1)
        name = Path(filename).name
        require(name not in result, f'Duplicate assembly name: {name}')
        result[name] = digest
    return result


def transitions(traces):
    ids = sorted({actor['id'] for trace in traces for actor in trace['actors']})
    result = {}
    for actor_id in ids:
        steps = []
        previous = object()
        for index, trace in enumerate(traces):
            actor = next((a for a in trace['actors'] if a['id'] == actor_id), None)
            key = (actor['art'], actor['frame']) if actor else (None, None)
            if key != previous:
                steps.append({'observation': index, 'art': key[0], 'frame': key[1]})
                previous = key
        result[str(actor_id)] = steps
    return result


def encode_and_compare(source, files, output, fps):
    video = output / 'native-lossless.mp4'
    command = ['ffmpeg', '-v', 'error', '-y', '-framerate', str(fps),
               '-i', str(source / 'frame-%03d.png'), '-frames:v', str(len(files)),
               '-c:v', 'libx264rgb', '-crf', '0', '-preset', 'veryslow',
               '-threads', '2', '-pix_fmt', 'rgb24', str(video)]
    subprocess.run(command, check=True)
    probe = json.loads(subprocess.check_output([
        'ffprobe', '-v', 'error', '-select_streams', 'v:0', '-show_entries',
        'stream=codec_name,pix_fmt,width,height,avg_frame_rate,nb_frames,duration',
        '-of', 'json', str(video)]))['streams'][0]
    require((probe['width'], probe['height']) == VIEWPORT, 'Encoded viewport differs')
    require(Fraction(probe['avg_frame_rate']) == fps, 'Encoded frame rate differs')
    require(int(probe['nb_frames']) == len(files), 'Encoded frame count differs')
    decoder = subprocess.Popen(['ffmpeg', '-v', 'error', '-threads', '2', '-i',
                                str(video), '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-'],
                               stdout=subprocess.PIPE)
    records = []
    try:
        for frame in files:
            with Image.open(frame) as image:
                require(image.size == VIEWPORT and image.mode == 'RGBA',
                        f'Unexpected native PNG dimensions or mode: {frame.name}')
                pixels = image.convert('RGB').tobytes()
            require(decoder.stdout.read(len(pixels)) == pixels,
                    f'Lossless RGB decode differs: {frame.name}')
            records.append({'file': frame.name, 'pngSha256': file_sha(frame),
                            'rgbSha256': sha(pixels),
                            'traceSha256': file_sha(frame.with_suffix('.json'))})
        require(not decoder.stdout.read(1), 'Video contains additional frames')
        require(decoder.wait() == 0, 'Video decoder failed')
    finally:
        if decoder.poll() is None:
            decoder.kill()
            decoder.wait()
        decoder.stdout.close()
    return video, records, probe


def diagnostic_sheet(output, files, actors, filename, indices, crop, columns):
    scale, label_height = 3, 32
    width, height = (crop[2] - crop[0]) * scale, (crop[3] - crop[1]) * scale
    canvas = Image.new('RGB', (columns * width,
                       ((len(indices) + columns - 1) // columns) * (height + label_height)),
                       (30, 34, 39))
    draw = ImageDraw.Draw(canvas)
    for tile, observation in enumerate(indices):
        with Image.open(files[observation]) as image:
            pixels = image.convert('RGB').crop(crop).resize((width, height), Image.Resampling.NEAREST)
        x = tile % columns * width
        y = tile // columns * (height + label_height)
        canvas.paste(pixels, (x, y + label_height))
        actor = actors[observation]
        phase = 'idle' if actor['art'] == 'gravekeeper-breathe-v1' else 'recoil'
        draw.text((x + 4, y + 3), f'Observation {observation:03d}', fill='white')
        draw.text((x + 4, y + 16), f'Keeper 4: {phase} {actor["frame"]}', fill='white')
    canvas.save(output / filename)
    # Reopen the archive and compare every tile with its actual native source.
    with Image.open(output / filename) as archived:
        for tile, observation in enumerate(indices):
            with Image.open(files[observation]) as original:
                expected = original.convert('RGB').crop(crop).resize((width, height), Image.Resampling.NEAREST)
            x = tile % columns * width
            y = tile // columns * (height + label_height) + label_height
            require(archived.crop((x, y, x + width, y + height)).tobytes() == expected.tobytes(),
                    f'Diagnostic crop differs from native pixels: {filename}, {observation}')
    return {'file': filename, 'crop': list(crop), 'indices': indices,
            'columns': columns, 'scale': scale, 'labelHeight': label_height,
            'sha256': file_sha(output / filename)}


def readme(proof, crop):
    steps = ', '.join(f'{x["observation"]:03d}: {x["art"]} / {x["frame"]}' for x in proof['actorPoseTransitions']['4'])
    return f'''# Actual native gravekeeper {proof['kind']}

This archive contains {proof['observations']} actual 960×640 native framebuffer observations at stride 1, from a completed motion capture. Every observation is encoded once at 60 fps. The first-to-last span is {proof['simulationSecondsFirstToLast']:.6g} simulation seconds. The container duration is {proof['videoDurationSeconds']:.6g} s, including the last frame display interval. Readback affects pacing. Neither elapsed trace timestamps nor this movie claim wall-clock performance.

The legal witness contains one AI archer 18 movement step, a 33-damage nonfatal hit on keeper 4 (37→4 HP), and the normal next AI endTurn. The complete F5 output equals that independently engine-produced settled output except for exactly campaign.savedAt and battle.savedAt. Both timestamps changed. Source contract durations are 110 ms per AI movement cell, 320 ms source release, 600 ms source recovery, 320 ms recoil and 420 ms impact. The first captured frame can occur after movement starts. Recorded samples are checked against recoil/impact boundaries at 60 Hz; they do not measure exact sub-frame timing or effect opacity.

## Evidence and inspection

- native-lossless.mp4 is lossless RGB H.264. Every decoded RGB frame was compared byte-for-byte with the original native PNG RGB buffer. PNG alpha is not preserved by RGB video. Original raw PNG hashes remain in native-proof.json; the original complete PNG sequence remains outside Git.
- native-frames.jsonl preserves every original native JSON object byte-for-byte, in order, with one added LF separator per object. Each original trace hash and the complete JSONL hash are verified. It records actor art/frame, facing, root/support, observation counters and presenting, but not effect opacity or event dispatch.
- native-observation-*.png are untouched full-frame source PNGs. native-startup.png is an untouched separate TITLE SCREEN capture, not a settled battlefield screenshot.
- native-keeper-*-3x.png sheets contain only exact nearest-neighbor 3× crops plus labels outside each tile. Full-frame crop box: {list(crop)}. They are diagnostic inspection aids, not altered game views.
- native.log, native.exit, input/output saves, native-source.txt and assembly manifests are copied byte-for-byte. Source logs can retain original engineering workspace paths for provenance. Reproduction scripts do not depend on those paths.
- native-proof.json records all file hashes, original PNG/RGB/trace hashes, still indices, crop geometry, observed transitions and source identity. frozen-identity.json is the separately frozen expected identity. SHA-256 manifests detect content changes relative to that trusted anchor; they do not establish authenticity against wholesale replacement of both evidence and its trusted anchor.

Keeper transitions by zero-based observation: {steps}

## Completed UI route

{proof['nativeUiRoute']}

This packaging step does not establish visual acceptance. A reviewer must inspect the actual video/stills and live UI before claiming clarity, visibility or artistic acceptance.

## Reproduce and verify

From the directory containing the three Python scripts, use Python 3, Pillow, ffmpeg (libx264rgb), and ffprobe:

```sh
python package-native.py --repo "$REPO" --run-dir "$RUN" --capture-dir "$CAPTURE" \
  --assemblies "$ASSEMBLIES" --identity frozen-identity.json --legal-root "$LEGAL" \
  --kind {proof['kind']} --crop {crop[0]} {crop[1]} {crop[2]} {crop[3]} --route "$CONFIRMED_ROUTE" --output "$NEW_EMPTY_OUTPUT"
python verify-native-evidence.py --kind {proof['kind']} --identity frozen-identity.json \
  --proof "$ARCHIVE/native-proof.json" --traces "$ARCHIVE/native-frames.jsonl" \
  --video "$ARCHIVE/native-lossless.mp4" --input-save "$ARCHIVE/native-input.json" \
  --output-save "$ARCHIVE/native-output.json" --legal-root "$LEGAL"
python test-native-evidence.py
```

Only completed observations are packaged. No game is launched, imported or built, and no save/game state is edited. Use an empty output directory. All script inputs are explicit parameters or relative paths.
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('repo','run-dir','capture-dir','assemblies','output'):
        parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--identity',type=Path,default=HERE/'frozen-identity.json')
    parser.add_argument('--legal-root',type=Path,required=True)
    parser.add_argument('--kind',choices=KINDS,required=True)
    parser.add_argument('--crop',type=int,nargs=4,required=True)
    parser.add_argument('--route',required=True,help='Exact confirmed route for this completed native run')
    args = parser.parse_args()
    require(args.route.strip(), 'A confirmed native UI route is required')
    require(not args.output.exists() or (args.output.is_dir() and not any(args.output.iterdir())), 'Output must be empty; do not overwrite evidence')
    require(0 <= args.crop[0] < args.crop[2] <= VIEWPORT[0] and 0 <= args.crop[1] < args.crop[3] <= VIEWPORT[1], 'Crop outside viewport')
    source_lines = (args.run_dir/'native-source.txt').read_text().splitlines()
    require(len(source_lines) == 3, 'Source identity must contain commit and two trees')
    actual = subprocess.check_output(['git','rev-parse',source_lines[0],f'{source_lines[0]}:godot',f'{source_lines[0]}:tools'],cwd=args.repo,text=True).splitlines()
    require(actual == source_lines, 'Git source trees do not match the recorded commit')
    hashes = read_hashes(args.run_dir/'native-assemblies-sha256.txt')
    require(set(hashes) == set(ASSEMBLIES), 'Unexpected frozen DLL manifest')
    require(hashes == {name:file_sha(args.assemblies/name) for name in ASSEMBLIES}, 'Frozen DLL bytes differ')
    comparison = json.loads((args.run_dir/'save-comparison.json').read_text())
    require(comparison['passed'] is True and comparison['completeSaveEqualExceptTwoWriterTimestamps'] is True and comparison['freshOfficialSaveTimestamps'] is True, 'Official save comparison has not passed')
    verify_args = SimpleNamespace(kind=args.kind, identity=args.identity, capture_dir=args.capture_dir,
        proof=None,traces=None,video=None,native_exit=args.run_dir/'native.exit',session_log=args.run_dir/'native.log',
        source=args.run_dir/'native-source.txt',hashes=args.run_dir/'native-assemblies-sha256.txt',
        input_save=args.run_dir/'native-input.json',output_save=args.run_dir/'native-output.json',legal_root=args.legal_root)
    proof = v.verify(verify_args)
    files = sorted(args.capture_dir.glob('frame-*.png'))
    raw = [p.with_suffix('.json').read_bytes() for p in files]
    require(all(b'\n' not in data and b'\r' not in data for data in raw), 'Original trace contains newline; cannot silently normalize')
    traces = [json.loads(data) for data in raw]
    actors = [next(a for a in t['actors'] if a['id'] == 4) for t in traces]
    args.output.mkdir(parents=True,exist_ok=True)
    video,records,probe = encode_and_compare(args.capture_dir,files,args.output,60)
    require(records == proof['frames'], 'Raw verification and encode hashes differ')
    (args.output/'native-frames.jsonl').write_bytes(b''.join(data+b'\n' for data in raw))
    pose_indices = []
    seen = set()
    for i,actor in enumerate(actors):
        key = (actor['art'],actor['frame'])
        if key not in seen:
            seen.add(key); pose_indices.append(i)
    transition_indices = sorted({0,len(files)-1,*(x['observation'] for x in proof['actorPoseTransitions']['4'])})
    still_indices = sorted({*transition_indices,*(max(0,i-1) for i in transition_indices),*pose_indices})
    stills = {}
    for i in still_indices:
        name = f'native-observation-{i:03d}.png'
        shutil.copy2(files[i],args.output/name)
        require(file_sha(args.output/name) == records[i]['pngSha256'], 'Copied still differs')
        stills[name] = i
    sheets = [diagnostic_sheet(args.output,files,actors,name,indices,args.crop,columns)
        for name,indices,columns in [('native-keeper-poses-3x.png',pose_indices,4),
            ('native-keeper-transitions-3x.png',transition_indices,6),
            ('native-keeper-all-observations-3x.png',list(range(len(files))),10)]]
    source_files = ('native-input.json','native-output.json','native-source.txt','native-assemblies-sha256.txt',
                    'native.log','native.exit','save-comparison.json')
    original_hashes = {}
    for name in source_files:
        shutil.copy2(args.run_dir/name,args.output/name)
        original_hashes[name] = file_sha(args.run_dir/name)
        require(file_sha(args.output/name) == original_hashes[name], f'Provenance bytes differ: {name}')
    (args.output/'native-assemblies.sha256').write_text(''.join(f'{hashes[name]}  {name}\n' for name in ASSEMBLIES))
    startup = args.run_dir/'capture/native-001.png'
    require(startup.is_file(), 'Separate native title-screen startup capture is missing')
    shutil.copy2(startup,args.output/'native-startup.png')
    proof.update(videoDurationSeconds=len(files)/60,videoSha256=file_sha(video),videoProbe=probe,
        traceSha256=file_sha(args.output/'native-frames.jsonl'), captureDirectory=args.capture_dir.name,
        nativeUiRoute=args.route, captureScope='Observed native AI movement/shot/keeper recoil window through the first completed receipt observation.',
        sourceFileSha256={name:file_sha(args.output/name) for name in (*source_files,'native-assemblies.sha256')},
        originalSourceFileSha256=original_hashes,
        additionalStills={'native-startup.png':{'source':'capture/native-001.png','description':'Automatic native startup TITLE SCREEN capture','pngSha256':file_sha(startup)}},
        stills=stills,diagnosticSheets=sheets,
        losslessDecodedRgbComparison='Every decoded RGB buffer equals original native PNG RGB bytes; PNG alpha is not preserved in RGB video.',
        visualAcceptance='Not asserted by the packager; requires separate review of actual files and live UI.')
    (args.output/'native-proof.json').write_text(json.dumps(proof,indent=2)+'\n')
    (args.output/'README.md').write_text(readme(proof,args.crop))
    archive_args = SimpleNamespace(**vars(verify_args))
    archive_args.capture_dir = None
    archive_args.proof = args.output/'native-proof.json'
    archive_args.traces = args.output/'native-frames.jsonl'
    archive_args.video = video
    archive_args.input_save = args.output/'native-input.json'
    archive_args.output_save = args.output/'native-output.json'
    archive_args.source = args.output/'native-source.txt'
    archive_args.hashes = args.output/'native-assemblies-sha256.txt'
    archive_args.native_exit = args.output/'native.exit'
    archive_args.session_log = args.output/'native.log'
    (args.output/'artifact-sha256.txt').write_text(''.join(f'{file_sha(p)}  {p.name}\n' for p in sorted(args.output.iterdir()) if p.is_file()))
    v.verify(archive_args)
    print(f'PASS: {args.kind}: {len(files)} original motion frames; raw/archive verification, complete save, lossless RGB and untouched crops verified. Visual acceptance not asserted.')


if __name__ == '__main__':
    main()
