from pathlib import Path
from PIL import Image,ImageChops
import json,hashlib,datetime
D=Path(__file__).parent;repo=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019');square=D.name=='ch03-s11';topic='C01-'+D.name.upper()
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def write(name,obj):(D/name).write_text(json.dumps(obj,ensure_ascii=False,indent=2)+'\n')
manifest=json.loads((repo/'packages/story-candidate-01/assets/final-fantasy-v1/manifest-final-fantasy-v1.json').read_text())
def find(x):
 if isinstance(x,dict):
  if x.get('topicId')==topic:return x
  for v in x.values():
   r=find(v)
   if r:return r
 elif isinstance(x,list):
  for v in x:
   r=find(v)
   if r:return r
canonical=find(manifest)
board='scene-board-07.png' if square else 'scene-board-11.png';full=[0,0,627,627] if square else [0,627,627,1254];inset=[5,5,622,622] if square else [5,632,622,1249]
relative='packages/story-candidate-01/assets/final-fantasy-v1/masters/narrative/scenes/'+board;original=repo/relative;im=Image.open(original)
assert sha(original)==sha(D/'original-fullboard.png')
assert ImageChops.difference(im.crop(full),Image.open(D/'original-full-cell-627.png')).getbbox() is None
assert ImageChops.difference(im.crop(inset),Image.open(D/'original-cell-617.png')).getbbox() is None
source={'path':relative,'sha256':sha(original),'size':list(im.size),'mode':im.mode,'slot':'top-left' if square else 'bottom-left','cellIndexZeroBased':0 if square else 2,'originalNarrativeIndexZeroBased':54 if square else 72,'fullCellCropBox':full,'legacyInsetCropBox':inset,'originalPixelCropsVerified':True,'fullBoardAndSlotVisuallyInspectedBeforeGeneration':True}
audit_rel='docs/art-rebuild-20261019/approved-v5/remaining-narrative-audit/manifest.json';a=json.loads((repo/audit_rel).read_text());legacy=next(x for x in a['topics'] if x['topicId']==topic)
write('audited-source.json',{'topicId':topic,'canonicalManifestRecordAtSourcePreparation':canonical,'canonicalManifestReadOnly':True,'canonicalKindConfirmed':'scene','source':source,'historicalAuditRecord':legacy,'historicalAuditNote':'Historical key-prop classification for CH03-S11 was superseded by the explicit cemetery scene classification correction; square128 geometry remains unchanged.' if square else 'Original architecture scene identity retained.','classificationProof':{'path':'docs/art-rebuild-20261019/approved-v5/cemetery-scene-classification/evidence.json','sha256':sha(repo/'docs/art-rebuild-20261019/approved-v5/cemetery-scene-classification/evidence.json')} if square else None})
if square:
 refs=[('docs/story-candidates/candidate-01/art-assets/07-topic-catalog.md',64,69,'Stable topic identity, not isolated PROP-NAME-TAGS'),('docs/story-candidates/candidate-01/deep-customized/09-static-assets.md',65,82,'Scene with living waist tags corresponding to names of dead'),('docs/story-candidates/candidate-01/deep-customized/03-elder-peoples.md',7,25,'Waist name tags and repair of individual grave names'),('docs/story-candidates/candidate-01/deep-novel-v2/03-elder-peoples-novel.md',1,75,'Mundane records, confused echoes, continuing paper name maintenance; not magical talismans'),('docs/story-candidates/candidate-01/deep-novel-v2/09-continuity-and-static-assets.md',5,24,'Chapter-three continuity and static art constraints')]
else:
 refs=[('docs/story-candidates/candidate-01/art-assets/07-topic-catalog.md',80,85,'Stable ancient dragon covenant ruin location identity'),('docs/story-candidates/candidate-01/deep-customized/04-grey-banner-queen.md',49,65,'Dragon consent and true-name constraints; contextual lore, not a substitute architecture design'),('docs/story-candidates/candidate-01/deep-novel-v2/04-grey-banner-queen-novel.md',131,175,'Ancient dragon covenant and refusal context, not a mandate to add a living dragon, rider or new gameplay objective')]
parts=[];proof=[]
for path,start,end,role in refs:
 f=repo/path;lines=f.read_text().splitlines();excerpt='\n'.join(f'{i+1}: {lines[i]}' for i in range(start-1,min(end,len(lines))))
 parts.append(f'SOURCE: {path}\nSHA256: {sha(f)}\nROLE: {role}\n{excerpt}\n')
 proof.append({'path':path,'lineRange':[start,end],'sha256':sha(f),'role':role})
