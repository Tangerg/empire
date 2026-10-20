#!/usr/bin/env python3
"""Record the evidence-backed square-scene correction proposal. No repo mutation."""
import json
from pathlib import Path
O=Path(__file__).resolve().parent
R=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019')
j=json.loads((O/'manifest.json').read_text())
base='docs/story-candidates/candidate-01/'
source_refs={
'FIRST-GREY-FLAG': [('deep-customized/01-border-fire.md',123,139),('deep-customized/09-static-assets.md',109,109)],
'FREE-OATH-STONE':[('deep-customized/09-static-assets.md',118,118),('deep-novel-v2/07-throne-without-king-novel.md',477,503)],
'CROWN-FRAGMENT':[('deep-customized/09-static-assets.md',137,143),('deep-novel-v2/03-elder-peoples-novel.md',99,99)],
'DRAGON-ALLIANCE-CLASP':[('deep-customized/09-static-assets.md',111,111),('deep-customized/09-static-assets.md',137,143)],
'LEFT-GLOVE':[('deep-customized/01-border-fire.md',7,15),('deep-customized/09-static-assets.md',25,25),('deep-customized/09-static-assets.md',107,107)],
'EXILE-HARDTACK':[('deep-customized/01-border-fire.md',111,137),('deep-customized/09-static-assets.md',25,25)],
'CONTROL-FRAGMENT':[('deep-customized/09-static-assets.md',25,33),('deep-customized/02-grey-banner-exile.md',76,90)],
'THREE-PEOPLES-CLASP':[('deep-customized/09-static-assets.md',25,25)],
'ADA-MEDICINE':[('deep-customized/01-border-fire.md',39,61),('deep-customized/09-static-assets.md',108,108)],
'IVRA-BROKEN-BRIDLE':[('deep-customized/02-grey-banner-exile.md',69,90),('deep-customized/09-static-assets.md',110,110)],
'EXPIRING-MANDATE':[('deep-customized/06-war-of-seven-towers.md',131,133),('deep-novel-v2/06-war-of-seven-towers-novel.md',5,5),('deep-novel-v2/06-war-of-seven-towers-novel.md',513,513)],
'NAME-TAGS':[('deep-customized/03-elder-peoples.md',9,25),('deep-customized/09-static-assets.md',113,113)],
'CASUALTY-REGISTER':[('deep-customized/01-border-fire.md',29,35),('deep-novel-v2/01-border-fire-novel.md',139,153)],
'GRANARY-KEY':[('deep-novel-v2/02-grey-banner-exile-novel.md',163,165)],
'EVACUATION-MAP':[('art-assets/07-topic-catalog.md',92,92)],
'CH01-S00':[('deep-customized/09-static-assets.md',65,69),('deep-customized/01-border-fire.md',7,15)],
'CH03-S11':[('deep-customized/09-static-assets.md',65,81),('deep-customized/03-elder-peoples.md',7,25)]}
for t in j['topics']:
 key=t['topicId'].removeprefix('C01-PROP-') if t['topicId'].startswith('C01-PROP-') else t['topicId'].removeprefix('C01-')
 refs=[]
 # Canonical authored catalog entry is always the identity/label source.
 p=R/base/'art-assets/07-topic-catalog.md'
 for i,line in enumerate(p.read_text().splitlines(),1):
  if '`'+t['topicId']+'`' in line:refs.append({'path':str(p.relative_to(R)),'lines':[i,i],'evidence':'exact canonical topic and label'})
 for f,start,end in source_refs[key]:
  p=R/base/f;lines=p.read_text().splitlines();assert end<=len(lines)
  refs.append({'path':str(p.relative_to(R)),'lines':[start,end],'evidence':'authored lore/asset brief; source image remains the evidence of current depicted design'})
 t['semanticSourceReferences']=refs
 t['visualReview']['sourceVsLoreResolution']='Preserve current source identity; any newly required semantic redesign is an explicit art brief decision, not an automatic correction.'
 if key=='CH03-S11':
  t['proposedExportClassification']='opaque-scene-v1-after-documented-canonical-kind-correction'
  t['proposedSourceAlpha']='complete opaque RGB or RGBA square scene; reusable isolated name tags remain separate PROP-NAME-TAGS'
  t['visualReview']['riskOrHold']='Evidence-backed metadata correction: key-prop→scene, preserving topic/path/128×128/opaque pixels. Original and lore both identify the cemetery scene; no semantic redesign required.'
  t['visualReview']['sourceVsLoreResolution']='Original painting and authored scene brief agree; only canonical kind/transparency classification conflicts. Correct that classification explicitly instead of indefinitely holding the scene.'
  t['proposedCanonicalChange']={'onlyChangedCanonicalField':{'kind':{'from':'key-prop','to':'scene'}},'preserve':['topicId','assetId','label','png','width=128','height=128','no anchor','no frames','current opaque runtime pixels','pending art status','existing separate PROP-NAME-TAGS identity'],'requiresArtworkRedesign':False,'performed':False}
