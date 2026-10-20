"""Read historical mapping/pixels and compare the bounded opening-scene correction."""
import collections
import hashlib
import io
import json
from pathlib import Path
import re
import subprocess

from PIL import Image

ROOT = Path(__file__).resolve().parents[4]
OUT = Path(__file__).resolve().parent
BASE = '46cc02f'
PACK = Path('packages/story-candidate-01/assets/final-fantasy-v1')
TOPIC = 'C01-CH01-S00'


def original(path):
    return subprocess.check_output(['git', 'show', f'{BASE}:{path}'], cwd=ROOT)


def digest(data):
    return hashlib.sha256(data).hexdigest()


builder = PACK / 'tools/build_final_fantasy_v1.py'
namespace = {'__file__': str(ROOT / builder), '__name__': 'historical_mapping_audit'}
exec(compile(original(builder), str(builder), 'exec'), namespace)
catalog = Path('docs/story-candidates/candidate-01/art-assets/07-topic-catalog.md')
section = original(catalog).decode().split('## 叙事静态图，共 80 项')[1].split('## 战斗单位')[0]
ordered = re.findall(r'`(C01-[^`]+)`', section)
assert len(ordered) == 80 and ordered.index(TOPIC) == 44
assert namespace['NARRATIVE_SOURCES']['prop-board-04.png'][3] == 44
source = PACK / 'masters/narrative/props/prop-board-04.png'
cached = PACK / 'intermediate/alpha/narrative/props/prop-board-04.png'
board = Image.open(io.BytesIO(original(source)))
alpha = Image.open(io.BytesIO(original(cached))).convert('RGBA')
alpha.putdata([(r, g, b, 0) if r > 225 and b > 175 and g < 105 and a > 0 else (r, g, b, a)
               for r, g, b, a in alpha.get_flattened_data()])
cell = namespace['grid_cell'](alpha, 2, 2, 1, 1, 5)
reconstructed = namespace['fit_transparent'](cell, (128, 128), 4, False)
reconstructed.putdata([(r, g, b, 0) if r > 230 and b > 190 and g < 60 and a > 0 else (r, g, b, a)
                       for r, g, b, a in reconstructed.get_flattened_data()])
manifest = PACK / 'manifest-final-fantasy-v1.json'
old = json.loads(original(manifest))
current = json.loads((ROOT / manifest).read_text())
before = next(a for a in old['assets'] if a['topicId'] == TOPIC)
after = next(a for a in current['assets'] if a['topicId'] == TOPIC)
assert after == {**before, 'kind': 'scene'}
assert current == {**old, 'assets': [{**a, 'kind': 'scene'} if a['topicId'] == TOPIC else a for a in old['assets']]}
runtime = PACK / before['png']
data = (ROOT / runtime).read_bytes()
assert data == original(runtime)
image = Image.open(io.BytesIO(data))
assert image.tobytes() == reconstructed.tobytes()
assert image.size == (128, 128) and image.getchannel('A').getextrema() == (0, 255)
inventory = Path('docs/art-rebuild-20261019/canonical-topic-manifest.json')
old_inventory = json.loads(original(inventory))
new_inventory = json.loads((ROOT / inventory).read_text())
assert new_inventory == [{**a, 'kind': 'scene', 'alpha_requirement': 'opaque-scene'}
                         if a['topicId'] == TOPIC else a for a in old_inventory]
consumer_paths = ['packages/story-candidate-01/src/story.ts',
                  'packages/story-candidate-01/src/presentation/candidate-01-story.ts',
                  'packages/story-candidate-01/src/presentation/candidate-01-bindings.ts',
                  'packages/story-candidate-01/src/campaign.ts']
consumers = json.loads((OUT / 'consumers.json').read_text())
assert not consumers['openingSceneConsumers'] and consumers['prologue']['scene'] == 'C01-CH01-S01'
assert all(TOPIC not in (ROOT / p).read_text() for p in consumer_paths)
unchanged = ['tools/art/source-overrides.json', str(PACK / 'manifest-tactical-runtime-hd.json')]
assert all((ROOT / p).read_bytes() == original(p) for p in unchanged)
story_paths = ['docs/story-candidates/candidate-01/deep-customized/01-border-fire.md',
               'docs/story-candidates/candidate-01/deep-customized/09-static-assets.md',
               'docs/story-candidates/candidate-01/deep-novel-v2/01-border-fire-novel.md',
               'docs/story-candidates/candidate-01/deep-novel-v2/09-continuity-and-static-assets.md']
