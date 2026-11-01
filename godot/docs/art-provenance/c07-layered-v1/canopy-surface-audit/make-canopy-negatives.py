import json,copy
base='empire-c01/godot/data/scenes/';out='c07-static-preview/'
s=json.load(open(base+'c01-07-canopy-surface.json'));cat=json.load(open(base+'c01-07-canopy-surface-preview-objects.json'));g=json.load(open(base+'c01-07-canopy-surface-preview-ground.json'));sc=json.load(open(base+'c01-07-canopy-surface-preview.json'))
for kind in ['early-depth','late-depth','filled-hole','original-uv']:
 t=copy.deepcopy(s)
 if kind.endswith('depth'):
  for support in t['supports']:support['frontPixels'][1]+= -40 if kind=='early-depth' else 40
 if kind=='filled-hole':t['members'].append({'id':'negative-filled-real-hole','supportId':'column-969','polygonPixels':[[24,39],[26,39],[26,41],[24,41]],'uvPixels':[[550,900]]*4,'tint':'ffffff','coverage':'opaque'})
 open(out+'formal-canopy-'+kind+'-surface.json','w').write(json.dumps(t,indent=2))
 c=copy.deepcopy(cat)
 for asset in c['objects'].values():
  if asset.get('surfaceGeometryPath')=='res://data/scenes/c01-07-canopy-surface.json':asset['surfaceGeometryPath']='res://formal-canopy-'+kind+'-surface.json'
 open(out+'formal-canopy-'+kind+'-objects.json','w').write(json.dumps(c,indent=2))
 gg=copy.deepcopy(g);gg['objectCatalogPath']='res://formal-canopy-'+kind+'-objects.json';open(out+'formal-canopy-'+kind+'-ground.json','w').write(json.dumps(gg,indent=2))
 ss=copy.deepcopy(sc);ss['assembledGroundPath']='res://formal-canopy-'+kind+'-ground.json';open(out+'formal-canopy-'+kind+'-scene.json','w').write(json.dumps(ss,indent=2))
