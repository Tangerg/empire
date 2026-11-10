#!/usr/bin/env python3
"""Package completed native captures without altering game frames or game state.

Requires Python 3, Pillow, ffmpeg and ffprobe. Raw frame PNGs remain outside Git.
The output contains a lossless RGB movie, every original trace, selected untouched
PNG screenshots, labeled nearest-neighbor diagnostic sheets and provenance.
"""
import argparse
from fractions import Fraction
import hashlib
import json
from pathlib import Path
import shutil
import shlex
import subprocess

from PIL import Image, ImageDraw

KINDS = ('idle-right', 'idle-left', 'recovery-left')
ASSEMBLIES = ('Empire.dll', 'Empire.Core.dll', 'GodotSharp.dll')
VIEWPORT = (960, 640)
ROUTES = {
    'idle-right': 'F9, Space, Space, Escape, live settled UI inspection, F8, wait for all 103 observations, F5, verify the saved banner, close the window.',
    'idle-left': 'F9, Space, Space, Space, Escape, live settled UI inspection, F8, wait for all 103 observations, F5, verify the saved banner, close the window.',
}


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
        decoder.stdout.close()
        if decoder.poll() is None:
            decoder.kill()
            decoder.wait()
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
        phase = 'idle' if actor['art'] == 'gravekeeper-breathe-v1' else 'cast'
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