report = {
    'baseCommit': BASE, 'topicId': TOPIC,
    'decision': 'Correct intended scene classification; preserve and disclose legacy subject/alpha mismatch pending new art.',
    'canonicalDelta': {'kind': {'from': 'key-prop', 'to': 'scene'}},
    'legacyCanonicalRecord': before, 'currentCanonicalRecord': after,
    'allOtherManifestFieldsAndTopicsIdentical': True,
    'inventoryDelta': {'kind': {'from': 'key-prop', 'to': 'scene'},
                       'alpha_requirement': {'from': 'genuine-transparent-background', 'to': 'opaque-scene'}},
    'allOtherInventoryRowsAndFieldsIdentical': True,
    'originalSource': {
        'path': str(source), 'sha256': digest(original(source)), 'size': list(board.size), 'mode': board.mode,
        'catalogIndexZeroBased': 44, 'boardCellIndexZeroBased': 3, 'slot': 'bottom-right',
        'fullCell': [627, 627, 1254, 1254], 'legacyInset': [632, 632, 1249, 1249],
        'fullCellPixelSha256': digest(board.crop((627, 627, 1254, 1254)).tobytes()),
        'legacyInsetPixelSha256': digest(board.crop((632, 632, 1249, 1249)).tobytes()),
        'visualReview': 'Full board and exact full cell inspected: lone blue-gray metal-rimmed stitched bracer and tied cord on magenta, no people or hands.'},
    'legacyReconstruction': {
        'matchesRuntimeRgbaPixels': True,
        'method': 'Cached alpha, historical magenta cleanup, inset-5 bottom-right cell, alpha>=28 bbox fit at 128 square with 4px padding, final magenta cleanup',
        'cachedAlphaPath': str(cached), 'cachedAlphaSha256': digest(original(cached)),
        'builderPath': str(builder), 'builderSha256': digest(original(builder)),
        'oldBuilderUnchanged': 'Historical source mapping evidence, not the approved-source exporter'},
    'runtime': {'path': str(runtime), 'sha256': digest(data), 'byteIdentical': True,
                'size': list(image.size), 'mode': image.mode, 'alphaExtrema': list(image.getchannel('A').getextrema()),
                'status': 'Legacy transparent loose bracer retained; not an accepted opaque scene'},
    'authoredEvidence': [
        {'path': story_paths[0], 'section': '1. 章首冷开场',
         'finding': 'Roderick ties Leia’s left wristguard; image S00 and prop LEFT-GLOVE have explicit separate IDs.'},
        {'path': story_paths[1], 'section': '3. 关键场景', 'finding': 'Scene label and hand-action intimacy composition.'},
        {'path': story_paths[2], 'section': 'opening', 'finding': 'Roderick loosens and reties Leia’s wrist strap before first command.'},
        {'path': story_paths[3], 'section': 'chapter one row', 'finding': 'Old wristguard hand action; S00 and LEFT-GLOVE listed separately.'}],
    'consumerAudit': consumers,
    'consumerSourceHashes': {p: digest((ROOT / p).read_bytes()) for p in consumer_paths},
    'authoredSourceHashes': {p: digest((ROOT / p).read_bytes()) for p in story_paths},
    'unchangedRegistrations': {p: digest((ROOT / p).read_bytes()) for p in unchanged},
    'narrativeCounts': dict(collections.Counter(a['kind'] for a in current['assets'] if a['category'] == 'narrative-static')),
    'newCompletedTopics': 0, 'newNativeMasters': 0, 'newSourceOverrides': 0,
    'newHdRegistrations': 0, 'newShippedConsumers': 0, 'artworkModified': False, 'gameplayModified': False,
    'preservedPriorAudit': 'docs/art-rebuild-20261019/approved-v5/narrative-keyprop-audit/manifest.json',
}
(OUT / 'evidence.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
print('Verified source mapping/RGBA reconstruction, bounded manifest/inventory delta, unchanged runtime and selectors.')
