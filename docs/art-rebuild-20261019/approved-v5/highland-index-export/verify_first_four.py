"""Bounded real-source export proof. Writes only a temporary fixture and output directory."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import sys
import tempfile
from PIL import Image, ImageDraw, ImageFont

parser = argparse.ArgumentParser()
parser.add_argument('--tool-root', type=Path, required=True)
parser.add_argument('--source-root', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
parser.add_argument('--original-runtime-baseline', action='store_true',
                    help='Read original atlas pixels at runtime paths; SHA must match catalog baseline.')
args = parser.parse_args()
sys.path.insert(0, str(args.tool_root / 'tools/art'))
from export_environment_overrides import PACK, export, cell_box
catalog_path = args.tool_root / 'docs/art-rebuild-20261019/approved-v5/highland-index-export/catalog-example-first-four.json'
data = json.loads(catalog_path.read_text())
override = data['atlases']['cliff-modules-highland']
assert list(override['cells']) == ['straight-south', 'end-west', 'end-east', 'concave-corner']
manifest_path = PACK / 'environment-builder-v1/manifest-environment-builder-v1.json'
manifest_bytes = (args.source_root / manifest_path).read_bytes()
atlas = next(a for a in json.loads(manifest_bytes)['atlases'] if a['id'] == 'cliff-modules-highland')
assert atlas['cells'] == [] and atlas['anchor'] == [64, 127]
args.output.mkdir(parents=True, exist_ok=True)
sha = lambda value: hashlib.sha256(value).hexdigest()
source_bytes = {}
with tempfile.TemporaryDirectory(prefix='empire-highland-first-four-') as directory:
    root = Path(directory)
    (root / manifest_path).parent.mkdir(parents=True, exist_ok=True)
    (root / manifest_path).write_bytes(manifest_bytes)
    for name, entry in [*override['baselines'].items(), *override['cells'].items()]:
        relative = Path(entry['source'])
        read_path = relative
        if args.original_runtime_baseline and name in ('1x', '2x'):
            read_path = PACK / 'environment-builder-v1' / atlas['png' if name == '1x' else 'png2x']
        raw = (args.source_root / read_path).read_bytes()
        assert sha(raw) == entry['sha256'], f'source mismatch: {read_path}'
        source_bytes[str(read_path)] = raw
        target = root / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(raw)
    catalog = root / 'catalog.json'
    catalog.write_text(json.dumps(data))
    reports = export(root, catalog)
    export(root, catalog, True)
    assert (root / manifest_path).read_bytes() == manifest_bytes
    for report in reports:
        density = report['density']
        assert [c['index'] for c in report['cells'] if c['overridden']] == [0, 1, 2, 3]
        assert all(c['baselinePixelSha256'] == c['outputPixelSha256'] for c in report['cells'][4:])
        shutil.copyfile(root / report['output'], args.output / f'after@{density}x.png')
        shutil.copyfile(root / report['baseline']['source'], args.output / f'before@{density}x.png')
        with Image.open(root / report['output']) as output:
            report['pngSha256'] = sha((root / report['output']).read_bytes())
            for cell in report['cells'][:4]:
                alpha = output.crop(cell_box(cell['index'], 128 * density)).getchannel('A')
                cell['visibleBoundsAlpha128'] = alpha.point(lambda n: 255 if n >= 128 else 0).getbbox()
    assert all((args.source_root / path).read_bytes() == raw for path, raw in source_bytes.items())
    assert (args.source_root / manifest_path).read_bytes() == manifest_bytes
report = {'kind': 'isolated real-source catalog-only export, no level integration or contact calibration',
          'catalogSha256': sha(catalog_path.read_bytes()), 'sourceManifestSha256': sha(manifest_bytes),
          'sourceHashes': {path: sha(raw) for path, raw in source_bytes.items()},
          'manifestBytesUnchanged': True, 'nativeSourceBytesUnchanged': True,
          'reexportCheckPassed': True, 'changedIndices': [0, 1, 2, 3],
          'untouchedIndices': list(range(4, 16)), 'exports': reports}
(args.output / 'actual-first-four-export.json').write_text(json.dumps(report, indent=2) + '\n')
# A labeled full-atlas comparison, not a scene or a native master replacement.
font = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf', 14)
small = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf', 11)
board = Image.new('RGB', (1080, 706), '#18202b')
draw = ImageDraw.Draw(board)
draw.text((20, 12), 'Highland catalog-only | actual full 16-slot atlases | logical 1x', font=font, fill='white')
draw.text((20, 38), 'Only slots 0–3 replaced; 4–15 preserved pixel-for-pixel. No scene contact calibration.', font=small, fill='#d1d9e4')
for column, name in enumerate(('before', 'after')):
    ox = 20 + column * 540
    draw.text((ox, 64), name.upper(), font=font, fill='white')
    with Image.open(args.output / f'{name}@1x.png') as atlas_image:
        for index, label in enumerate(atlas['cellOrder']):
            x, y = ox + index % 4 * 128, 94 + index // 4 * 150
            draw.text((x, y), f'{index}: {label}', font=small, fill='#ffe3a1' if index < 4 else '#a6b0be')
            cell = atlas_image.crop(cell_box(index, 128))
            board.paste(cell, (x, y + 17), cell)
board.save(args.output / 'full-atlas-before-after.png')
print(json.dumps({'changed': report['changedIndices'], 'preserved': len(report['untouchedIndices']),
                  'manifestUnchanged': True, 'sourceUnchanged': True,
                  'sourceFootLogical': {k:v['sourceFootLogical'] for k,v in reports[0]['sources'].items()}}, indent=2))
