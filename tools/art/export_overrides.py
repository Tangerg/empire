#!/usr/bin/env python3
"""Export registered masters and explicit character/background card compositions."""
from __future__ import annotations

import argparse
import hashlib
import io
import json
import math
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


def read_source(root: Path, pack: Path, spec, label: str):
    if not isinstance(spec, dict) or set(spec) != {'source', 'sha256'}:
        raise ValueError(f'{label}: expected source and sha256 only')
    path = within(root, spec['source'])
    if not path.is_relative_to((pack / 'masters').resolve()):
        raise ValueError(f'{label}: source must be in its pack masters directory')
    raw = path.read_bytes()
    digest = hashlib.sha256(raw).hexdigest()
    if digest != spec['sha256']:
        raise ValueError(f'{label}: source SHA-256 mismatch')
    with Image.open(io.BytesIO(raw)) as opened:
        if opened.format != 'PNG':
            raise ValueError(f'{label}: source must be PNG')
        source = opened.copy()
    return source, digest


def compose_character_card(root: Path, pack: Path, asset, hd, override):
    topic = asset['topicId']
    if (pack != root / 'packages/story-candidate-01/assets/final-fantasy-v1'
            or asset.get('kind') != 'character-card'
            or (asset['width'], asset['height']) != (192, 224)
            or asset.get('frames') or asset.get('variants') or hd):
        raise ValueError(f'{topic}: only existing Gray Flag 192x224 character-card contracts supported')
    if (not isinstance(override, dict)
            or set(override) != {'kind', 'character', 'background', 'placement'}
            or override['kind'] != 'character-card-composite-v1'):
        raise ValueError(f'{topic}: expected character-card-composite-v1 with character, background and placement')
    character, character_hash = read_source(root, pack, override['character'], f'{topic} character')
    character_alpha = validate_alpha(character, f'{topic} character')
    background, background_hash = read_source(root, pack, override['background'], f'{topic} background')
    if (background.mode not in ('RGB', 'RGBA')
            or (background.mode == 'RGBA' and background.getchannel('A').getextrema() != (255, 255))):
        raise ValueError(f'{topic}: background must be RGB or fully opaque RGBA')
    placement = override['placement']
    if not isinstance(placement, dict) or set(placement) != {'renderWidth', 'origin'}:
        raise ValueError(f'{topic}: expected renderWidth and origin placement')
    width, origin = placement['renderWidth'], placement['origin']
    if (type(width) is not int or not 1 <= width <= 192
            or not isinstance(origin, list) or len(origin) != 2
            or any(type(value) is not int or value < 0 for value in origin)):
        raise ValueError(f'{topic}: placement requires positive integer width and nonnegative integer origin')
    height = round(character.height * width / character.width)
    if height < 1 or origin[0] + width > 192 or origin[1] + height > 224:
        raise ValueError(f'{topic}: complete character canvas must fit inside the card')
    # Cover is intentional only for the independent background. Character alpha
    # and the entire authored foreground canvas are retained, without bbox fitting.
    crop_width = min(background.width, background.height * 192 / 224)
    crop_height = min(background.height, background.width * 224 / 192)
    left, top = (background.width - crop_width) / 2, (background.height - crop_height) / 2
    rendered = ImageOps.fit(background.convert('RGBA'), (192, 224), Image.Resampling.LANCZOS)
    rendered.alpha_composite(character.resize((width, height), Image.Resampling.LANCZOS), tuple(origin))
    provenance = {
        'kind': override['kind'],
        'sources': {
            'character': {**override['character'], 'size': list(character.size), 'mode': character.mode,
                          'sha256': character_hash, 'alpha': character_alpha},
            'background': {**override['background'], 'size': list(background.size), 'mode': background.mode,
                           'sha256': background_hash},
        },
        'characterPlacement': {**placement, 'renderSize': [width, height], 'crop': None},
        'backgroundFit': {'method': 'center-cover', 'sourceCropBox':
                          [left, top, left + crop_width, top + crop_height]},
        'outputAlpha': {'extrema': list(rendered.getchannel('A').getextrema()),
                        'meaning': 'opaque UI composition; transparent character source remains independent'},
    }
    return rendered, provenance


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
        category = asset['category']
        contracts = {'battle-prop': 64, 'equipment': 48, 'skill': 48, 'status': 32, 'hud': 32}
        if category == 'narrative-static':
            rendered, provenance = compose_character_card(root, pack, asset, hd, override)
            exports = [(asset, 1, rendered, provenance)]
        elif category not in contracts or asset.get('frames') or asset.get('variants'):
            raise ValueError(f'{topic}: only static battle-prop/equipment/skill/status/hud exports supported')
        else:
            side = contracts[category]
            anchor = asset.get('anchor')
            valid_anchor = (isinstance(anchor, list) and len(anchor) == 2
                            and all(isinstance(v, (int, float)) and math.isfinite(v) and 0 <= v <= side for v in anchor))
            if ((asset['width'], asset['height']) != (side, side) or not valid_anchor
                    or (category != 'battle-prop' and anchor != [side // 2, side // 2])):
                raise ValueError(f'{topic}: unsupported logical size/anchor')
            source, digest = read_source(root, pack, override, topic)
            source_alpha = validate_alpha(source, topic)
            records_to_export = [(asset, 1)]
            if hd:
                if category != 'battle-prop' or (hd['width'], hd['height'], hd['pixelDensity']) != (128, 128, 2):
                    raise ValueError(f'{topic}: unsupported HD contract')
                records_to_export.append((hd, 2))
            exports = []
            for record, density in records_to_export:
                rendered = fit(source, (record['width'], record['height']), 2 * density)
                exports.append((record, density, rendered, {'sourceSha256': digest,
                                'sourceAlpha': source_alpha, 'outputAlpha': validate_alpha(rendered, 'export')}))
        for record, density, rendered, provenance in exports:
            target = within(pack, record['png'])
            expected_dir = pack / ('runtime-hd' if density == 2 else 'runtime') / category
            if target.parent != expected_dir.resolve() or target.suffix != '.png':
                raise ValueError(f'{topic}: unexpected export path')
            if target in targets:
                raise ValueError(f'duplicate destination: {target}')
            targets.add(target)
            planned.append((topic, provenance, target, rendered))
    return planned


def export(root: Path, catalog: Path, check: bool = False):
    planned = plan(root, catalog)  # Validate every input before replacing any output.
    report = []
    for topic, provenance, target, rendered in planned:
        if check:
            with Image.open(target) as current:
                if current.mode != rendered.mode or current.size != rendered.size or current.tobytes() != rendered.tobytes():
                    raise ValueError(f'{topic}: stale export {target}')
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            buffer = io.BytesIO()
            rendered.save(buffer, format='PNG', optimize=True)
            target.write_bytes(buffer.getvalue())
        report.append({'topicId': topic, **provenance,
                       'output': str(target.relative_to(root)),
                       'pixelSha256': hashlib.sha256(rendered.tobytes()).hexdigest(),
                       'size': list(rendered.size)})
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true', help='verify exports without writing')
    args = parser.parse_args()
    print(json.dumps(export(ROOT, ROOT / CATALOG, args.check), indent=2))


if __name__ == '__main__':
    main()
