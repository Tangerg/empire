from pathlib import Path
from copy import deepcopy
import json,hashlib,re,collections,xml.etree.ElementTree as E,sys,subprocess,os,concurrent.futures
import numpy as np
from PIL import Image
ROOT=Path(os.environ['EMPIRE_REPO']).resolve()
OUT=Path(os.environ['AUDIT_OUTPUT']).resolve()
sys.path.insert(0,str(ROOT/'tools/art'))
from render_ground_review import normalize_actor_facing
from svg_viewports import NS,flatten_viewports
ENV=ROOT/'packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1'
manifest=json.loads((ENV/'manifest-environment-builder-v1.json').read_text())
atlases={Path(a['png2x']).name:a for a in manifest['atlases']}
bindings=json.loads((ROOT/'tools/art/environment-overrides.json').read_text())['atlases']
planned={4,8,12,15}; instances=[]; levels=[]; tasks=[]
for level in ['c01-01','c01-02']:
 d=OUT/level;raw=E.parse(d/'runtime-standing-full.svg').getroot();p=json.loads((d/'runtime-scene-provenance.json').read_text());rend=next(r for r in p['renders'] if r['filename']=='runtime-standing-full.svg')
 world=raw.find(f'{{{NS}}}g[@class="board-world"]');wi=list(raw).index(world)
 for im in raw.iter(f'{{{NS}}}image'):
  h=im.get('href','')
  if h and not h.startswith('data:'): im.set('href',str((d/h).resolve()))
 drawings=[]
 for layer in world:
  cls=layer.get('class','')
  if 'layer' not in cls.split():continue
  for ix,pic in enumerate(layer):
   meta={'level':level,'layer':cls.replace('layer layer-',''),'layerIndex':ix,'outsideWorld':False}
   if meta['layer']=='units':meta.update(rend['standingDrawOrder'][ix])
   for im in pic.iter(f'{{{NS}}}image'):
    at=atlases.get(Path(im.get('href','')).name)
    if at and at.get('cells'):
     n=round(-float(im.get('x','0'))/at['cellWidth'])+at['columns']*round(-float(im.get('y','0'))/at['cellHeight']);cell=at['cells'][n]
     prop=next((g for g in pic.iter(f'{{{NS}}}g') if 'candidate-environment-prop' in g.get('class','')),None)
     sc=abs(float(re.search(r'scale\(([-.0-9]+)',prop.get('transform','')).group(1))) if prop is not None else None
     meta.update({'atlas':at['id'],'cellId':cell['id'],'cellIndex':n,'scale':sc,'registered':cell['id'] in bindings.get(at['id'],{}).get('cells',{}),'semanticMetadata':cell,'sourceFile':str(Path(im.get('href')).relative_to(ROOT))})
     instances.append(meta)
     break
   drawings.append((pic,dict(layer.attrib),meta))
 for i,pic in enumerate(list(raw)[wi+1:]):drawings.append((pic,{}, {'id':f'frame:{i}','outsideWorld':True}))
 width=int(float(raw.get('width')));height=int(float(raw.get('height')))
 defsstyles=[deepcopy(x) for x in raw if x.tag in [f'{{{NS}}}defs',f'{{{NS}}}style']]
 def write_mask(selected,path):
  svg=E.Element(f'{{{NS}}}svg',{k:raw.get(k) for k in ['width','height','viewBox']})
  for x in defsstyles:svg.append(deepcopy(x))
  w=E.SubElement(svg,f'{{{NS}}}g',{'transform':world.get('transform')})
  for pic,attrs,m in selected:
   parent=svg if m['outsideWorld'] else w
   if attrs:parent=E.SubElement(parent,f'{{{NS}}}g',attrs)
   parent.append(deepcopy(pic))
  normalize_actor_facing(svg);df=E.SubElement(svg,f'{{{NS}}}defs');flatten_viewports(svg,df)
  E.ElementTree(svg).write(path,encoding='unicode')
 for i,(pic,attrs,m) in enumerate(drawings):
  target=m.get('atlas')=='camps-foundations' or m.get('atlas')=='cliff-modules-temperate' and m['cellIndex'] not in planned
  if not target:continue
  ident=f'{level}-{i}';work=OUT/'masks';work.mkdir(exist_ok=True)
  a=work/f'{ident}-source.svg';b=work/f'{ident}-later.svg';write_mask([drawings[i]],a);write_mask(drawings[i+1:],b)
  tasks.append((m,a,b,width));m['globalDisplayIndex']=i
 levels.append({'levelId':level,'viewport':rend['viewport'],'unitCount':rend['unitCount'],'buildingCount':rend['buildingCount'],'stateSha256':rend['stateSha256'],'sourceLayerCounts':rend['sourceLayerCounts'],'sources':p['sources'],'svgSha256':hashlib.sha256((d/'runtime-standing-full.svg').read_bytes()).hexdigest()})
