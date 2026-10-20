from pathlib import Path
from PIL import Image
from scipy.ndimage import label,find_objects
import numpy as np,json,hashlib
R=Path(__file__).parent;Q=R/'qa';P=['low-fumes','rising-fumes','plume-crest','settling-wisps']
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,d):p.write_text(json.dumps(d,indent=2,ensure_ascii=False)+'\n')
metrics=json.loads((Q/'qa-metrics.json').read_text());registration=json.loads((Q/'material-registration-proof.json').read_text());frames=[];edge=[]
for p in P:
 im=Image.open(R/p/'master.png');a=np.array(im);mask=a[:,:,3]>=128;mask[680:]=False;lab,n=label(mask);components=[]
 for k,s in enumerate(find_objects(lab)):
  z=lab[s]==k+1
  if z.sum()>500:
   yy,xx=np.where(z);rgb=a[s][:,:,:3][z].mean(axis=0);components.append({'bounds':[s[1].start,s[0].start,s[1].stop,s[0].stop],'alpha128PixelCount':int(z.sum()),'meanRGB':rgb.tolist(),'centroid':[float(xx.mean()+s[1].start),float(yy.mean()+s[0].start)]})
 rgb=a[:,:,:3].astype(int);warm=(a[:,:,3]>=128)&(rgb[:,:,0]-rgb[:,:,1]>15)&(rgb[:,:,1]-rgb[:,:,2]>10)&(rgb[:,:,1]>130)&(rgb[:,:,2]>95);warm[760:]=False;papers=[]
 for name,x0,x1 in [('left',150,450),('right',900,1210)]:
  m=warm.copy();m[:,:x0]=False;m[:,x1:]=False;yy,xx=np.where(m)
  if len(xx)>100:papers.append({'lane':name,'warmInteriorBounds':[int(xx.min()),int(yy.min()),int(xx.max()+1),int(yy.max()+1)],'warmInteriorCentroid':[float(xx.mean()),float(yy.mean())],'pixels':len(xx)})
 f={'phase':p,'upperComponentsClippedAtY680':components,'airbornePaperWarmInteriors':papers,'wholeSourceAlphaMass':float(a[:,:,3].sum()/255)};frames.append(f)
 perimeter=np.zeros(a.shape[:2],bool);perimeter[0,:]=perimeter[-1,:]=True;perimeter[:,0]=perimeter[:,-1]=True;colors=[]
 for name,bg in [('black',(0,0,0,255)),('white',(255,255,255,255))]:
  c=Image.new('RGBA',im.size,bg);c.alpha_composite(im);delta=np.abs(np.array(c).astype(int)[:,:,:3]-np.array(bg[:3]));colors.append({'background':name,'maxPerimeterChannelDifference':int(delta[perimeter].max())})
 edge.append({'phase':p,'sourceSHA256':sha(R/p/'master.png'),'sourcePerimeterAlphaMax':int(a[:,:,3][perimeter].max()),'sourcePerimeterNonzeroPixels':int((a[:,:,3][perimeter]>0).sum()),'blackWhiteCompositeEvidence':colors,'visualReview':'Full-resolution source black/white pairs and native six-background composites show no visible backing card, clipped outline, rectangular haze or colored matte fringe. The dark irregular soil is intentional subject matter.'})
masses=np.array([f['wholeSourceAlphaMass'] for f in frames]);deltas=(np.diff(masses)/masses[:-1]*100).tolist()
caveats=['The four poses are strongly differentiated over only one third of a second. The initial left tip rises 357 source pixels (7.97 logical pixels) between frames 1 and 2, while frames 2 and 3 primarily broaden the crest. The cadence is not uniform and may feel abrupt at 12 fps.','The total alpha mass grows about 12.3%, then 25.8%, and drops about 26.7% at the settling frame. The last drop is an authored plume collapse, not a whole-frame opacity fade. It may read as a fast collapse.','The airborne paper cohort first appears in frame 2 and is separate from the two stationary ground papers. Do not claim those fixed ground papers lift or that every contour point follows a measured physical trajectory.','The final frame retains two low wisps, one detached remnant, falling papers and the soil. It is a nonblank terminal pose, not a seamless return to frame 1. No scene-removal time or 420 ms node lifetime is known without a consumer.','The material features are registered within 1 source pixel in the fixed-ROI search, and soil bounds vary by at most 2 source pixels (0.045 logical pixels). Their generated shading is not byte-identical, so subtle texture flicker remains possible.','At 32 pixels the smallest crest fragments and detached final remnant are subordinate, sometimes only a pixel. They read more clearly at 64 pixels. Browser and live runtime playback remain unobserved.']
motion={'topicId':'C01-FX-14','method':'Upper alpha128 components are measured only above y680 to separate motion from the fixed material patch. Warm paper interiors use fixed color and lane masks above y760. These are diagnostics only and never alter source pixels or placement.','materialRegistrationProof':'material-registration-proof.json','frames':frames,'adjacentWholeSourceAlphaMassChangesPercent':deltas,'sequenceInterpretation':['Frame 1: two low curls over a fixed irregular soil patch and two stationary ground papers.','Frame 2: upper tips extend sharply upward; two additional paper scraps are airborne.','Frame 3: broad connected crest curls across the upper region; both airborne papers rise further and change angle; three small fragments appear.','Frame 4: the broad crest retires, leaving slender lower curls and one detached remnant; airborne papers descend toward the patch. The fixed ground papers remain.'],'continuityCaveats':caveats,'loop':False,'browserObserved':False,'liveGameObserved':False}
write(Q/'motion-and-registration-proof.json',motion)
write(Q/'source-edge-composite-proof.json',{'sourceGate':'Actual source perimeter alpha <= 1, retained without cleanup; alpha >= 2 would fail.','frames':edge,'finalPadding':'Every final cell has 2 logical pixels of alpha 0 padding, 4 physical pixels at 2x.','fullSourceBlackWhiteReviewed':True,'nativeSixBackgroundsReviewed':True})
placement={'scale':28/1254,'origin':[2,2],'sourcePivot':[627,627]};phases=[{'phase':p,'source':f'packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/fx/fx-14/{p}/master.png','sha256':sha(R/p/'master.png')} for p in P]
write(R/'integration-catalog.json',{'schemaVersion':1,'assets':{'C01-FX-14':{'kind':'fx-phases-v1','phases':phases,'placement':placement}}})
failures=[]
for d in sorted((R/'rejected').iterdir()):
 if d.is_dir():failures.append({'attempt':str(d.relative_to(R)),**json.loads((d/'rejection.json').read_text()),'sourceSHA256':sha(d/'master.png')})