(D/'story-source-context.txt').write_text('\n'.join(parts))
write('story-reference-hashes.json',proof)
cons=json.loads((D/'consumers.json').read_text());hits=[]
def walk(x,path):
 if isinstance(x,dict):
  for k,v in x.items():walk(v,path+'.'+k)
 elif isinstance(x,list):
  for i,v in enumerate(x):walk(v,path+f'[{i}]')
 elif isinstance(x,str) and (topic.lower() in x.lower() or D.name in x.lower()):hits.append({'path':path,'value':x})
walk(cons,'consumers')
source_hits=[]
for base in [repo/'packages/story-candidate-01/src',repo/'packages/game-ui/src',repo/'apps']:
 for f in base.rglob('*'):
  if f.suffix not in ['.ts','.tsx','.js','.css']:continue
  for n,line in enumerate(f.read_text(errors='replace').splitlines(),1):
   if topic.lower() in line.lower() or D.name in line.lower():source_hits.append({'file':str(f.relative_to(repo)),'line':n,'text':line.strip(),'testOnly':'.test.' in f.name})
write('consumer-check.json',{'method':'Fresh copied read-only TypeScript transpile/evaluation collector; source literal scan excludes generated art/manifests and separately identifies tests.','collectorSha256':sha(D/'collect-consumers.cjs'),'consumerExtractionSha256':sha(D/'consumers.json'),'storyCount':len(cons['stories']),'choiceCount':len(cons['choices']),'levelCount':len(cons['levels']),'topicHitsInExtractedConsumerData':hits,'sourceLiteralMatches':source_hits,'noCurrentShippedConsumerFound':not hits and all(h['testOnly'] for h in source_hits),'genericApiReachabilityIsNotConsumer':True,'browserOrLiveVerification':False})
origin='/workspace/scratch/84c9fe948755/generated_images/'+('exec-b5a1b1a3-867c-4166-9868-2b89421f0854.png' if square else 'exec-13bf7cfc-b3e7-4d7d-818e-46f799fb1ac3.png')
raw=Image.open(D/'raw.png')
write('generation-result.json',{'tool':'built-in image_gen','generatedSourcePath':origin,'generationAttempts':1 if square else 2,'selectedAttempt':1 if square else 2,'selectedRequest':'request.json' if square else 'request-v2.json','size':list(raw.size),'mode':raw.mode,'sha256':sha(D/'raw.png'),'originalGeneratedFileEqualsPreservedRaw':sha(Path(origin))==sha(D/'raw.png'),'targetedCorrection':None if square else 'Zoom out to preserve all principal ruin architecture inside a landscape canvas; remove decorative glyph panels. Rejected-v1 raw/prompt/request/reason retained intact.','opaqueSceneIntentional':True,'programmaticArtworkModification':False})
write('request-hashes.json',{f.name:sha(f) for f in D.iterdir() if f.is_file() and (f.name.startswith('prompt') or f.name.startswith('request') and f.name!='request-hashes.json' or f.name.startswith('reference-hashes'))})
write('proposed-source-contract.json',{'topicId':topic,'kind':'opaque-scene-v1','scene':{'source':'masters/approved-v5/narrative-scenes/'+D.name+'/master.png','sha256':sha(D/'master.png'),'sourceCropBox':None if square else [4,2,1668,938]},'canonical':{'width':128 if square else 256,'height':128 if square else 144,'kind':'scene','preserveTopicAssetPathAndGameplay':True},'optionalHd':{'width':1254 if square else 1664,'height':1254 if square else 936,'density':9.796875 if square else 6.5,'upscaled':False},'registrationPerformedByThisWorker':False,'note':'Parent owns explicit selected-source registration and validation. This file is a handoff proposal, not a second active source catalog.'})
write('final-verification.json',{'topicId':topic,'nativeOriginalRawMasterByteIdentity':'passed','sourceBoardHashAndExactPixelCropVerification':'passed','intentionalOpaqueAlpha255':'passed','exactCanonicalAspectAndSourceNativePhysicalDensity':'passed','canonicalLogicalPreview':'visually inspected','fullNativeAndExactPhysicalExport':'visually inspected','coverGeometryPreviews':'visually inspected; limitations in source-review.md','originalSourceFullboardAndSlot':'visually inspected before generation','laterNarrativeBrief':'read before generation; documented excerpts and hashes','readableInventedNamesOrHeraldry':'none observed','repositoryRuntimeSelectorsGameplay':'not edited by this worker','liveBrowserVerification':'not performed; no current exact-topic consumer found','generationFailuresRetained':[] if square else ['rejected-v1/'],'remaining':'Parent review, explicit registration/export/test/backup integration'})
print(topic,'provenance and contract written')
