from pathlib import Path
import json,hashlib
B=Path(__file__).parent
poses=['idle','walk-a','carry-or-work','walk-b'];reg=json.loads((B/'registration-evidence.json').read_text());source=json.loads((B/'source-evidence.json').read_text());reviews=json.loads((B/'qa-review.json').read_text())['reviews']
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
bundle={'topicId':'C01-MISSION-BAKER','assetId':'C01-FFV1-MISSION-BAKER','category':'mission-unit','status':'Native source quartet and static QA complete; coordinator formal export/integration pending','frameOrder':poses,'generationTool':'built-in image_gen.imagegen','nativeArtPolicy':'Every selected raw/master is byte-identical. No cleanup, painting, recoloring, cropping, per-pose rescaling or source padding. QA composites and technical export proposals are separate only.','sourceEvidence':'source-evidence.json','registrationEvidence':'registration-evidence.json','qaReview':'qa-review.json','originalLogicalRuntimeContract':source['logicalRuntime'],'originalHDRuntimeContract':source['hdRuntime'],'actualConsumers':[],'domainActorCount':0,'scenePlacementCount':0,'consumerClassification':'catalog-only','palettePolicy':'Original warm-tan skin, cream rolled sleeves, brown apron/trousers/boots, dark brown scored loaves, golden-brown wicker basket and wooden baking paddle. No faction recolor.','semanticContract':'idle/walk-a/carry-or-work/walk-b. Catalog availability does not imply authored gameplay use.','liveGameAcceptance':False,'rejectedAttempts':[],'poses':{}}
summary={'idle':'Both hands hold bread basket at waist, paddle tucked at screen-right; both boots planted.','walk-a':'Screen-right boot extended heel-first with dark sole visible; screen-left boot planted. Same two basket support hands, bread and paddle.','carry-or-work':'Screen-left hand cradles basket, screen-right hand extends same paddle carrying one scored loaf. Two basket loaves clearly visible; no duplicated hand or paddle.','walk-b':'Screen-right boot planted; screen-left rear heel lifted and knee bent. Opposite support to walk-a, same basket and paddle.'}
for p in poses:
 q=json.loads((B/p/'alpha-qa.json').read_text());bundle['poses'][p]={'raw':f'{p}/raw.png','master':f'{p}/master.png','exactPrompt':f'{p}/prompt.txt','request':f'{p}/request.json','generationResult':f'{p}/generation-result.json','requestProvenance':f'{p}/request-provenance.json','alphaQA':f'{p}/alpha-qa.json','sha256':sha(B/p/'master.png'),'nativeSize':q['nativeSize'],'sourceFoot':reg['poses'][p]['sourceSupportAnchor'],'registrationOffsetLogical':reg['poses'][p]['registrationOffsetLogical'],'review':summary[p]}
(B/'bundle-manifest.json').write_text(json.dumps(bundle,indent=2)+'\n')
readme='''# Baker source quartet

C01-MISSION-BAKER only. No repository edits or Git operations.

## Delivery

- Four immutable native RGBA raw/master pairs in idle, walk-a, carry-or-work, walk-b
- Exact prompt, request, generation-result metadata, reference hashes and source hashes per pose
- Source board / row / idle identity references, approved family references, and source-evidence.json
- registration-evidence.json: measured two-boot sole bands, fixed44/900 scale, whole-canvas translations, common1586×992 source proposal,104×64 frame at origin[8,4]
- qa-native44-three-backgrounds.png and3x, family native44 and3x, full native composites, montage comparisons and6.25fps two-pose walk GIF
- bundle-manifest.json, qa-review.json, SHA256SUMS.json and reproducible proof scripts

## Interpretation

The exact original board row is baker-miner-artisan-bridge row1. The topic is catalog-only: no current domain actors or scene placements. Four static source poses complete this art bundle; no live deployment or runtime animation acceptance is implied.

Idle, walk-a and walk-b preserve both hands on the basket and the original paddle tucked at screen-right. Work moves the far screen-right hand to the same paddle, presenting a loaf, while the near screen-left hand continues supporting the basket. The visible work basket contains two large loaves; no exact loaf-count continuity is claimed. There are only two hands and one paddle.

Native source pixels remain untouched. Perimeter maxalpha1/0/0/0 passes the <=1 gate. Faint alpha residues are retained. Whole-canvas source placements fit the proposed frame; work correction is+5.194444logical x to account for its left-shifted body and extended paddle. Coordinator accepted this exact measured translation with an explicit6px per-topic bound. No per-pose fitting or source cleanup is used.

Formal exporter/test/build/integration ownership remains with the coordinator. Runtime and live-game tests were not run here.
'''
(B/'README.md').write_text(readme)
(B/'SHA256SUMS.json').write_text(json.dumps({str(p.relative_to(B)):sha(p) for p in sorted(B.rglob('*')) if p.is_file() and p.name!='SHA256SUMS.json'},indent=2)+'\n')
print(json.dumps({'topicId':bundle['topicId'],'poseCount':len(bundle['poses']),'files':len(json.loads((B/'SHA256SUMS.json').read_text())),'root':str(B)}))
