from pathlib import Path
import json,copy
p=Path(__file__).parent
load=lambda n:json.loads((p/n).read_text())
for case in ['filled-hole','early-wood','stone-shift','late-soil']:
 name='right-garden-planted-negative-'+case
 s=load('right-garden-planted-study.json');g=load('right-garden-planted-study-ground.json');c=load('right-garden-planted-study-objects.json')
 if case=='stone-shift':
  next(x for x in g['objects'] if x['id']=='west-inner-right-garden-stone')['contact'][0]-=5
 else:
  asset='right-garden-soil' if case=='late-soil' else 'right-garden-timber'
  surf=load('right-garden-soil-surface.json' if case=='late-soil' else 'right-garden-timber-merged-surface.json')
  if case=='filled-hole':
   old=load('right-garden-negative-filled-hole-surface.json');surf['members'].append(old['members'][-1]);surf['supports'].append(old['supports'][-1])
  else:
   for x in surf['supports']:x['frontPixels'][1]=100 if case=='late-soil' else 0
  (p/(name+'-surface.json')).write_text(json.dumps(surf,indent=2)+'\n');c['objects'][asset]['surfaceGeometryPath']='res://'+name+'-surface.json'
 s['assembledGroundPath']='res://'+name+'-ground.json';g['objectCatalogPath']='res://'+name+'-objects.json'
 for suffix,v in [('',s),('-ground',g),('-objects',c)]:(p/(name+suffix+'.json')).write_text(json.dumps(v,indent=2)+'\n')