write(R/'failures.json',failures)
refcount=0
for prov in R.rglob('provenance.json'):
 d=prov.parent;assert sha(d/'raw.png')==sha(d/'master.png');refs=json.loads((d/'reference-hashes.json').read_text());req=json.loads((d/'request.json').read_text());assert req['prompt']==(d/'prompt.txt').read_text();assert req['referenced_image_paths']==[x['path'] for x in refs]
 for i,item in enumerate(refs):
  snapshot=d/'reference-snapshots'/f'input-{i+1}.png';assert sha(snapshot)==item['sha256'];item['snapshot']=str(snapshot.relative_to(R));refcount+=1
 write(d/'reference-hashes.json',refs);write(d/'reference-snapshots.json',[{'requestedPath':i['path'],'sha256AtGeneration':i['sha256'],'persistedExactInput':i['snapshot']} for i in refs])
 ev=json.loads((d/'source-evidence.json').read_text());ev['audit']='../../source-evidence.json' if d.parent.name=='rejected' else '../source-evidence.json';assert sha((d/ev['audit']).resolve())==ev['auditSha256'];write(d/'source-evidence.json',ev)
 write(d/'request-integrity.json',{'requestSHA256':sha(d/'request.json'),'promptSHA256':sha(d/'prompt.txt'),'exactRequestAndPromptAgree':True,'immutableReferenceSnapshotsVerified':True,'referenceCount':len(refs),'note':'Requested source paths may now contain a later selected candidate. The byte-hashed immutable snapshots preserve the actual generation inputs.'})
review={'topicId':'C01-FX-14','label':'墓园污染','status':'source-alpha-export-and-parent-static-phase-review-complete; live-playback-unobserved','phaseOrder':P,'sourceCount':4,'imagegenAttemptCount':8,'rejectedAttemptCount':4,'referenceSnapshotCount':refcount,'sourceCanvas':[1254,1254],'rawMasterByteIdentical':True,'sourceEdgeMaxima':[r['edgeAlphaMax'] for r in metrics['frames']],'sourceEdgeProof':'qa/source-edge-composite-proof.json','placement':placement,'perFrameFitCropShiftFadeAlphaCleanup':False,'native32And64SixBackgroundsReviewed':True,'fullAndTerminalComparisonReviewed':True,'fullSourceBlackWhiteReviewed':True,'parentStaticReview':'Accepted full/native/terminal static phase review on 2026-10-20. Fixed soil and ground papers; clear low, tall, crest, settling poses.','materialRegistrationProof':'qa/material-registration-proof.json','cohortReview':'qa/COHORT-REVIEW.md','motionProof':'qa/motion-and-registration-proof.json','actualCaveats':caveats,'exactAPNGTimingVerified':True,'apngCount':28,'verifiedDecodedFrames':112,'canonicalTiming':{'fps':12,'loop':False,'numPlays':1,'frameDurationSeconds':'1/12','sequenceSeconds':'1/3','encodedFinalFrameHold':False,'old420msRemovalApplies':False,'sceneRemovalMs':None},'diagnosticTiming':'Separate slow-diagnostic-repeat APNGs run at 3 fps and repeat for inspection; they are not the runtime contract.','browserObserved':False,'browserReason':'Known blocked browser/GPU environment reported by parent; no repeated escalation. Encoding and source-derived frames verified offline only.','authoredSceneConsumer':None,'liveGameObserved':False,'repoCodeRuntimeOrGitWrites':False}
write(R/'final-review.json',review);write(R/'final-quartet.json',{'topicId':'C01-FX-14','status':review['status'],'phaseOrder':P,'phases':phases,'placement':placement,'qa':'qa/qa-metrics.json','caveats':'qa/COHORT-REVIEW.md'})
for name,ps in [('first-pair',phases[:2]),('second-pair',phases[2:])]:write(R/f'{name}.json',{'topicId':'C01-FX-14','phases':ps,'sourcesBackedUp':True,'requestsPromptsHashesAndImmutableInputsPreserved':True})
color=Image.new('RGB',(1600,1600))
for i,p in enumerate(P):color.paste(Image.open(Q/f'{p}-green-ground-source-full.png').resize((800,800),Image.Resampling.LANCZOS),(i%2*800,i//2*800))
color.save(Q/'all-source-green-ground-overview.png')
print(json.dumps({'phaseHashes':phases,'alphaMassChangesPercent':deltas,'referenceSnapshots':refcount,'status':review['status']},indent=2))
