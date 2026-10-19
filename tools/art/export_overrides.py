#!/usr/bin/env python3
"""Export explicitly registered RGBA masters without running the legacy art builders."""
from __future__ import annotations

import argparse
import hashlib
import io
import json
from pathlib import Path

from PIL import Image, ImageOps

ROOT = Path(__file__).resolve().parents[2]
CATALOG = Path('tools/art/source-overrides.json')
PACKS = (
    ('story-candidate-01', 'final-fantasy-v1', 'fantasy'),
    ('story-candidate-02', 'final-scifi-v1', 'scifi'),
    ('story-candidate-03', 'final-ancient-china-v1', 'ancient-china'),
)


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f'duplicate JSON key: {key}')
        result[key] = value
    return result


def read_json(path: Path):
    return json.loads(path.read_text(), object_pairs_hook=unique_object)


def within(root: Path, relative: str) -> Path:
    if not isinstance(relative, str) or Path(relative).is_absolute():
        raise ValueError(f'expected relative path: {relative!r}')
    path = (root / relative).resolve()
    if not path.is_relative_to(root.resolve()):
        raise ValueError(f'path escapes root: {relative}')
    return path


def validate_alpha(image: Image.Image, label: str):
    if image.mode != 'RGBA':
        raise ValueError(f'{label}: expected RGBA, got {image.mode}')
    alpha = image.getchannel('A')
    low, high = alpha.getextrema()
    if low != 0 or high < 240:
        raise ValueError(f'{label}: expected transparent exterior and near-opaque subject')
    w, h = image.size
    edge_pixels = [alpha.getpixel((x, y)) for y in (0, h - 1) for x in range(w)]
    edge_pixels += [alpha.getpixel((x, y)) for x in (0, w - 1) for y in range(1, h - 1)]
    edge_max = max(edge_pixels)
    # One 8-bit alpha step changes a composite channel by at most one level.
    # Preserve that source residue; stronger edge opacity needs an art review.
    if edge_max > 1:
        raise ValueError(f'{label}: outer edge alpha {edge_max} exceeds 1; subject or shadow may be clipped')
    return {
        'edgeMaxAlpha': edge_max,
        'edgeNonzeroPixels': sum(value > 0 for value in edge_pixels),
        'coreBoundingBox': list(alpha.point(lambda value: 255 if value >= 240 else 0).getbbox()),
    }


def fit(source: Image.Image, size: tuple[int, int], padding: int) -> Image.Image:
    # Fit the complete authored canvas; never infer a new crop or replace alpha.
    fitted = ImageOps.contain(source, (size[0] - 2 * padding, size[1] - 2 * padding), Image.Resampling.LANCZOS)
    result = Image.new('RGBA', size, (0, 0, 0, 0))
    result.paste(fitted, ((size[0] - fitted.width) // 2, (size[1] - fitted.height) // 2))
    validate_alpha(result, 'export')
    return result


def records(root: Path):
    result = {}
    for campaign, directory, suffix in PACKS:
        pack = root / 'packages' / campaign / 'assets' / directory
        manifest = read_json(pack / f'manifest-final-{suffix}-v1.json')
        hd_path = pack / 'manifest-tactical-runtime-hd.json'
        hd = {}
        if hd_path.exists():
            for entry in read_json(hd_path)['assets']:
                if entry['topicId'] in hd:
                    raise ValueError('duplicate HD topic')
                hd[entry['topicId']] = entry
        for asset in manifest['assets']:
            topic = asset['topicId']
            if topic in result:
                raise ValueError(f'duplicate topic: {topic}')
            result[topic] = (pack, asset, hd.get(topic))
    return result


def plan(root: Path, catalog: Path):
    data = read_json(catalog)
    if data.get('schemaVersion') != 1 or not isinstance(data.get('assets'), dict):
        raise ValueError('expected schemaVersion 1 and assets object')
    known = records(root)
    planned = []
    targets = set()
    for topic, override in data['assets'].items():
        if topic not in known:
            raise ValueError(f'unknown topic: {topic}')
        pack, asset, hd = known[topic]
        if asset['category'] != 'battle-prop' or asset.get('frames') or asset.get('variants'):
            raise ValueError(f'{topic}: only static battle-prop exports supported')
        if (asset['width'], asset['height'], asset.get('anchor')) != (64, 64, [32, 32]):
            raise ValueError(f'{topic}: unsupported logical size/anchor')
        if not isinstance(override, dict) or set(override) != {'source', 'sha256'}:
            raise ValueError(f'{topic}: expected source and sha256 only')
        source_path = within(root, override['source'])
        if not source_path.is_relative_to((pack / 'masters').resolve()):
            raise ValueError(f'{topic}: source must be in its pack masters directory')
        raw = source_path.read_bytes()
        digest = hashlib.sha256(raw).hexdigest()
        if digest != override['sha256']:
            raise ValueError(f'{topic}: source SHA-256 mismatch')
        with Image.open(io.BytesIO(raw)) as opened:
            if opened.format != 'PNG':
                raise ValueError(f'{topic}: source must be PNG')
            source = opened.copy()
        source_alpha = validate_alpha(source, topic)
        exports = [(asset, 1)]
        if hd:
            if (hd['width'], hd['height'], hd['pixelDensity']) != (128, 128, 2):
                raise ValueError(f'{topic}: unsupported HD contract')
            exports.append((hd, 2))
        for record, density in exports:
            target = within(pack, record['png'])
            expected_dir = pack / ('runtime-hd' if density == 2 else 'runtime') / 'battle-prop'
            if target.parent != expected_dir.resolve() or target.suffix != '.png':
                raise ValueError(f'{topic}: unexpected export path')
            if target in targets:
                raise ValueError(f'duplicate destination: {target}')
            targets.add(target)
            rendered = fit(source, (record['width'], record['height']), 2 * density)
            planned.append((topic, digest, source_alpha, target, rendered))
    return planned


def export(root: Path, catalog: Path, check: bool = False):
    planned = plan(root, catalog)  # Validate every input before replacing any output.
    report = []
    for topic, digest, source_alpha, target, rendered in planned:
        if check:
            with Image.open(target) as current:
                if current.mode != rendered.mode or current.size != rendered.size or current.tobytes() != rendered.tobytes():
                    raise ValueError(f'{topic}: stale export {target}')
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            buffer = io.BytesIO()
            rendered.save(buffer, format='PNG', optimize=True)
            target.write_bytes(buffer.getvalue())
        report.append({'topicId': topic, 'sourceSha256': digest,
                       'output': str(target.relative_to(root)),
                       'pixelSha256': hashlib.sha256(rendered.tobytes()).hexdigest(),
                       'size': list(rendered.size), 'sourceAlpha': source_alpha,
                       'outputAlpha': validate_alpha(rendered, 'export')})
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true', help='verify exports without writing')
    args = parser.parse_args()
    print(json.dumps(export(ROOT, ROOT / CATALOG, args.check), indent=2))


if __name__ == '__main__':
    main()
