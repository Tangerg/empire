import hashlib
import json
from pathlib import Path
import shutil
import sys
from PIL import Image
sys.path.insert(0, str(Path.cwd() / 'tools/art'))
from export_overrides import PACKS, plan

main = Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019')
shared = Path('/workspace/shared/empire-art-rebuild/approved-v5')
out = shared / 'narrative-prop-export-code'
fixture = out / 'real-source-fixture'
pack = Path('packages/story-candidate-01/assets/final-fantasy-v1')
for campaign, directory, suffix in PACKS:
    relative = Path('packages') / campaign / 'assets' / directory
    target = fixture / relative
    target.mkdir(parents=True, exist_ok=True)
    name = f'manifest-final-{suffix}-v1.json'
    shutil.copyfile(main / relative / name, target / name)
assets = {}
source_before = {}
for item in json.loads((shared / 'narrative-props/first-four-delivery.json').read_text())['topics']:
    topic = item['topicId']
    slug = topic.removeprefix('C01-').lower()
    source = pack / 'masters/approved-v5/narrative-props' / slug / 'master.png'
    raw = Path(item['master']).read_bytes()
    digest = hashlib.sha256(raw).hexdigest()
    assert digest == item['sha256']
    (fixture / source).parent.mkdir(parents=True, exist_ok=True)
    (fixture / source).write_bytes(raw)
    source_before[Path(item['master'])] = digest
    assets[topic] = {'kind': 'transparent-prop-v1', 'prop': {'source': str(source), 'sha256': digest}}
cat = out / 'four-source-planned-catalog.json'
cat.write_text(json.dumps({'schemaVersion': 1, 'assets': assets}, indent=2) + '\n')
fixture_before = {p: hashlib.sha256(p.read_bytes()).hexdigest() for p in fixture.rglob('*') if p.is_file()}
report = []
for topic, provenance, target, rendered in plan(fixture, cat):
    slug = topic.removeprefix('C01-').lower()
    qa = shared / 'narrative-props' / slug / 'QA/contain-128.png'
    with Image.open(qa) as expected:
        assert rendered.mode == expected.mode == 'RGBA'
        assert rendered.size == expected.size == (128, 128)
        assert rendered.tobytes() == expected.tobytes(), topic
    rendered.save(out / f'{slug}-planned.png')
    report.append({'topicId': topic, **provenance, 'plannedCanonicalTarget': str(target.relative_to(fixture)),
        'size': list(rendered.size), 'pixelSha256': hashlib.sha256(rendered.tobytes()).hexdigest(),
        'workerQA': str(qa), 'workerQAPixelsMatch': True})
assert source_before == {p: hashlib.sha256(p.read_bytes()).hexdigest() for p in source_before}
assert fixture_before == {p: hashlib.sha256(p.read_bytes()).hexdigest() for p in fixture.rglob('*') if p.is_file()}
# Also check the first two already copied into main, without exporting or changing it.
main_catalog = out / 'two-source-planned-catalog.json'
main_catalog.write_text(json.dumps({'schemaVersion': 1, 'assets': {topic: assets[topic] for topic in
    ['C01-PROP-LEFT-GLOVE', 'C01-PROP-EXILE-HARDTACK']}}, indent=2) + '\n')
for topic, provenance, target, rendered in plan(main, main_catalog):
    with Image.open(out / f'{topic.removeprefix("C01-").lower()}-planned.png') as expected:
        assert rendered.tobytes() == expected.tobytes()
result = {'mode': 'read-only plans; writes only in shared evidence/fixture directories',
          'registeredSources': len(assets), 'sourcesAndFixtureUnchangedByPlan': True,
          'mainTwoSourcePlanMatches': True, 'outputs': report}
(out / 'four-source-planned-comparison.json').write_text(json.dumps(result, indent=2) + '\n')
print('4/4 native sources pass plan and match aligned worker QA pixel-for-pixel; main first two also match. No repository writes.')