print('Prepared',len(tasks),'target mask pairs',flush=True)
def render(task):
 m,a,b,w=task
 for src in [a,b]:
  png=src.with_suffix('.png')
  if not png.exists():subprocess.run(['inkscape',str(src),'--export-type=png',f'--export-filename={png}',f'--export-width={w}'],env={**os.environ,'INKSCAPE_PROFILE_DIR':str(OUT/'masks'/('profile-'+str(os.getpid())))},check=True,capture_output=True)
 source=np.asarray(Image.open(a.with_suffix('.png')).getchannel('A'),dtype=float)/255
 later=np.asarray(Image.open(b.with_suffix('.png')).getchannel('A'),dtype=float)/255
 m.update(sourceAlphaArea=float(source.sum()),visibleAlphaArea=float((source*(1-later)).sum()),sourceScreenBounds=Image.fromarray((source*255).astype('uint8')).getbbox())
 return m
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
 for k,r in enumerate(pool.map(render,tasks)):
  if (k+1)%5==0:print('Measured',k+1,'/',len(tasks),flush=True)
summary=[]
for aid,cid in sorted(set((m['atlas'],m['cellId']) for m in instances)):
 values=[m for m in instances if m['atlas']==aid and m['cellId']==cid]
 scales=[m['scale'] for m in values if m['scale'] is not None]
 summary.append({'atlas':aid,'cellId':cid,'cellIndex':values[0]['cellIndex'],'countsByLevel':dict(collections.Counter(m['level'] for m in values)),'totalCount':len(values),'registered':values[0]['registered'],'scaleRange':[min(scales),max(scales)] if scales else None,'sourceAlphaArea':sum(m.get('sourceAlphaArea',0) for m in values) if any('sourceAlphaArea' in m for m in values) else None,'visibleAlphaArea':sum(m.get('visibleAlphaArea',0) for m in values) if any('visibleAlphaArea' in m for m in values) else None})
report={'kind':'Read-only active source inventory from complete actual BoardView SVG snapshots','snapshotHead':'654b382','plannedCliffIndicesExcludedFromNextBatch':[4,8,12,15],'method':'Display-1x alpha-weighted area: target alpha multiplied by transmission of all later actual drawings, including later layers and frame. Diagnostic masks exclude root grading; complete scene art stays intact. No silhouette omitted from complete scene.','limitations':['Initial poses only, offline Inkscape, not browser/GPU','First cliff quartet not bound at capture time; next-batch priority excludes it rather than pretending the new quartet was present','Subsequent replacement silhouette changes can alter visibility rankings','Masks treat all source alpha, including antialiasing and shadows, as area'], 'levels':levels,'summary':summary,'instances':instances}
(OUT/'active-source-inventory.json').write_text(json.dumps(report,indent=2)+'\n')
for aid in ['cliff-modules-temperate','camps-foundations','rural-life']:
 print('\n'+aid)
 for m in sorted((m for m in summary if m['atlas']==aid),key=lambda m:-(m['visibleAlphaArea'] or 0)):print(m)
