"""Reproduce source checks and diagnostics; write exports only to this private fixture."""
import hashlib
import json
import shutil
import sys
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[4]
OUT = Path(__file__).parent
sys.path.insert(0, str(ROOT / 'tools/art'))
from export_canonical_terrain_overrides import (
    CHINA_PACK, export, pixel_hash, plan, read_material, render_material, review_previews,
)

pack = ROOT / CHINA_PACK
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
records = json.loads((OUT / 'reservation.json').read_text())['requests']
returned = json.loads((OUT / 'selected-returned-paths.json').read_text())
initial = json.loads((OUT / 'initial-returned-paths.json').read_text())
assets, reviews, panels, originals, inventory = {}, [], [], [], []
for req in records:
    slug, topic, mode = req['slug'], req['topicId'], req['tileMode']
    directory = pack / 'masters/approved-v5/canonical-terrain' / slug
    raw, master, tool_path = directory / 'raw.png', directory / 'master.png', Path(returned[slug])
    assert tool_path.read_bytes() == raw.read_bytes() == master.read_bytes()
    source_image = Image.open(master)
    w, h = source_image.size
    assert (w, h) == (1254, 1254)
    assert source_image.mode in ('RGB', 'RGBA')
    assert source_image.mode == 'RGB' or source_image.getchannel('A').getextrema() == (255, 255)
    windows = [[0, 0, w, h]]
    entry = {'source': str(master.relative_to(ROOT)), 'sha256': sha(master),
             'registration': {'sourceSize': [w, h], 'windowsXYXY': windows}}
    assets[topic] = entry
    references = [{'path': str(Path(p).relative_to(ROOT)), 'sha256': sha(Path(p))}
                  for p in req['referenced_image_paths']]
    invocations = [{'kind': 'generate', 'requestFile': 'request.json',
                    'returnedImagePath': initial[slug], 'sha256': sha(Path(initial[slug]))}]
    if slug == '70b193fa':
        retained = directory / 'candidates/initial'
        assert (retained / 'raw.png').read_bytes() == (retained / 'master.png').read_bytes() == Path(initial[slug]).read_bytes()
        invocations.append({'kind': 'bounded-edit', 'requestFile': 'edit-request.json',
                            'returnedImagePath': returned[slug], 'sha256': sha(master),
                            'reason': 'Initial full-material E/W arm endpoints contained water instead of dry levee earth; native32 diagnostic confirmed the failure.',
                            'outcome': 'Dry earth now reaches every cardinal midpoint. No second edit or retry.'})
        (retained / 'provenance.json').write_text(json.dumps({
            **invocations[0], 'status': 'rejected-routing-defect', 'rawEqualsMasterEqualsReturned': True,
            'processing': 'None; exact generated PNG bytes retained.', 'replacement': '../../master.png',
        }, indent=2))
    provenance = {'tool': 'built-in image_gen.imagegen', 'request': 'request.json',
                  'selectedRequest': 'edit-request.json' if slug == '70b193fa' else 'request.json',
                  'raw': 'raw.png', 'master': 'master.png', 'returnedImagePath': str(tool_path),
                  'rawEqualsMasterEqualsReturned': True, 'sha256': sha(master), 'sourceSize': [w, h],
                  'mode': source_image.mode, 'references': references, 'topicId': topic,
                  'status': 'parent-selected-native-reviewed-integration-pending', 'invocations': invocations,
                  'processing': 'None. Exact generated PNG bytes copied; no source crop, recolor, repaint, filtering or resample.'}
    (directory / 'provenance.json').write_text(json.dumps(provenance, indent=2, ensure_ascii=False))
    (directory / 'render-provenance.json').write_text(json.dumps({
        'returnedImagePath': str(tool_path), 'sha256': sha(tool_path), 'tool': 'built-in image_gen.imagegen',
        'invocationCountForTopic': len(invocations), 'invocations': invocations,
    }, indent=2))
    inventory.append({'topicId': topic, 'master': str(master.relative_to(ROOT)), 'raw': str(raw.relative_to(ROOT)),
                      'returnedPath': str(tool_path), 'sha256': sha(master), 'sourceMode': source_image.mode,
                      'size': [w, h], 'opaque': True, 'rawEqualsMasterEqualsReturned': True,
                      'genuineGenerationCount': 1, 'genuineEditCount': int(slug == '70b193fa')})
    source, source_provenance = read_material(ROOT, entry, mode, CHINA_PACK)
    sheet, samples, edges, cells = render_material(source, windows, mode, 32)
    old = OUT / 'original-reference' / f'terrain-{slug}.png'
    original = Image.open(old).convert('RGBA')
    assert sheet.getchannel('A').tobytes() == original.getchannel('A').tobytes()
    sheet.save(OUT / f'{slug}-native32.png')
    sheet.resize((sheet.width * 6, 192), Image.Resampling.NEAREST).save(OUT / f'{slug}-zoom6.png')
    for group, tile in [(panels, sheet), (originals, original)]:
        panel = Image.new('RGB', (560, 160), '#777777')
        ImageDraw.Draw(panel).text((4, 4), topic + ' | ' + mode, fill='white')
        for y, bg in [(24, 'black'), (68, 'white'), (112, '#626843')]:
            panel.paste(bg, (0, y, 560, y + 40))
            panel.paste(tile, (4, y + 4), tile)
        group.append(panel)
    grid = Image.new('RGBA', (256, 256))
    occupancy = {(x, y) for y in range(8) for x in range(8) if (x + y) % 5 != 0}
    for x, y in occupancy:
        bits = sum(bit for dx, dy, bit in [(0,-1,1),(1,0,2),(0,1,4),(-1,0,8)]
                   if (x + dx, y + dy) in occupancy)
        grid.paste(sheet.crop((bits * 32, 0, (bits + 1) * 32, 32)), (x * 32, y * 32))
    display = Image.new('RGB', (768, 256))
    for i, bg in enumerate(['black', 'white', '#626843']):
        display.paste(bg, (i * 256, 0, (i + 1) * 256, 256))
        display.paste(grid, (i * 256, 0), grid)
    display.save(OUT / f'{slug}-connected-grid-native32.png')
    display.resize((1536, 512), Image.Resampling.NEAREST).save(OUT / f'{slug}-connected-grid-zoom2.png')
    fixture_dir = OUT / 'topology-fixtures' / slug
    fixture_dir.mkdir(parents=True, exist_ok=True)
    for name, fixture in review_previews(sheet, mode).items():
        fixture.save(fixture_dir / (name + '.png'))
    full = sheet.crop((15 * 32, 0, 16 * 32, 32))
    probes = {direction: list(full.getpixel(pos)) for direction, pos in
              [('N',(16,0)), ('E',(31,16)), ('S',(16,31)), ('W',(0,16))]}
    material_probe_pass = all((p[0] > p[2] if slug == '70b193fa' else p[2] > p[0]) for p in probes.values())
    assert material_probe_pass
    reviews.append({'topicId': topic, 'tileMode': mode, 'size': list(sheet.size), 'source': source_provenance,
                    'samples': samples, 'edges': edges, 'cells': cells, 'alphaMatchesOriginal': True,
                    'originalSha256': sha(old), 'pixelSha256': pixel_hash(sheet),
                    'alphaSha256': pixel_hash(sheet.getchannel('A')),
                    'distinctCells': len({c['pixelSha256'] for c in cells}),
                    'cardinalMidpointRGBA': probes, 'cardinalMaterialProbePass': material_probe_pass,
                    'probeMeaning': 'Coarse color evidence only: warm earth R>B for levee; blue water B>R for drains. Does not replace visual topology review.'})