proposal=json.loads((O/'proposed-export-contract.json').read_text())
proposal['canonicalEligibility']['currentSemanticHolds']=['C01-CH01-S00']
proposal['canonicalEligibility']['classificationCorrectionExcludedFromTransparentPath']=['C01-CH03-S11']
proposal['fullsceneExceptions']['C01-CH03-S11']='Propose documented canonical kind correction key-prop→scene, preserving128×128, path, topic, label and opaque current pixels. Both original source and authored scene brief agree. Then share opaque-scene-v1 with ratio derived from declared canonical geometry; no fullscene-in-prop fallback and no automatic16:9 recrop.'
proposal['squareSceneCorrection']={'status':'proposed, not applied','topicId':'C01-CH03-S11','authoritativeChange':'canonical kind key-prop→scene only;128×128 retained; update derived classification/transparency documentation','renderer':'existing render_scene; same opaque-scene-v1 schema','boundedGeometry':'accept current existing256×144 and documented128×128 scene shapes; keep unframed/no-anchor constraints','aspectRule':'(cropRight-cropLeft)*asset.height == (cropBottom-cropTop)*asset.width','sourceMinimum':'compare source region against canonical width/height times registered density, never hardcoded256/144','pixelOperations':'complete square source when sourceCropBox=null, or explicit reviewed square crop; opaque master, no inferred center cover and no stretch','hd':'none needed for metadata correction; if explicitly registered later, same exact physical/logical ratio and source sampling rules; runtime scene validator/tests must accept documented square shape','tests':['classification change does not alter existing128×128 PNG bytes/path or create a new canonical topic','square scene accepts complete square source and rejects16:9 region, transparent source, frames/anchor','existing41 wide scenes retain256×144 and16:9 behavior','integrated scene tests derive expectations from canonical geometry; update source-native-HD test mock by topic/shape rather than narrative directory','incorrect key-prop opaque entry still rejects; CH01-S00 remains a decision because original and planned scene differ']}
proposal['requiredTests']=[s.replace('CH03-S11 and CH01-S00 remain held; opaque scene entry cannot bypass canonical kind/size checks; existing character and scene tests continue passing','CH03-S11 classification correction is documented with128×128 unchanged and remains opaque; CH01-S00 remains a genuine mismatch; opaque scene entry cannot bypass canonical kind/size checks; existing character and wide-scene tests continue passing') for s in proposal['requiredTests']]
(O/'manifest.json').write_text(json.dumps(j,ensure_ascii=False,indent=2)+'\n')
(O/'proposed-export-contract.json').write_text(json.dumps(proposal,ensure_ascii=False,indent=2)+'\n')
p=O/'README.md';s=p.read_text()
s=s.replace('HOLD full-scene exception. Preserve opaque128×128 and key-prop kind until explicit classification/identity decision; never chroma-key this scene or paint transparent holes into it.','Evidence-backed classification correction: change canonical kind to scene while preserving opaque128×128, exact topic/path and pixels; never chroma-key or redesign this cemetery.')
s=s.replace('Keep its current bytes/kind/size unchanged and held. `PROP-NAME-TAGS` already owns isolated markers. Do not secretly remap, chroma-key the cemetery, or weaken `opaque-scene-v1` to accept wrong kinds. If the user later chooses scene reclassification, change the authoritative canonical contract and callers/tests together; do not introduce a magic fallback.','Both original source and authored lore unequivocally identify a scene: deep-customized09 section3 (lines65–81) lists CH03-S11 under scenes, and deep-customized03 lines7–25 separately names the cemetery scene and PROP-NAME-TAGS prop. **Propose an explicit canonical kind correction from key-prop to scene while retaining128×128, exact topic/asset/path/label, current opaque pixels, absent anchors/frames and pending remake status.** This is a documented classification correction, not semantic redesign or a new topic. No change was made in this audit. `PROP-NAME-TAGS` already owns isolated markers. Do not secretly remap, chroma-key the cemetery, recrop it to16:9, or invent a fullscene-in-prop fallback. Extend the existing opaque-scene renderer coherently to the declared square scene geometry as described below.')
s=s.replace('Keep both chapter exceptions on hold through registration policy; do not fabricate a second geometry owner','Keep CH01-S00 on hold and exclude CH03-S11 from transparent registration while its documented scene classification is corrected; do not fabricate a second geometry owner')
addition='''
### Documented square-scene correction for CH03-S11

Current original+lore evidence is sufficient to recommend correcting its canonical kind to `scene`, with **128×128 preserved**. No generation or semantic redesign is needed to establish that classification. Apply it as a separate coherent classification change when authorized, update the derived inventory/transparency description, and keep the existing runtime image unchanged until a reviewed remake exists. It still counts as one pending canonical topic.

Reuse `opaque-scene-v1` rather than adding a duplicate exporter. Let the existing `render_scene` accept the current256×144 shape and the documented128×128 square scene shape, keeping all unframed/no-anchor/opaque-master checks. Derive exact crop aspect by `(right-left)*asset.height == (bottom-top)*asset.width` and source minimum size from canonical dimensions and density. `sourceCropBox:null` on a square native master means the whole source; a reviewed crop, if needed, must be square. Do not stretch or automatically16:9-crop it.

No HD registration or resolver change is required merely to correct the canonical kind. Before a future square-scene HD registration, update `validateCandidate01HdAsset`'s scene geometry guard and integrated scene tests to accept the square logical shape and exact physical ratio. Keep existing wide-scene sizes/ratios unchanged. The current directory-wide density-test mock also needs correction because it assumes all narrative physical images are1664×936. Proposed tests cover byte-preserved classification, correct square source/crop handling, wrong aspect/alpha rejection, unchanged wide scenes and no key-prop bypass.

'''
s=s.replace('## Verification and proposed regression coverage',addition+'## Verification and proposed regression coverage')
s=s.replace('`build-audit.py`, `finalize-audit.py`, `collect-consumers.cjs`:reproducible shared-output-only tooling','`build-audit.py`, `finalize-audit.py`, `classification-addendum.py`, `collect-consumers.cjs`:reproducible shared-output-only tooling. Run collect, build, finalize, then classification addendum in that order')
s=s.replace('visual/semantic review and exporter failure evidence','visual/semantic review, exact local lore source references and exporter failure evidence')
p.write_text(s)
print('Recorded square-scene classification correction proposal and per-topic lore references')
