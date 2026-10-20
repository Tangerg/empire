import hashlib
import json
import shutil
import sys
import tempfile
from pathlib import Path
from PIL import Image, ImageDraw
ROOT = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(ROOT / 'tools/art'))
from export_overrides import fit_canvas, validate_alpha, plan, export, PACKS
DOC = Path(__file__).resolve().parent
PACK = ROOT / 'packages/story-candidate-03/assets/final-ancient-china-v1'
spec = json.loads((DOC / 'generation-spec.json').read_text())
calls = ['9dd86cad-080d-4a05-ac44-e48a14805227', '4b26ccf1-e7aa-4787-acf0-2b14769b027e', 'eb741ca4-549c-4265-af8d-3f0dd0e8b014', '6577ff2f-b7bb-42b4-869a-fdea9773396a']
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def write(path, value): path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n')
assets = {a['topicId']: a for a in json.loads((PACK / 'manifest-final-ancient-china-v1.json').read_text())['assets']}
registry = json.loads((ROOT / 'tools/art/source-overrides.json').read_text())['assets']
progress = json.loads((ROOT / 'docs/art-rebuild-20261019/approved-v5/progress.json').read_text())
canonical = {a['topicId']: a for a in json.loads((ROOT / 'docs/art-rebuild-20261019/canonical-topic-manifest.json').read_text())}
cat = {}; rows = []; integrity = []; sources = []
sheet = Image.new('RGB', (380, 364), '#dddddd'); draw = ImageDraw.Draw(sheet)
for j, name in enumerate(['white', 'black', 'olive']): draw.text((15+j*122, 4), name, fill='black')
for i, (a, call) in enumerate(zip(spec['assets'], calls)):
    topic = a['topicId']; record = assets[topic]
    assert record['width'] == record['height'] == 48 and record['anchor'] == [24, 24]
    assert topic not in progress['topicStages']
    previous = registry.get(topic)
    assert previous is None or '/approved-v5/' not in previous['source']
    dest = PACK / 'masters/approved-v5/equipment' / topic
    dest.mkdir(parents=True, exist_ok=True)
    raw = ROOT.parent / 'generated_images' / ('exec-' + call + '.png')
    for name in ['raw.png', 'master.png']:
        target = dest / name
        if target.exists(): assert target.read_bytes() == raw.read_bytes(), 'Refusing to replace a different source'
        else: shutil.copyfile(raw, target)
    (dest / 'prompt.txt').write_text(spec['shared'] + a['subject'] + '\n')
    source = Image.open(dest / 'master.png'); source.load(); sources.append(source)
    source_alpha = validate_alpha(source, topic)
    native, contract = fit_canvas(source, (48, 48), 2)
    native.save(DOC / (topic + '-48.png'))
    output_alpha = validate_alpha(native, topic + ' native48'); assert output_alpha['edgeMaxAlpha'] == 0
    current = PACK / record['png']; preserved_runtime = DOC / (topic + '-before-runtime.png')
    shutil.copyfile(current, preserved_runtime)
    legacy = PACK / 'masters/remake-20261019/equipment' / topic / 'original-evidence.png'
    legacy = legacy if legacy.exists() else current
    shutil.copyfile(legacy, DOC / (topic + '-legacy-native.png'))
    evidence = {
        'topicId': topic, 'assetId': record['assetId'], 'label': record['label'],
        'tool': 'built-in image_gen.imagegen', 'transparent_background': True,
        'rawGeneratedSource': str(raw), 'sha256': sha(raw), 'rawMasterByteIdentical': raw.read_bytes() == (dest/'raw.png').read_bytes() == (dest/'master.png').read_bytes(),
        'promptFile': 'prompt.txt', 'generationAttempts': 1, 'boundedCorrectionUsed': False,
        'references': [{'path': p, 'sha256': sha(ROOT/p), 'role': 'Visually inspected for design; not passed to imagegen for this new generation.'} for p in spec['visuallyInspectedReferences']],
        'referenceDelivery': spec['referenceDelivery'], 'identityReconstruction': a['identity'],
        'identityEvidence': ['packages/story-candidate-03/assets/final-ancient-china-v1/tools/build_final_ancient_china_v1.py', 'docs/art-rebuild-20261019/canonical-topic-manifest.json', 'docs/story-candidates/candidate-03/art-assets/02-asset-scope.md', 'docs/story-candidates/candidate-03/art-assets/07-topic-catalog.md', 'packages/story-candidate-03/assets/final-ancient-china-v1/ART-DIRECTION.md'],
        'canonicalEntry': canonical[topic], 'currentManifestEntry': record,
        'builderAudit': next(a for a in json.loads((DOC/'legacy-builder-audit.json').read_text())['assets'] if a['topicId']==topic),
        'builderAuditPath': str((DOC/'legacy-builder-audit.json').relative_to(ROOT)),
        'approvedV5SelectionProgressSnapshot': progress['formalRuntimeBoundTopics'],
        'reservationEvidence': {'noApprovedV5StageAtSelection': True, 'priorRegistryEntry': previous, 'priorRegistryDoesNotCountAsApprovedV5': True},
        'legacySlot': {'path': str(legacy.relative_to(ROOT)), 'sha256': sha(legacy), 'preservedCopy': str((DOC/(topic+'-legacy-native.png')).relative_to(ROOT))},
        'beforeRuntime': {'path': str(current.relative_to(ROOT)), 'sha256': sha(current), 'preservedCopy': str(preserved_runtime.relative_to(ROOT))},
        'sourceSize': list(source.size), 'sourceAlpha': source_alpha, 'exportContract': contract,
        'canonicalNativeContract': {'size': [48,48], 'anchor': [24,24], 'padding': 2}, 'outputAlpha': output_alpha,
        'postprocessing': 'None. Raw/master are byte-identical native generation. Review exports use the existing whole-canvas contain only; no crop, repaint, recolor, alpha surgery or pixel cleanup.',
        'registered': False, 'runtimeIntegrated': False, 'liveGameVerified': False,
    }
    write(dest/'source-evidence.json', evidence); write(dest/'alpha-qa.json', {'source': source_alpha, 'output': output_alpha})
    cat[topic] = {'source': str((dest/'master.png').relative_to(ROOT)), 'sha256': sha(raw)}
    rows.append({**record, **cat[topic], 'evidence': str((dest/'source-evidence.json').relative_to(ROOT))})
    integrity.append({'topicId': topic, 'sha256': sha(raw), 'rawMasterByteIdentical': evidence['rawMasterByteIdentical'], 'source': source_alpha, 'native48': output_alpha})
    draw.text((8, 22+i*84), topic, fill='black')
    for j, color in enumerate(['white','black','#727b43']):
        tile = Image.new('RGBA',(48,48),color); tile.alpha_composite(native); sheet.paste(tile.convert('RGB'), (15+j*122,42+i*84))
    source_review = Image.new('RGBA', (source.width*3, source.height))
    for j, color in enumerate(['white','black','#727b43']):
        tile=Image.new('RGBA',source.size,color); tile.alpha_composite(source); source_review.paste(tile,(j*source.width,0))
    source_review.convert('RGB').save(DOC/(topic+'-source-white-black-olive.png'))
