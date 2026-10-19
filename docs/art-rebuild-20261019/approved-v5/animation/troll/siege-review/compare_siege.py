from pathlib import Path
from PIL import Image, ImageChops, ImageDraw
import json, hashlib
p=Path(__file__).resolve().parent
read=lambda phase,name:json.loads((p/phase/name).read_text())
b,a=[read(phase,'render-contract.json') for phase in ['before','after']]
bp,ap=[read(phase,'runtime-scene-provenance.json') for phase in ['before','after']]
bc,ac=[read(phase,'code-sha256.json') for phase in ['before','after']]
bm,am=[read(phase,'clearance/occlusion.json') for phase in ['before','after']]
trolls={x['id'] for x in b['actors'] if x['type']=='ogre'}
checks={}
for key in ['levelId','levelSha256','stateBeforeSha256','stateAfterSha256','state','level','actors','layerSha256','standingDrawOrder','sourceLayerCounts','viewport','boardWorldTransform','mapSha256','buildingsMarkupSha256']:
    checks[key+'Unchanged']=b[key]==a[key]
checks['beforeRenderPreservesState']=b['stateBeforeSha256']==b['stateAfterSha256']
checks['afterRenderPreservesState']=a['stateBeforeSha256']==a['stateAfterSha256']
is_test=lambda path: path.endswith('.test.ts') or '/__tests__/' in path or '/__bench__/' in path
checks['allRuntimeCodeInputsUnchanged']={k:v for k,v in bc.items() if not is_test(k)}=={k:v for k,v in ac.items() if not is_test(k)}
checks['allNonTrollActorMarkupUnchanged']=all(h==a['actorMarkupSha256'][actor] for actor,h in b['actorMarkupSha256'].items() if actor not in trolls)
checks['trollBindingUsesApprovedV5']=all(x['usesApprovedV5'] for x in a['actorBindings'] if x['id'] in trolls)
checks['allActorsPreserved']=len(a['actors'])==len(b['actors'])==19
checks['noOmissions']=a['omissions']==b['omissions']==[]
changed_sources=[x for x in sorted(bp['sources'].keys() & ap['sources'].keys()) if bp['sources'][x]!=ap['sources'][x]]
old={x['id']:x for x in bm['actors']};new={x['id']:x for x in am['actors']}
reports=[]
for actor in a['actorBindings']:
    if actor['id'] not in trolls:continue
    i=actor['id'];geometry={}
    for phase,mask in [('before',old[i]),('after',new[i])]:
        atlas=Image.open(p/phase/'clearance/pieces@1x.png').convert('RGBA')
        n=mask['displayIndex'];tile=atlas.crop((n%16*256,n//16*256,n%16*256+256,n//16*256+256))
        bbox=tile.getchannel('A').getbbox();x,y=mask['cropOrigin'];wx,wy=74,62
        geometry[phase]={'silhouetteWorldBounds':[bbox[0]+x,bbox[1]+y,bbox[2]+x,bbox[3]+y],
                         'silhouetteScreenBounds':[bbox[0]+x+wx,bbox[1]+y+wy,bbox[2]+x+wx,bbox[3]+y+wy]}
    reports.append({'id':i,'owner':actor['owner'],'cell':actor['cell'],'facing':actor['facing'],
                    'worldFoot':actor['worldFoot'],'screenFoot':[actor['worldFoot'][0]+74,actor['worldFoot'][1]+62],
                    'runtimeImage':actor['runtimeImage'],'geometry':geometry,
                    'beforeOccludedFraction':old[i]['occludedFraction'],'afterOccludedFraction':new[i]['occludedFraction'],
                    'afterCovering':new[i]['covering']})
other_deltas=[{'id':i,'type':new[i]['type'],'cell':new[i]['cell'],'before':old[i]['occludedFraction'],'after':new[i]['occludedFraction'],'delta':round(new[i]['occludedFraction']-old[i]['occludedFraction'],6)} for i in old if i not in trolls and old[i]['occludedFraction']!=new[i]['occludedFraction']]
with Image.open(p/'before/runtime-standing-full@1x.png') as bi,Image.open(p/'after/runtime-standing-full@1x.png') as ai:
    diff=ImageChops.difference(bi.convert('RGB'),ai.convert('RGB'))
    pixel_changes=diff.getbbox();diff.save(p/'pixel-difference@1x.png')
    outside=diff.copy();outside_draw=ImageDraw.Draw(outside)
    for report in reports:
        for phase in ['before','after']:
            left,top,right,bottom=report['geometry'][phase]['silhouetteScreenBounds']
            outside_draw.rectangle((left,top,right-1,bottom-1),fill=(0,0,0))
    checks['scenePixelsOutsideTrollSilhouettesUnchanged']=outside.getbbox() is None
    sheet=Image.new('RGB',(724*2,554),(18,25,25));d=ImageDraw.Draw(sheet)
    d.text((8,8),'BEFORE - complete actual siege | offline 1x',(255,255,255));d.text((732,8),'AFTER - complete actual siege | offline 1x',(255,255,255))
    sheet.paste(bi,(0,30));sheet.paste(ai,(724,30));sheet.save(p/'complete-before-after@1x.png')
    crops=Image.new('RGB',(640,526),(18,25,25));d=ImageDraw.Draw(crops)
    for row,report in enumerate(reports):
        x,y=report['screenFoot'];box=(x-80,y-100,x+80,y+20)
        for col,(phase,im) in enumerate([('before',bi),('after',ai)]):
            d.text((col*320+8,row*263+7),f"{phase.upper()} actor {report['id']} cell {report['cell']} | offline 2x",(255,255,255))
            crop=im.crop(box).resize((320,240),Image.Resampling.NEAREST)
            crops.paste(crop,(col*320,row*263+23))
    crops.save(p/'troll-context-before-after@2x.png')
result={'kind':'offline complete actual siege consumer comparison; not browser/GPU or live animation',
        'checks':checks,'passed':all(checks.values()),'changedCodePaths':[x for x in sorted(bc.keys()|ac.keys()) if bc.get(x)!=ac.get(x)],
        'allCapturedCodeInputsIdentical':bc==ac,'capturedCodeInputCount':len(bc),'changedInputsAreTestsOnly':all(is_test(k) for k in bc.keys()|ac.keys() if bc.get(k)!=ac.get(k)),
        'changedExistingSourcePaths':changed_sources,'removedSources':sorted(bp['sources'].keys()-ap['sources'].keys()),'addedSources':sorted(ap['sources'].keys()-bp['sources'].keys()),
        'actors':reports,'nonTrollOcclusionChanges':other_deltas,'pixelDifferenceScreenBounds':pixel_changes,
        'limitations':['Initial pose only','Offline Inkscape; facing and nested viewport geometry normalized by existing helper','Actor alpha overlap excludes badge/ring/contact shadow; CSS shadow filters omitted','No selective hiding, fading, substitutions, actor movement or rules changes']}
(p/'comparison.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps(result,indent=2))
if not result['passed']:raise SystemExit('Invariant comparison failed')