(OUT / 'proposed-registration.json').write_text(json.dumps({'schemaVersion': 1, 'assets': assets}, indent=2))
(OUT / 'native-review.json').write_text(json.dumps(reviews, indent=2))
(OUT / 'source-inventory.json').write_text(json.dumps(inventory, indent=2))
for name, group in [('all', panels), ('original-strips', originals)]:
    combined = Image.new('RGB', (560, len(group) * 160))
    for i, panel in enumerate(group):
        combined.paste(panel, (0, i * 160))
    combined.save(OUT / f'{name}-native32.png')
    combined.resize((1120, combined.height * 2), Image.Resampling.NEAREST).save(OUT / f'{name}-zoom2.png')

planned = plan(ROOT, OUT / 'proposed-registration.json')
assert len(planned) == 4 and all(report['density'] == 1 for _, _, report in planned)
(OUT / 'export-plan.json').write_text(json.dumps([report for _, _, report in planned], indent=2))
fixture = OUT / 'export-fixture'
fixture.mkdir(exist_ok=True)
manifest = CHINA_PACK / 'manifest-final-ancient-china-v1.json'
(fixture / manifest).parent.mkdir(parents=True, exist_ok=True)
shutil.copyfile(ROOT / manifest, fixture / manifest)
for entry in assets.values():
    path = Path(entry['source'])
    (fixture / path).parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(ROOT / path, fixture / path)
catalog = fixture / 'proposed-registration.json'
shutil.copyfile(OUT / 'proposed-registration.json', catalog)
written = export(fixture, catalog)
checked = export(fixture, catalog, check=True)
(OUT / 'isolated-export.json').write_text(json.dumps(written, indent=2))
(OUT / 'isolated-export-check.json').write_text(json.dumps(checked, indent=2))
checks = {'sourceCount': 4, 'independentlyGenerated': True,
          'sourceHashesUnique': len({item['sha256'] for item in inventory}) == 4,
          'sourceByteEquality': True, 'sourceSizes': [[1254,1254]] * 4, 'allOpaque': True,
          'genuineGenerationCount': 4, 'boundedGenuineEditCount': 1, 'sourcePostProcessing': 'none',
          'fullSquareMaterialWindows': True, 'nativeOnly': True, 'hdOutputs': 0,
          'planCount': len(planned), 'isolatedExportCount': len(written), 'isolatedCheckCount': len(checked),
          'originalAlphaEquality': all(r['alphaMatchesOriginal'] for r in reviews),
          'derivedCellCounts': [len(r['cells']) for r in reviews],
          'distinctCells': [r['distinctCells'] for r in reviews], 'allowedEdgeEquality': True,
          'cardinalMaterialProbesPass': all(r['cardinalMaterialProbePass'] for r in reviews),
          'sharedCatalogMutatedByWorker': False, 'sharedRuntimeMutatedByWorker': False,
          'sharedManifestsMutatedByWorker': False, 'sharedExporterMutatedByWorker': False,
          'gameplayConsumerCreated': False}
assert checks['sourceHashesUnique']
(OUT / 'checks.json').write_text(json.dumps(checks, indent=2))
print(json.dumps(checks, indent=2))
