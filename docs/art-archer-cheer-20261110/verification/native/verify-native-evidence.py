"""Verify archived native RGB observations and both celebration lifecycles."""
import hashlib
import json
import subprocess
from pathlib import Path
from PIL import Image, ImageChops

ROOT = Path(__file__).resolve().parent
proof = json.loads((ROOT / 'native-cheer-proof.json').read_text())
video = ROOT / 'native-cheer-lossless.mp4'
assert hashlib.sha256(video.read_bytes()).hexdigest() == proof['videoSha256']
traces = [json.loads(line) for line in (ROOT / 'native-cheer-frames.jsonl').read_text().splitlines()]
assert len(traces) == len(proof['frames']) == proof['observations'] == 100
assert proof['viewport'] == [960, 640]
assert all(t['captureStride'] == 1 for t in traces)
assert all(b['observedFrame'] == a['observedFrame'] + 1 and b['processFrame'] == a['processFrame'] + 1
           for a, b in zip(traces, traces[1:]))
assert all(t['presenting'] for t in traces[:-1]) and not traces[-1]['presenting']
actors = {id: [next((a for a in t['actors'] if a['id'] == id), None) for t in traces] for id in (2, 3, 9, 18)}
for id, sequence in actors.items():
    previous = None
    transitions = []
    for i, actor in enumerate(sequence):
        key = (actor['art'], actor['frame']) if actor else (None, None)
        if key != previous:
            transitions.append({'observation': i, 'art': key[0], 'frame': key[1]})
            previous = key
    assert transitions == proof['actorPoseTransitions'][str(id)]
    if id in (2, 3, 9):
        expected = proof['actors'][str(id)]
        assert all(a is not None and a['position'] == expected['position']
                   and a['supportPlane'] == expected['supportPlane']
                   and not a['mirrored'] for a in sequence)
    if id in (3, 9):
        assert [(s['observation'], s['frame']) for s in transitions if s['art'] == 'archer-cheer-v1'] == [
            (41, 0), (47, 1), (54, 2), (64, 3), (80, 4), (90, 5)]
        assert all(a['scale'] == [.0805, .0805] for a in sequence[41:99])
        assert sequence[99]['art'] == 'archer-breathe-v1' and sequence[99]['frame'] == 0
assert actors[2][94]['art'] == 'torren-salute-v1' and actors[2][95]['art'] == 'torren-breathe-v1'
assert all(a is not None for a in actors[18][:41]) and all(a is None for a in actors[18][41:])
decoder = subprocess.Popen(['ffmpeg', '-v', 'error', '-i', str(video), '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-'], stdout=subprocess.PIPE)
decoded = []
try:
    for record in proof['frames']:
        pixels = decoder.stdout.read(960 * 640 * 3)
        assert len(pixels) == 960 * 640 * 3
        assert hashlib.sha256(pixels).hexdigest() == record['rgbSha256'], record['file']
        decoded.append(Image.frombytes('RGB', (960, 640), pixels))
    assert not decoder.stdout.read(1) and decoder.wait() == 0
finally:
    if decoder.poll() is None:
        decoder.kill()
        decoder.wait()
for name, i in proof['stills'].items():
    path = ROOT / name
    assert hashlib.sha256(path.read_bytes()).hexdigest() == proof['frames'][i]['pngSha256']
    assert ImageChops.difference(Image.open(path).convert('RGB'), decoded[i]).getbbox() is None
for spec in proof['diagnosticSheets']:
    box = spec['crop']
    width = (box[2] - box[0]) * spec['scale']
    height = (box[3] - box[1]) * spec['scale']
    sheet = Image.open(ROOT / spec['file']).convert('RGB')
    for n, i in enumerate(spec['indices']):
        expected = decoded[i].crop(box).resize((width, height), Image.Resampling.NEAREST)
        x = n % spec['columns'] * width
        y = n // spec['columns'] * (height + 24) + 24
        actual = sheet.crop((x, y, x + width, y + height))
        assert ImageChops.difference(expected, actual).getbbox() is None
print('PASS: 100 full RGB frames, eight native stills, all diagnostic crop tiles, six celebration poses, independent idle endpoints and preceding retirement.')
print('Selected late-death window only; native facing is unmirrored. Opacity is not recorded in the trace. No real-time performance claim.')