def readme(proof, route, capture_name, crop):
    kind = proof['kind']
    idle = kind.startswith('idle-')
    direction = 'right-facing, unmirrored' if kind == 'idle-right' else 'left-facing, mirrored'
    route_argument = '' if idle else ' --route ' + shlex.quote(route)
    keeper = proof['actorPoseTransitions']['4']
    steps = ', '.join(f'{x["observation"]:03d}: {x["art"]} / {x["frame"]}' for x in keeper)
    if idle:
        coverage = ('All four registered breathing poses appear with the keeper root and support plane fixed. '
                    'The sequence covers more than two 2.4-second cycles and two rest-pose wraps. '
                    'It begins at the current idle phase rather than resetting the animation for capture.')
        save = ('The complete official F5 output equals the legal input after removing only '
                '`campaign.savedAt` and `battle.savedAt`. Both writer timestamps changed.')
    else:
        coverage = ('The selected action window contains all six cast poses, the restored declared idle pose 0, '
                    'and the first observation with the presentation complete. The keeper root and support plane '
                    'remain fixed. The source animation ends at 600 ms; the recipient presentation/receipt ends '
                    'at 800 ms. This sampled movie alone does not establish exact sub-frame durations.')
        save = ('The complete official F5 output equals the independently engine-produced legal heal output '
                'after removing only `campaign.savedAt` and `battle.savedAt`. Both writer timestamps changed. '
                'The legal action heals target 3 from 39 to 69 HP and leaves keeper 4 facing west.')
    return f'''# Actual native gravekeeper {kind}

This is a {direction} native UI recording from the frozen tested build. {coverage}

## Capture and timing

- Source capture: `{capture_name}`.
- {proof['observations']} actual 960 × 640 framebuffer observations; capture stride {proof['captureStride']}.
- Engine configured at 60 simulation frames per second. The original observations are encoded at {proof['videoFramesPerSecond']} frames per second, without duplicated, interpolated or synthesized frames.
- First-to-last observation span: {proof['simulationSecondsFirstToLast']:.6g} simulation seconds. Video container duration: {proof['videoDurationSeconds']:.6g} seconds, including the final frame's display interval.
- Readback affects frame pacing. Neither elapsed trace timestamps nor the movie claim wall-clock or real-time performance.

`native-lossless.mp4` uses lossless RGB H.264. Every decoded full-frame RGB buffer was compared byte-for-byte with its source PNG. The video does not preserve the PNG alpha channel. `native-proof.json` records the SHA-256 of each original PNG, decoded RGB buffer and trace, plus the movie and provenance files. The original raw PNG sequence remains outside Git.

`native-frames.jsonl` retains every original per-frame JSON object in order, with a newline separator added. It records actors, facing, positions, support planes, art/frame selection, capture/render observation counters, elapsed observations and `presenting`. It does not record opacity or recipient effect events.

## Inspection files

`native-observation-*.png` are untouched full-frame native screenshots. `native-startup.png` is the separate untouched automatic startup title-screen capture. Any settled battlefield checks before recording were live UI observations and are not archived screenshots. The labeled `native-keeper-*-3x.png` sheets are diagnostic crops from the actual observations, enlarged only by nearest-neighbor sampling. Their crop box is `{list(crop)}` in full-frame screen coordinates. These contact sheets are inspection aids, not new artwork or composited game views. The proof records every tile's source observation and crop geometry.

Keeper pose starts by zero-based observation:

{steps}

## Native route and save provenance

Normal UI route: {route}

Keyboard selection centers the camera through the game's normal behavior. No saved state, facing, camera, receipt, clock or frame was patched. The scene is the official isolated campaign save. `native-input.json`, `native-output.json`, `native-source.txt`, `native-assemblies.sha256`, `native-assemblies-sha256.txt`, `native.log`, `native.exit` and `full-save-comparison.json` preserve the completed run and its result. {save}

Source commit: `{proof['sourceCommit']}`. Godot tree: `{proof['godotSourceTree']}`. Tools tree: `{proof['toolsSourceTree']}`. The wrapper checked the three DLLs against the frozen build both before and after execution; packaging rechecked their hashes. The native exit code is 0. The log retains the software OpenGL/V-sync warning and environment catalog diagnostic rather than hiding them.

## Reproduce this archive

From this directory's parent, with the original raw capture and final DLL directory available:

```sh
python package-native.py --repo "$REPO" --run-dir "$QA/native-{kind}" \\
  --capture-dir "$QA/native-{kind}/capture/{capture_name}" \\
  --assemblies "$QA/final-assemblies" --kind {kind} \\
  --crop {crop[0]} {crop[1]} {crop[2]} {crop[3]} --output "$OUTPUT"{route_argument}
```

The recovery recipe passes its exact confirmed route. Set `OUTPUT` to an empty destination for independent reproduction. This script packages existing observations only; it does not launch, import or rebuild the game. Python 3, Pillow, ffmpeg with `libx264rgb`, and ffprobe are required. The separately maintained `../verify-native-evidence.py` independently checks the archived video, trace, identity, save and timeline.
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo', type=Path, required=True)
    parser.add_argument('--run-dir', type=Path, required=True)
    parser.add_argument('--capture-dir', type=Path, required=True)
    parser.add_argument('--assemblies', type=Path, required=True)
    parser.add_argument('--kind', choices=KINDS, required=True)
    parser.add_argument('--crop', type=int, nargs=4, required=True, metavar=('LEFT', 'TOP', 'RIGHT', 'BOTTOM'))
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--route', help='Exact route for a completed recovery run; idle routes have recorded defaults')
    args = parser.parse_args()
    idle = args.kind.startswith('idle-')
    stride = 3 if idle else 1
    fps = 60 // stride
    route = args.route or ROUTES.get(args.kind)
    require(bool(route), 'A confirmed native UI route is required')
    require((args.run_dir / 'native.exit').read_text().strip() == '0', 'Native run has not completed with exit 0')
    require(0 <= args.crop[0] < args.crop[2] <= VIEWPORT[0] and
            0 <= args.crop[1] < args.crop[3] <= VIEWPORT[1], 'Crop lies outside native viewport')
    source_lines = (args.run_dir / 'native-source.txt').read_text().splitlines()
    require(len(source_lines) == 3, 'Native source identity must contain commit, Godot tree and tools tree')
    actual_trees = subprocess.check_output(['git', 'rev-parse', source_lines[0],
        f'{source_lines[0]}:godot', f'{source_lines[0]}:tools'], cwd=args.repo, text=True).splitlines()
    require(actual_trees == source_lines, 'Native source trees do not match the recorded commit')
    hashes = read_hashes(args.run_dir / 'native-assemblies-sha256.txt')
    require(set(hashes) == set(ASSEMBLIES), 'Unexpected frozen assembly manifest')
    require(hashes == {name: file_sha(args.assemblies / name) for name in ASSEMBLIES}, 'Frozen DLL hash mismatch')
    comparison = json.loads((args.run_dir / 'full-save-comparison.json').read_text())
    require(comparison['passed'] and comparison['completeSaveEqualExceptTwoWriterTimestamps'] and
            comparison['freshOfficialSaveTimestamps'], 'Official save comparison has not passed')
    files = sorted(args.capture_dir.glob('frame-*.png'))
    require(bool(files) and [p.name for p in files] == [f'frame-{i:03d}.png' for i in range(len(files))],
            'Native frame sequence is empty or incomplete')
    require([p.stem for p in sorted(args.capture_dir.glob('frame-*.json'))] == [p.stem for p in files],
            'Native PNG/trace sequences differ')
    raw_traces = [p.with_suffix('.json').read_bytes() for p in files]
    require(all(b'\n' not in raw and b'\r' not in raw for raw in raw_traces),
            'Expected one-line original native traces without line endings')
    traces = [json.loads(raw) for raw in raw_traces]
    require(all(t['captureStride'] == stride for t in traces), 'Unexpected capture stride')
    require(all(b['processFrame'] - a['processFrame'] == stride and
                b['observedFrame'] - a['observedFrame'] == stride for a, b in zip(traces, traces[1:])),
            'Native observations are not contiguous at the declared stride')
    if idle:
        require(len(files) == 103 and all(not t['presenting'] for t in traces), 'Idle run is incomplete')
    else:
        require(all(t['presenting'] for t in traces[:-1]) and not traces[-1]['presenting'],
                'Recovery run must end at its first completed presentation observation')
    actors = [next(a for a in t['actors'] if a['id'] == 4) for t in traces]
    require(all(a['position'] == actors[0]['position'] and a['supportPlane'] == actors[0]['supportPlane']
                and a['mirrored'] == (args.kind != 'idle-right') for a in actors),
            'Keeper root, support plane or facing changed unexpectedly')
    poses = transitions(traces)
    args.output.mkdir(parents=True, exist_ok=True)
    video, records, probe = encode_and_compare(args.capture_dir, files, args.output, fps)
    (args.output / 'native-frames.jsonl').write_bytes(b''.join(raw + b'\n' for raw in raw_traces))
    phase_indices = []
    seen = set()
    for i, actor in enumerate(actors):
        key = (actor['art'], actor['frame'])
        if key not in seen:
            phase_indices.append(i)
            seen.add(key)
    phase_indices.sort(key=lambda i: (actors[i]['art'], actors[i]['frame']))
    transition_indices = sorted({0, len(files) - 1, *(p['observation'] for p in poses['4'])})
    still_indices = sorted({0, len(files) - 1, *phase_indices,
                           *(max(0, p['observation'] - 1) for p in poses['4'] if not idle)})
    stills = {}
    for i in still_indices:
        name = f'native-observation-{i:03d}.png'
        shutil.copy2(files[i], args.output / name)
        require(file_sha(args.output / name) == records[i]['pngSha256'], f'Copied PNG differs: {name}')
        stills[name] = i
    sheets = [diagnostic_sheet(args.output, files, actors, name, indices, args.crop, columns)
              for name, indices, columns in (
                  ('native-keeper-poses-3x.png', phase_indices, 4),
                  ('native-keeper-transitions-3x.png', transition_indices, 6),
                  ('native-keeper-all-observations-3x.png', list(range(len(files))), 10))]
    source_files = ('native-input.json', 'native-output.json', 'native-source.txt',
                    'native-assemblies-sha256.txt', 'native.log', 'native.exit', 'full-save-comparison.json')
    for name in source_files:
        shutil.copy2(args.run_dir / name, args.output / name)
    (args.output / 'native-assemblies.sha256').write_text(''.join(f'{hashes[name]}  {name}\n' for name in ASSEMBLIES))
    startup = args.run_dir / 'capture/native-001.png'
    require(startup.is_file(), 'Separate native startup capture is missing')
    shutil.copy2(startup, args.output / 'native-startup.png')
    proof = {
        'kind': args.kind, 'sourceCommit': source_lines[0], 'godotSourceTree': source_lines[1],
        'toolsSourceTree': source_lines[2], 'assemblySha256': hashes,
        'inputSaveSha256': file_sha(args.output / 'native-input.json'),
        'outputSaveSha256': file_sha(args.output / 'native-output.json'),
        'viewport': list(VIEWPORT), 'observations': len(files), 'captureStride': stride,
        'simulationFramesPerSecond': 60, 'videoFramesPerSecond': fps,
        'simulationSecondsFirstToLast': (traces[-1]['processFrame'] - traces[0]['processFrame']) / 60,
        'videoDurationSeconds': len(files) / fps,
        'losslessDecodedRgbComparison': f'All {len(files)} original native PNG full-frame RGB buffers exactly equal decoded video frames. PNG alpha is not preserved in RGB video.',
        'videoSha256': file_sha(video), 'videoProbe': probe, 'traceSha256': file_sha(args.output / 'native-frames.jsonl'),
        'actorPoseTransitions': poses,
        'actors': {'4': {key: actors[0][key] for key in ('position', 'supportPlane', 'mirrored')}},
        'captureScope': 'Continuous sampled native idle window across two loop wraps.' if idle else 'Complete native heal presentation from cast frame 0 through first completed receipt observation.',
        'captureDirectory': args.capture_dir.name, 'nativeUiRoute': route,
        'rawTraceRecordsOpacity': False, 'realTimePerformanceClaim': False,
        'nativeProcessExitCode': 0, 'nativeSessionSha256': file_sha(args.output / 'native.log'),
        'sourceFileSha256': {name: file_sha(args.output / name) for name in (*source_files, 'native-assemblies.sha256')},
        'additionalStills': {'native-startup.png': {'source': 'capture/native-001.png', 'description': 'Automatic native startup title-screen capture', 'pngSha256': file_sha(startup)}},
        'stills': stills, 'diagnosticSheets': sheets, 'frames': records,
    }
    (args.output / 'native-proof.json').write_text(json.dumps(proof, indent=2) + '\n')
    (args.output / 'README.md').write_text(readme(proof, route, args.capture_dir.name, args.crop))
    print(f'PASS: {args.kind}: {len(files)} original native frames; lossless RGB roundtrip, trace preservation, untouched stills and all diagnostic crop tiles verified.')


if __name__ == '__main__':
    main()
