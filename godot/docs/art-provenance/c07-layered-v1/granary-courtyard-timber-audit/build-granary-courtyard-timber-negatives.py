from pathlib import Path
import json,copy
r=Path('/workspace/scratch/84c9fe948755');g=r/'empire-c01/godot/data/scenes';out=r/'c07-static-preview'
scene=json.load(open(g/'c01-07-selected-assembly.json'));layout=json.load(open(g/'c01-07-selected-assembly-ground.json'));catalog=json.load(open(g/'c01-07-selected-assembly-objects.json'));key='granary-courtyard-timber-left';instance=next(i for i in layout['objects'] if i['id']==key);origin=[v*16 for v in instance['contact']];source=json.load(open(g/'c01-07-granary-courtyard-timber-left-surface.json'))
for mode in ['late265','filled-hole']:
 s=copy.deepcopy(scene);l=copy.deepcopy(layout);c=copy.deepcopy(catalog);d=copy.deepcopy(source);prefix='granary-courtyard-timber-negative-'+mode
 if mode=='late265':
  for support in d['supports']:support['frontPixels'][1]=265-origin[1]
 else:
  x,y=296,196;support='wrong-filled-left-hole';d['supports'].append({'id':support,'frontPixels':[x-origin[0],212-origin[1]],'kind':'structural'});d['members'].append({'id':'wrong-filled-true-left-hole','supportId':support,'polygonPixels':[[x-1-origin[0],y-1-origin[1]],[x+1-origin[0],y-1-origin[1]],[x+1-origin[0],y+1-origin[1]],[x-1-origin[0],y+1-origin[1]]],'uvPixels':[[110,200],[115,200],[115,220],[110,220]],'coverage':'opaque','tint':'b7bdc5'})
 c['objects'][key]['surfaceGeometryPath']='res://'+prefix+'-surface.json';s['assembledGroundPath']='res://'+prefix+'-ground.json';l['objectCatalogPath']='res://'+prefix+'-objects.json'
 for suffix,value in [('',s),('-ground',l),('-objects',c),('-surface',d)]: (out/(prefix+suffix+'.json')).write_text(json.dumps(value,indent=2)+'\n')
