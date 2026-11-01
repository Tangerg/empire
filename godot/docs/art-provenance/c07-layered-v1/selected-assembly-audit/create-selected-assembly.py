from pathlib import Path
import json,copy,hashlib
root=Path('empire-c01/godot/data/scenes');out=Path('c07-static-preview')
granary=json.loads((root/'c01-07-granary-door-stock-surface-preview-ground.json').read_text());barracks=json.loads((root/'c01-07-canopy-surface-preview-ground.json').read_text());gc=json.loads((root/'c01-07-granary-door-stock-surface-preview-objects.json').read_text());bc=json.loads((root/'c01-07-canopy-surface-preview-objects.json').read_text())
for k in set(gc['objects'])&set(bc['objects']):assert gc['objects'][k]==bc['objects'][k],k
for a in barracks['objects']:
 matched=[b for b in granary['objects'] if b['id']==a['id']]
 if matched:assert matched==[a]
for k in set(granary)-{'objects','objectCatalogPath'}:assert granary[k]==barracks[k],k
cat=copy.deepcopy(gc)
for k,v in bc['objects'].items():
 if k not in cat['objects']:cat['objects'][k]=v
merged=copy.deepcopy(granary);merged['objects']=copy.deepcopy(barracks['objects'])+[o for o in granary['objects'] if o['id'] not in {x['id'] for x in barracks['objects']}];merged['objectCatalogPath']='res://data/scenes/c01-07-selected-assembly-objects.json'
sc=json.loads((root/'c01-07-granary-door-stock-surface-preview.json').read_text());sc['assembledGroundPath']='res://data/scenes/c01-07-selected-assembly-ground.json'
assert len(merged['objects'])==len({o['id'] for o in merged['objects']})==11;assert len(cat['objects'])==11
for name,obj in [('c01-07-selected-assembly-objects.json',cat),('c01-07-selected-assembly-ground.json',merged),('c01-07-selected-assembly.json',sc)]:
 p=root/name
 if p.exists():raise RuntimeError('One-time bootstrap refuses to overwrite advancing selected assembly: '+str(p))
 p.write_text(json.dumps(obj,indent=2))
report={'scope':'one-time exact merge of frozen approved slices; no new objects or official binding','objects':[o['id'] for o in merged['objects']],'overlapAssetsEqual':sorted(set(gc['objects'])&set(bc['objects'])),'officialSceneSha256':hashlib.sha256((root/'c01-07.json').read_bytes()).hexdigest()}
(out/'selected-assembly-bootstrap.json').write_text(json.dumps(report,indent=2));print(report)