sheet.save(DOC/'native-review-white-black-olive.png')
full=Image.new('RGBA',(2508,2508),'#727b43')
for i, source in enumerate(sources): full.alpha_composite(source,(i%2*1254,i//2*1254))
full.convert('RGB').save(DOC/'full-source-review.png')
contact=Image.new('RGB',(960,4*340),'#dddddd');dr=ImageDraw.Draw(contact)
for i, a in enumerate(spec['assets']):
    dr.text((8,i*340+3),a['topicId'],fill='black')
    for j,color in enumerate(['white','black','#727b43']):
        tile=Image.new('RGBA',sources[i].size,color);tile.alpha_composite(sources[i]);tile=tile.resize((320,320),Image.Resampling.LANCZOS);contact.paste(tile.convert('RGB'),(j*320,i*340+20))
contact.save(DOC/'full-source-review-white-black-olive.png')
catalog=DOC/'integration-candidates.json';write(catalog,{'schemaVersion':1,'assets':cat})
planned=plan(ROOT,catalog)
for topic,_,_,image in planned:
    with Image.open(DOC/(topic+'-48.png')) as native: assert native.mode==image.mode and native.size==image.size and native.tobytes()==image.tobytes()
write(DOC/'export-plan-qa.json', {'count':len(planned),'nativePreviewsMatchUnchangedPlan':True,'assets':[{'topicId':t,'provenance':p,'target':str(target.relative_to(ROOT)),'size':list(im.size)} for t,p,target,im in planned]})
with tempfile.TemporaryDirectory(prefix='c03-equipment-batch05-') as tmp:
    isolated=Path(tmp)
    for campaign,directory,suffix in PACKS:
        source_pack=ROOT/'packages'/campaign/'assets'/directory
        target_pack=isolated/'packages'/campaign/'assets'/directory; target_pack.mkdir(parents=True)
        for name in [f'manifest-final-{suffix}-v1.json','manifest-tactical-runtime-hd.json']:
            if (source_pack/name).exists():shutil.copyfile(source_pack/name,target_pack/name)
    for entry in cat.values():
        target=isolated/entry['source'];target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(ROOT/entry['source'],target)
    isolated_catalog=isolated/'integration-candidates.json';write(isolated_catalog,{'schemaVersion':1,'assets':cat})
    written=export(isolated,isolated_catalog)
    checked=export(isolated,isolated_catalog,check=True)
    assert written==checked and len(checked)==4
    write(DOC/'isolated-export-check.json',{'passed':True,'count':4,'scope':'Temporary isolated root with unchanged canonical manifests and selected masters. Production runtime was never written.','exportAndCheckReportsIdentical':True,'assets':checked})
write(DOC/'batch-alpha-integrity-qa.json',{'count':4,'sourceEdgeThreshold':1,'nativeRequiredEdgeMax':0,'assets':integrity})
write(DOC/'delivery-manifest.json',{'status':'integration candidates only; parent review and integration pending','assets':rows})
print('Saved four byte-identical sources; isolated plan, export and check passed; native48 edges are zero.')
