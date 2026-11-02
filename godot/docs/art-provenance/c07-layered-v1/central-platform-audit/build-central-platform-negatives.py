from pathlib import Path
import json,copy
p=Path('/workspace/scratch/84c9fe948755/c07-static-preview');scene=json.load(open(p/'central-platform-candidate.json'));ground=json.load(open(p/'central-platform-candidate-ground.json'));catalog=json.load(open(p/'central-platform-candidate-objects.json'));surface=json.load(open(p/'central-platform-surface.json'))
for mode in ['old-common-front','late-riser','invade-paving','reverse-equal-depth-order']:
 name='central-platform-negative-'+mode;s=copy.deepcopy(scene);g=copy.deepcopy(ground);c=copy.deepcopy(catalog);d=copy.deepcopy(surface)
 if mode=='old-common-front':d=json.load(open(p/'central-platform-failed-v1/central-platform-surface.json'))
 if mode=='late-riser':
  for support in d['supports']:
   if support['id'].startswith('riser-'):support['frontPixels'][1]=550-448
 if mode=='invade-paving':
  d['supports'].append({'id':'illegal-road-fill','frontPixels':[824-714,520-448],'kind':'physical'})
  d['members'].append({'id':'illegal-road-fill','supportId':'illegal-road-fill','polygonPixels':[[106,70],[114,70],[114,74],[106,74]],'uvPixels':[[410,650],[490,650],[490,850],[410,850]],'tint':'ffffff','coverage':'opaque'})
 if mode=='reverse-equal-depth-order':
  item=next(v for v in g['objects'] if v['id']=='central-platform');g['objects'].remove(item);g['objects'].append(item)
 c['objects']['central-platform']['surfaceGeometryPath']='res://'+name+'-surface.json';g['objectCatalogPath']='res://'+name+'-objects.json';s['assembledGroundPath']='res://'+name+'-ground.json'
 for suffix,v in [('',s),('-ground',g),('-objects',c),('-surface',d)]: (p/(name+suffix+'.json')).write_text(json.dumps(v,indent=2)+'\n')
