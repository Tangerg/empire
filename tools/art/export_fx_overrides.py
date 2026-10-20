#!/usr/bin/env python3
"""Export reviewed FX phases with one full-canvas transform and fixed pivot."""
import argparse
import hashlib
import json
import math
import re
import stat
from pathlib import Path

from PIL import Image

from export_overrides import ROOT, read_json, read_source, records, within

# Cyclic phase order does not approve authored art or prove the 4 -> 1 seam.
AMBIENT_PHASE_ORDERS = {
    'C01-FX-09': ['rainfall', 'splash-onset', 'splash-crown', 'rain-renewal'],
    'C01-FX-10': ['ripple-near', 'ripple-growing', 'ripple-wide', 'ripple-renewal'],
    'C01-FX-11': ['gust-curl-a', 'gust-curl-b', 'gust-breakup', 'gust-renewal'],
    'C01-FX-12': ['heat-curl-a', 'heat-curl-b', 'heat-split', 'heat-renewal'],
}

# Phase meaning is topic-specific. Enable further topics only with their reviewed order.
PHASE_ORDERS = {
    'C01-FX-01': ['onset', 'sweep', 'peak', 'dissipate'],
    'C01-FX-02': ['onset', 'sweep', 'peak', 'dissipate'],
    'C01-FX-03': ['approach', 'impact', 'burst', 'dissipate'],
    'C01-FX-04': ['contact', 'expand', 'peak', 'settle'],
    'C01-FX-05': ['ignite', 'rise', 'flare', 'embers'],
    'C01-FX-06': ['low-ring', 'rising-ring', 'sealed-ring', 'fading-fragments'],
    'C01-FX-07': ['airborne-burst', 'falling-pieces', 'falling-remnants', 'settled-heap'],
    'C01-FX-08': ['clustered-echoes', 'rising-echoes', 'dissolving-echoes', 'wisp-remnants'],
    'C01-FX-13': ['cracked-earth', 'ember-rise', 'flame-peak', 'ember-residue'],
    'C01-FX-14': ['low-fumes', 'rising-fumes', 'plume-crest', 'settling-wisps'],
    'C01-FX-15': ['spirit-onset', 'spirit-growth', 'leafy-spirit-peak', 'spirit-residue'],
    'C01-FX-16': ['short-rays', 'rising-fan', 'graylight-crest', 'residual-rays'],
    'C01-FX-17': ['healing-onset', 'leaf-spiral', 'healing-peak', 'orbit-dissipation'],
    'C01-FX-18': ['scattered-bands', 'aligning-ring', 'closed-restraints', 'settled-restraints'],
    'C01-FX-19': ['dense-veil', 'curling-veil', 'veil-breakup', 'leaf-wisps'],
    'C01-FX-20': ['bare-crescent', 'contact-spark', 'spark-spray', 'residual-sparks'],
    'C01-FX-21': ['debris-lift', 'rubble-scatter', 'dust-impact', 'settling-dust'],
    **AMBIENT_PHASE_ORDERS,
}


def validate_fx_alpha(image, label, allowed_edge_max=0):
    if image.mode != 'RGBA':
        raise ValueError(f'{label}: expected PNG RGBA')
    alpha = image.getchannel('A')
    low, high = alpha.getextrema()
    if low != 0 or high == 0:
        raise ValueError(f'{label}: transparent exterior and nonempty effect required')
    width, height = image.size
    edges = [alpha.getpixel((x, y)) for y in (0, height - 1) for x in range(width)]
    edges += [alpha.getpixel((x, y)) for x in (0, width - 1) for y in range(1, height - 1)]
    if max(edges) > allowed_edge_max:
        raise ValueError(f'{label}: effect outer edge exceeds alpha{allowed_edge_max}')
    return {'maxAlpha': high, 'edgeMaxAlpha': max(edges), 'edgeNonzeroPixels': sum(v > 0 for v in edges),
            'allowedEdgeMaxAlpha': allowed_edge_max, 'boundingBox': list(alpha.getbbox()),
            'nonzeroPixels': sum(alpha.histogram()[1:])}


def finite(value):
    return type(value) in (int, float) and math.isfinite(value)


def pair(value):
    return isinstance(value, list) and len(value) == 2 and all(finite(v) for v in value)


