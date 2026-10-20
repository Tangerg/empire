"""Independent read-only source checks; writes only this shared QA directory."""
import sys, json, hashlib, runpy
from pathlib import Path
from PIL import Image
sys.dont_write_bytecode=True
R=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019')
S=Path('/workspace/shared/empire-art-rebuild/approved-v5/faction-kit')
A=S.parent/'faction-kit-audit'
O=Path(__file__).resolve().parent
E=runpy.run_path(str(R/'tools/art/export_overrides.py'),run_name='qa_read_only')
audit=json.loads((A/'faction-kit-audit.json').read_text())
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
records=E['records'](R)

def components(im, threshold):
 a=im.getchannel('A');w,h=im.size
 pts={(x,y) for y in range(h) for x in range(w) if a.getpixel((x,y))>=threshold};out=[]
 while pts:
  st=[pts.pop()];xs=[];ys=[]
  while st:
   x,y=st.pop();xs.append(x);ys.append(y)
   for p in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)]:
    if p in pts:pts.remove(p);st.append(p)
  if len(xs)>=20:out.append({'pixels':len(xs),'bbox':[min(xs),min(ys),max(xs)+1,max(ys)+1]})
 return sorted(out,key=lambda c:-c['pixels'])

report={'scope':'Independent QA of existing Wasteland and Militia sources only; no image generation, source mutation, repository or Git writes.', 'reviewDateUtc':'2026-10-20','exporterPath':str(R/'tools/art/export_overrides.py'),'exporterSha256':sha(R/'tools/art/export_overrides.py'),'assets':[]}
for slug,slot in [('faction-wasteland',7),('faction-militia-mercenary',8)]:
 d=S/slug;ev=json.loads((d/'source-evidence.json').read_text());im=Image.open(d/'master.png');au=audit['assets'][slot-1]
 out=E['fit'](im,(128,128),2);native=Image.open(d/'native-128.png')
 pack,canonical,hd=records[ev['topicId']]
 checks={'masterHashRecorded':sha(d/'master.png')==ev['masterSha256'],'rawEqualsMaster':sha(d/'raw.png')==sha(d/'master.png'),'rawOriginExists':Path(ev['rawGeneratedSource']).exists(),'rawOriginEqualsMaster':sha(Path(ev['rawGeneratedSource']))==sha(d/'master.png'),'auditHashRecorded':sha(A/'faction-kit-audit.json')==ev['sourceAuditSha256'],'sourceFullCellHashRecorded':sha(A/ev['source']['referenceFullCell'])==ev['source']['referenceFullCellSha256'],'sourceBoardHashRecorded':sha(R/ev['source']['board'])==ev['source']['boardSha256'],'fullCellEqualsOriginalBoardCrop':Image.open(A/ev['source']['referenceFullCell']).tobytes()==Image.open(R/ev['source']['board']).crop(ev['source']['fullCellBox']).tobytes(),'auditTopicMatchesEvidence':au['topicId']==ev['topicId'],'canonicalAssetIdMatches':canonical['assetId']==ev['assetId'],'canonicalContract128Centered':canonical['width']==128 and canonical['height']==128 and canonical['anchor']==[64,64],'noHD':hd is None,'noFramesOrVariants':not canonical.get('frames') and not canonical.get('variants'),'fullCanvasExporterPixelEquality':out.tobytes()==native.tobytes()}
 for req in ev['toolRequests']:
  q=json.loads((d/req['file']).read_text());pf='refinement-prompt.txt' if req['file'].startswith('refinement') else 'prompt.txt'
  checks['hash:'+req['file']]=sha(d/req['file'])==req['sha256'];checks['prompt:'+pf]=(d/pf).read_text().rstrip('\n')==q['prompt'];checks['hash:'+pf]=sha(d/pf)==ev['promptHashes'][pf]
  for ref in req['referenceHashes']:checks['reference:'+ref['requestPath']]=sha(Path(ref['evidencePath']))==ref['sha256']
 for bg,color in [('white',(255,255,255)),('dark',(24,27,31)),('olive',(93,104,54))]:
  comp=Image.new('RGBA',(128,128),color+(255,));comp.alpha_composite(out)
  checks['previewPixels:'+bg]=comp.convert('RGB').tobytes()==Image.open(d/f'preview-128-{bg}.png').tobytes()
 alpha=im.getchannel('A');hist=alpha.histogram();bounds=alpha.point(lambda v:255 if v>=8 else 0).getbbox()
 visual={
  'viewed': ['full original source cell', 'complete generated master', 'native 128px white/dark/olive strip', 'approved Lorne/rune-artificer masters and concept-v2'],
  'style':'Bold charcoal outlines, simplified broad matte facets, complete catalog silhouettes. More saturated and faceted than legacy source, consistent with inspected approved-v5 references. Minor source texture was intentionally simplified; no photoreal or ornate reinterpretation.',
  'native128':'All three object identities remain readable on each inspected background. No visible opaque backdrop, magenta key, external glow or cropped silhouette.',
  'lowAlphaGapCaveat':'Lanczos low-alpha fringes connect some close silhouettes at threshold 8; this is not proof of exactly zero alpha in every inter-object gap. Three separate substantial components exist at alpha >=64 for both kits, and at >=28 for Wasteland. No source alpha was changed.',
  'sourceMarginCaveat':'The authored sources do not achieve the prompt target of 6%/inner-88% margins. Actual complete silhouettes and zero-alpha runtime perimeter pass the existing exporter contract. Parent accepted this documented margin deviation.'
 }
 if slug=='faction-wasteland':
  visual['identity']='Round repaired shield with rust/ochre/angular blue-gray panel arrangement and iron boss; folded rust cloth with branching repair stitches, tan lower-right patch and blue-gray/ochre hem; slender plain spear with wrapped shaft and complete butt. Three original source objects, no invented tribal emblem.'
  visual['semanticCaveat']='Patch count/brush facets and exact spear angle are stylized, not a pixel-identical redraw. Defining object and patchwork topology preserved.'
 else:
  visual['identity']='Round/oval plank shield with slate/teal left, small green upper-left patch and ochre/brown right; folded teal cloth with brown belt, brass circular clasp and downturned belt end; plain short square-headed steel hammer with wood handle. Full shield/head outlines restored relative to legacy inset.'
  visual['semanticCaveat']='The source clasp contains a small curved incised groove. The accepted generated master has a plain gold boss and does not preserve that groove. Parent explicitly accepted this as a nonsemantic stylization; do not claim exact motif fidelity.'
 report['assets'].append({'slug':slug,'topicId':ev['topicId'],'slot':slot,'fullCellBox':ev['source']['fullCellBox'],'masterPath':str(d/'master.png'),'masterSha256':sha(d/'master.png'),'rawGeneratedSource':ev['rawGeneratedSource'],'sourceEvidencePath':str(d/'source-evidence.json'),'checks':checks,'allChecksPass':all(checks.values()),'sourceImage':{'size':list(im.size),'mode':im.mode,'alphaExtrema':list(alpha.getextrema()),'zeroAlphaFraction':hist[0]/(im.width*im.height),'nearOpaqueFractionGE250':sum(hist[250:])/(im.width*im.height),'visibleBoundsGE8':list(bounds),'visibleMarginsL_T_R_B':[bounds[0],bounds[1],im.width-bounds[2],im.height-bounds[3]],'exporterAlphaGate':E['validate_alpha'](im,slug)},'native128':{'path':str(d/'native-128.png'),'sha256':sha(d/'native-128.png'),'method':'Current exporter fit(), complete authored canvas, 2px padding, no bbox crop or alpha cleanup','exporterAlphaGate':E['validate_alpha'](out,slug),'componentsGE8':components(out,8),'componentsGE64':components(out,64),'componentsGE240':components(out,240)},'visualReview':visual,'acceptance':'ready-for-formal-binding-with-documented-caveats','integrationStatus':'Formal binding/export catalog mutation was not performed by this reviewer. No runtime-completion claim.','provenanceLimit':'Preserved raw/master/origin hashes and request/prompt/reference hashes verified. This review did not independently replay or recover the original image-generation tool response.'})
(O/'independent-qa.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'report':str(O/'independent-qa.json'),'allChecksPass':all(a['allChecksPass'] for a in report['assets']),'acceptance':[{'slug':a['slug'],'status':a['acceptance']} for a in report['assets']]},indent=2))
