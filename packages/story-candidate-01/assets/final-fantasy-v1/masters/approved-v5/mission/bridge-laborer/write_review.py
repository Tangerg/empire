from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,json,hashlib
root=Path('/workspace/shared/empire-art-rebuild/approved-v5/mission/bridge-laborer')
poses=['idle','walk-a','carry-or-work','walk-b']; scale=44/956; canonical=[802.5,980]; origin=[8,4]; frame=[104,64]; common=[1586,993]
reg={'status':'Native quartet complete. Whole-canvas measured export proposal only; no runtime writes.','sharedScale':scale,'bodyReferenceSourcePixels':956,'bodyTargetHeightLogicalPixels':44,'bodyMeasurement':'Accepted idle hair crown24 to lowest boot sole980. Shared44/956scale unchanged for every pose.','canonicalSourceFoot':canonical,'commonSourceCanvas':common,'paddingPolicy':'Native dimensions differ by up to2px width and1px height; pad transparent right/bottom only during export to1586x993. No native raw/master edits.','canvasPaddingToleranceRequired':2,'proposedCommonFrameSize':frame,'proposedOrigin':origin,'registrationMethod':'Translate entire canvas at shared44/956scale. For each boot, alpha>127 bottom4-row band gives sole-center x; sourceFoot.x averages both boots and sourceFoot.y is lowest sole.','poses':{}}
frames=[]; detail=Image.new('RGB',(1586*2,993*2),'#dddddd'); detaild=ImageDraw.Draw(detail)
for idx,pose in enumerate(poses):
 im=Image.open(root/pose/'master.png'); a=np.array(im.getchannel('A')); boots=[]
 for left,right in [(430,800),(810,1180)]:
  ys,xs=np.where(a[850:,left:right]>127); y=int(ys.max()+850); sy,sx=np.where(a[y-3:y+1,left:right]>127); boots.append([float((sx.min()+sx.max())/2+left),y])
 foot=[sum(b[0] for b in boots)/2,max(b[1] for b in boots)]
 offset=[(canonical[i]-foot[i])*scale for i in range(2)]
 q=json.loads((root/pose/'alpha-qa.json').read_text()); b=q['visibleAlpha127Bounds']; logical=[origin[0]+offset[0]+b[0]*scale,origin[1]+offset[1]+b[1]*scale,origin[0]+offset[0]+b[2]*scale,origin[1]+offset[1]+b[3]*scale]
 bounds=[origin[0]+offset[0],origin[1]+offset[1],origin[0]+offset[0]+common[0]*scale,origin[1]+offset[1]+common[1]*scale]
 reg['poses'][pose]={'nativeSize':list(im.size),'padRight':common[0]-im.width,'padBottom':common[1]-im.height,'bootSoleBandCenters':boots,'sourceSupportAnchor':foot,'registrationOffsetLogical':offset,'nativeVisibleBoundsAlphaGreaterThan127':b,'nativeAllNonzeroAlphaBounds':q['allNonzeroAlphaBounds'],'proposedLogicalVisibleBounds':logical,'proposedLogicalFullCanvasBounds':bounds,'wholeCanvasFitsProposedFrame':bounds[0]>=0 and bounds[1]>=0 and bounds[2]<=frame[0] and bounds[3]<=frame[1],'perimeterGate':{'maxNativePerimeterAlpha':q['perimeterMaxAlpha'],'pixelsWithPerimeterAlphaGreaterThan1':q['perimeterPixelsAlphaAbove1'],'passesNativeSilhouettePerimeterGate':q['perimeterPixelsAlphaAbove1']==0}}
 padded=Image.new('RGBA',tuple(common));padded.alpha_composite(im)
 # A single whole-canvas resize, same shared scale. No content crop/fit or alpha modifications.
 small=padded.resize((round(common[0]*scale),round(common[1]*scale)),Image.Resampling.LANCZOS)
 tile=Image.new('RGBA',tuple(frame));tile.alpha_composite(small,(round(origin[0]+offset[0]),round(origin[1]+offset[1])));frames.append(tile)
 preview=Image.new('RGBA',tuple(common),(255,255,255,255));preview.alpha_composite(im);detail.paste(preview.convert('RGB'),((idx%2)*common[0],(idx//2)*common[1]));detaild.text(((idx%2)*common[0]+20,(idx//2)*common[1]+20),pose,fill='black')
reg['requiredMaximumTranslationLogicalPixels']=max(abs(c) for v in reg['poses'].values() for c in v['registrationOffsetLogical']);reg['recommendedRegistrationLimitLogicalPixels']=4
json.dump(reg,(root/'registration-evidence.json').open('w'),indent=2)
contact=Image.new('RGB',(416,168),'#888888');d=ImageDraw.Draw(contact)
for i,(pose,im) in enumerate(zip(poses,frames)):
 for row,c in enumerate([(255,255,255,255),(0,0,0,255)]):
  tile=Image.new('RGBA',tuple(frame),c);tile.alpha_composite(im);contact.paste(tile.convert('RGB'),(i*104,row*84+20))
 d.text((i*104+4,4),pose,fill='white');d.text((i*104+4,88),pose,fill='white')
contact.save(root/'qa-native44-black-white.png');contact.resize((1248,504),Image.Resampling.NEAREST).save(root/'qa-native44-black-white-3x.png');detail.save(root/'qa-native-quartet-white.png')
# Animated diagnostic is a review composite; not a runtime/master replacement.
gif=[]
for im in frames:
 bg=Image.new('RGBA',tuple(frame),(235,235,235,255));bg.alpha_composite(im);gif.append(bg.convert('RGB').resize((416,256),Image.Resampling.NEAREST))
gif[0].save(root/'qa-sequence.gif',save_all=True,append_images=gif[1:],duration=[500,250,600,250],loop=0)
manifest={'topicId':'C01-MISSION-BRIDGE-LABORER','status':'Complete native quartet, QA and measured registration proposal; coordinator handles export/binding.','frameOrder':poses,'generationTool':'built-in image_gen.imagegen','nativeArtPolicy':'All raw/master pairs byte-identical native tool outputs, write-protected, no crop/recolor/alpha cleanup/per-pose fitting.','sourceEvidence':'source-evidence.json','registrationEvidence':'registration-evidence.json','qa':['qa-native44-black-white.png','qa-native44-black-white-3x.png','qa-native-quartet-white.png','qa-sequence.gif'],'poses':{p:{'raw':f'{p}/raw.png','master':f'{p}/master.png','request':f'{p}/request.json','prompt':f'{p}/prompt.txt','alphaQA':f'{p}/alpha-qa.json','sha256':hashlib.sha256((root/p/'master.png').read_bytes()).hexdigest()} for p in poses}}
json.dump(manifest,(root/'bundle-manifest.json').open('w'),indent=2)
review={'status':'Native art pass; final runtime scene/export QA remains with coordinator.','identity':'Original source face, skin, hair/beard, vest/shirt/trousers/patch/belt/pouch/boots, squared timber, wooden mallet and hip rope retained.','style':'Approved chunky matte broad shapes with clear hands and boots, same44logical adult body class as accepted humans.','poses':{'idle':'Both boots planted, beam shoulder-supported, mallet lowered.','walk-a':'Patched far leg leads at screen-right; unpatched near leg trails left; mallet rearward.','carry-or-work':'Timber lowered across waist, right hand supports it, left hand makes controlled mallet seating tap; planted stance, no combat gesture.','walk-b':'Unpatched near leg leads at screen-right; patched far leg trails left; mallet shifted forward. Whole character is not mirrored.'},'alpha':'All genuine RGBA with large fully-transparent background. Three poses retain native alpha1 perimeter specks; no alpha>1 silhouette touches edge. No alpha changes.','cameraScale':'All nominal1585x992; native tool variance1584-1586wide,992-993high documented for export-only transparent padding tolerance2. Single44/956scale and measured whole-canvas foot translations.','noRuleChanges':True,'noRepositoryEdits':True}
json.dump(review,(root/'qa-review.json').open('w'),indent=2)
print(json.dumps({p:reg['poses'][p]['sourceSupportAnchor'] for p in poses}));print('maximum logical translation',reg['requiredMaximumTranslationLogicalPixels'])