def plan(root, catalog):
    data = read_json(catalog)
    if (not isinstance(data, dict) or set(data) != {'schemaVersion', 'assets'}
            or type(data['schemaVersion']) is not int or data['schemaVersion'] != 1
            or not isinstance(data['assets'], dict)):
        raise ValueError('expected schemaVersion 1 and assets object')
    known = records(root)
    planned, targets = [], set()
    for topic, override in data['assets'].items():
        if topic not in PHASE_ORDERS or topic not in known:
            raise ValueError(f'{topic}: no reviewed FX phase contract')
        pack, asset, hd = known[topic]
        if (pack / 'masters').resolve() != pack.resolve() / 'masters':
            raise ValueError(f'{topic}: source masters must use the canonical owned directory')
        order = PHASE_ORDERS[topic]
        loop = topic in AMBIENT_PHASE_ORDERS
        geometry = {'width': 128, 'height': 32, 'frameWidth': 32, 'frameHeight': 32, 'frames': 4, 'fps': 12}
        if (pack != root / 'packages/story-candidate-01/assets/final-fantasy-v1'
                or asset.get('category') != 'fx'
                or any(type(asset.get(k)) is not int or asset[k] != v for k, v in geometry.items())
                or asset.get('anchor') != [16, 16] or asset.get('loop') is not loop
                or asset.get('blendMode') != 'normal'):
            raise ValueError(f'{topic}: inconsistent canonical FX contract')
        if (not hd or any(type(hd.get(k)) is not int or hd[k] != v
                          for k, v in {'width': 256, 'height': 64, 'pixelDensity': 2}.items())):
            raise ValueError(f'{topic}: unsupported HD contract; 256x64 at density 2 required')
        if (not isinstance(override, dict) or set(override) != {'kind', 'phases', 'placement'}
                or override['kind'] != 'fx-phases-v1' or not isinstance(override['phases'], list)
                or len(override['phases']) != 4):
            raise ValueError(f'{topic}: expected fx-phases-v1 with four ordered phases and placement')
        for index, entry in enumerate(override['phases']):
            if (not isinstance(entry, dict) or set(entry) != {'phase', 'source', 'sha256'}
                    or entry['phase'] != order[index]):
                raise ValueError(f'{topic}: exact semantic phase order required: {order}')
            if not isinstance(entry['sha256'], str) or not re.fullmatch(r'[0-9a-f]{64}', entry['sha256']):
                raise ValueError(f'{topic}: invalid source SHA-256')
        placement = override['placement']
        if not isinstance(placement, dict) or set(placement) != {'scale', 'origin', 'sourcePivot'}:
            raise ValueError(f'{topic}: expected shared scale, origin and sourcePivot only; no crop or per-phase fit')
        scale, origin, pivot = placement['scale'], placement['origin'], placement['sourcePivot']
        if (not finite(scale) or scale <= 0 or not pair(origin)
                or any(type(v) is not int or v < 0 for v in origin) or not pair(pivot)):
            raise ValueError(f'{topic}: finite positive scale, nonnegative integer origin and finite sourcePivot required')
        sources, provenance, hashes = [], [], set()
        source_size = None
        for entry in override['phases']:
            name = entry['phase']
            if not within(root, entry['source']).is_file():
                raise ValueError(f'{name}: source must be a regular file')
            source, digest = read_source(root, pack, {k: entry[k] for k in ('source', 'sha256')}, name)
            # Imagegen can leave alpha1 perimeter residue; preserve it, then require
            # strict zero on every final padded cell. Visible alpha2+ is rejected.
            alpha = validate_fx_alpha(source, name, allowed_edge_max=1)
            if source_size is None:
                source_size = source.size
            if source.size != source_size:
                raise ValueError(f'{topic}: all phases must share exact authored canvas dimensions')
            pixel_hash = hashlib.sha256(source.tobytes()).hexdigest()
            if pixel_hash in hashes:
                raise ValueError(f'{topic}: duplicate decoded source phases')
            hashes.add(pixel_hash)
            sources.append(source)
            provenance.append({'phase': name, 'source': entry['source'], 'sourceSha256': digest,
                               'sourcePixelSha256': pixel_hash, 'sourceCanvas': list(source.size), 'alpha': alpha})
        scaled_size = [side * scale for side in source_size]
        if any(not finite(v) or v < 1 or not float(v).is_integer() for v in scaled_size):
            raise ValueError(f'{topic}: shared scale must produce integer canvas dimensions without aspect rounding')
        size = [int(v) for v in scaled_size]
        if any(origin[i] + size[i] > 32 for i in (0, 1)):
            raise ValueError(f'{topic}: shared transform would clip full authored canvas')
        if (any(pivot[i] < 0 or pivot[i] >= source_size[i] for i in (0, 1))
                or any(not math.isclose(origin[i] + pivot[i] * scale, asset['anchor'][i], rel_tol=0, abs_tol=1e-8)
                       for i in (0, 1))):
            raise ValueError(f'{topic}: shared sourcePivot must map to the authoritative logical anchor')
        for record, density in ((asset, 1), (hd, 2)):
            directory = 'runtime-hd' if density == 2 else 'runtime'
            expected = f'{directory}/fx/fx-{topic[-2:]}.png'
            target = within(pack, record['png'])
            if (record['png'] != expected or target != pack.resolve() / expected or target in targets
                    or (target.exists() and (not stat.S_ISREG(target.stat().st_mode) or target.stat().st_nlink != 1))
                    or any(parent.exists() and not parent.is_dir() for parent in target.parents)):
                raise ValueError(f'{topic}: unexpected, linked or duplicate export path')
            targets.add(target)
            sheet = Image.new('RGBA', (128 * density, 32 * density))
            frame_reports, rendered_hashes = [], set()
            for index, (name, source) in enumerate(zip(order, sources)):
                rendered = source.resize(tuple(side * density for side in size), Image.Resampling.LANCZOS)
                cell = Image.new('RGBA', (32 * density, 32 * density))
                # Unmasked paste preserves authored alpha instead of multiplying it by itself.
                cell.paste(rendered, tuple(v * density for v in origin))
                alpha = validate_fx_alpha(cell, f'{name}@{density}x')
                digest = hashlib.sha256(cell.tobytes()).hexdigest()
                if digest in rendered_hashes:
                    raise ValueError(f'{topic}: duplicate rendered phases at target density')
                rendered_hashes.add(digest)
                sheet.paste(cell, (index * 32 * density, 0))
                frame_reports.append({'phase': name, 'pixelSha256': digest, 'alpha': alpha})
            report = {'topicId': topic, 'density': density, 'output': str(target.relative_to(root)),
                      'size': list(sheet.size), 'frameSize': [32 * density, 32 * density],
                      'logicalAnchor': asset['anchor'], 'placement': placement,
                      'renderedCanvasSize': [v * density for v in size], 'sources': provenance,
                      'frames': frame_reports, 'pixelSha256': hashlib.sha256(sheet.tobytes()).hexdigest(),
                      'fps': asset['fps'], 'loop': asset['loop'], 'blendMode': asset['blendMode'],
                      'playback': {'mode': 'continuous-loop' if loop else 'once-hold',
                                   'sequenceDurationMs': asset['frames'] * 1000 / asset['fps'],
                                   'afterLastFrame': 'wrap-to-first-frame' if loop else 'hold-last-frame',
                                   'wrapTransition': [order[-1], order[0]] if loop else None,
                                   'lifetimeOwner': 'consumer'},
                      'loopClosureReview': 'required-separately' if loop else 'not-applicable',
                      'independentFrameFit': False, 'semanticMotionReview': 'required-separately',
                      'liveGamePlayback': 'not-performed'}
            planned.append((target, sheet, report))
    return planned


def export(root, catalog, check=False):
    # Finish validation for every source and destination before the first write.
    planned = plan(root, catalog)
    for target, sheet, _ in planned:
        if check:
            with Image.open(target) as current:
                if current.format != 'PNG' or current.mode != 'RGBA' or current.size != sheet.size or current.tobytes() != sheet.tobytes():
                    raise ValueError(f'{target}: stale FX export')
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            sheet.save(target, format='PNG', optimize=True)
    return [report for _, _, report in planned]


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('catalog', type=Path, help='Explicit reviewed FX catalog; no art is registered by this tool')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(export(ROOT, args.catalog, args.check), indent=2))
