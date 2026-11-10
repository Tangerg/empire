"""Verify archived native RGB observations and the actor's recorded lifecycle."""

import hashlib
import json
import subprocess
from pathlib import Path

from PIL import Image, ImageChops


ROOT = Path(__file__).resolve().parent
proof = json.loads((ROOT / "native-fall-proof.json").read_text())
video = ROOT / "native-fall-lossless.mp4"
assert hashlib.sha256(video.read_bytes()).hexdigest() == proof["videoSha256"]
traces = [json.loads(line) for line in (ROOT / "native-fall-frames.jsonl").read_text().splitlines()]
assert len(traces) == len(proof["frames"]) == proof["observations"] == 93
assert proof["viewport"] == [960, 640]
assert all(trace["captureStride"] == 1 for trace in traces)
assert all(
    after["observedFrame"] == before["observedFrame"] + 1
    and after["processFrame"] == before["processFrame"] + 1
    for before, after in zip(traces, traces[1:])
)
actors = [
    next((actor for actor in trace["actors"] if actor["id"] == 3), None)
    for trace in traces
]
assert all(actor is not None for actor in actors[:-1]) and actors[-1] is None
assert all(
    actor["position"] == proof["actorPosition"]
    and actor["supportPlane"] == proof["actorSupportPlane"]
    and not actor["mirrored"]
    for actor in actors[:-1]
)
assert all(actor["scale"] == [0.096, 0.096] for actor in actors[45:-1])
transitions = []
previous = None
for index, actor in enumerate(actors):
    current = (actor["art"], actor["frame"]) if actor else None
    if current != previous:
        transitions.append({
            "observation": index,
            "art": current[0] if current else None,
            "frame": current[1] if current else None,
        })
        previous = current
assert transitions == proof["actorPoseTransitions"]
assert [
    (step["observation"], step["frame"])
    for step in transitions if step["art"] == "archer-fall-v1"
] == [(45, 0), (49, 1), (53, 2), (58, 3), (65, 4), (72, 5)]
assert not traces[-1]["presenting"]

decoder = subprocess.Popen(
    ["ffmpeg", "-v", "error", "-i", str(video), "-f", "rawvideo", "-pix_fmt", "rgb24", "-"],
    stdout=subprocess.PIPE,
)
decoded = []
for record in proof["frames"]:
    pixels = decoder.stdout.read(960 * 640 * 3)
    assert len(pixels) == 960 * 640 * 3
    assert hashlib.sha256(pixels).hexdigest() == record["rgbSha256"], record["file"]
    decoded.append(Image.frombytes("RGB", (960, 640), pixels))
assert not decoder.stdout.read(1) and decoder.wait() == 0

for index, label in [
    (0, "before-hit"), (45, "fall-start"), (72, "ground-hold"),
    (87, "terminal-fade"), (92, "retired"),
]:
    archived = ROOT / f"native-{label}.png"
    assert hashlib.sha256(archived.read_bytes()).hexdigest() == proof["frames"][index]["pngSha256"]
    assert ImageChops.difference(Image.open(archived).convert("RGB"), decoded[index]).getbbox() is None

crop_box = tuple(proof["diagnosticCrop"])
tile_width = (crop_box[2] - crop_box[0]) * 3
tile_height = (crop_box[3] - crop_box[1]) * 3
for sheet_name, indices, columns in [
    ("native-fall-phases-3x.png", [0, 18, 45, 49, 53, 58, 65, 72, 82, 87, 91, 92], 6),
    ("native-all-observations-3x.png", list(range(93)), 10),
]:
    sheet = Image.open(ROOT / sheet_name).convert("RGB")
    for index, observation in enumerate(indices):
        expected = decoded[observation].crop(crop_box).resize(
            (tile_width, tile_height), Image.Resampling.NEAREST
        )
        x = index % columns * tile_width
        y = index // columns * (tile_height + 24) + 24
        actual = sheet.crop((x, y, x + tile_width, y + tile_height))
        assert ImageChops.difference(expected, actual).getbbox() is None

print("PASS: 93 full RGB frames, five full PNG stills, all diagnostic crop tiles, six poses and terminal retirement.")
print("Opacity is not recorded in the trace; visual review and controlled-clock tests remain required.")
