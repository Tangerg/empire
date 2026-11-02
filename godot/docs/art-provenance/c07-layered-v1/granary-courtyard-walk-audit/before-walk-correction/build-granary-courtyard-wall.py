"""One selected wall run, source-visible feet manually located (+/-2px).
Old painted mask contains left timber fence and road; it is NOT source geometry.
Right low extension is omitted because original solid footprint crosses legal(24,15).
Existing gate stone atlas; current runtime facts are exported surfaces only.
"""
from pathlib import Path
import json
root=Path('/workspace/scratch/84c9fe948755');out=root/'c07-static-preview';scenes=root/'empire-c01/godot/data/scenes'
texture='res://assets/native/c07-layered-v1/southeast-gate-v2-stone-candidate.png'
uv=[[435,480],[470,466],[490,477],[450,492]]
def quad(p,u0,v0,u1,v1):
 def q(u,v):return [(1-v)*((1-u)*p[0][i]+u*p[1][i])+v*((1-u)*p[3][i]+u*p[2][i]) for i in (0,1)]
 return [q(u0,v0),q(u1,v0),q(u1,v1),q(u0,v1)]
def face(m,name,p,rows,columns,tint):
 if name=='cap':
  m.append({'id':'continuous-cap','polygon':p,'uv':uv,'tint':tint});return
 # Non-overlapping masonry/mortar rectangles; no black alpha gaps.
 for row in range(rows):
  v0=row/rows;v1=(row+1)/rows;offset=.5 if row%2 else 0
  cuts=sorted(set([0.,1.]+[(i+offset)/columns for i in range(columns+1) if 0<(i+offset)/columns<1]))
  for col,(u0,u1) in enumerate(zip(cuts,cuts[1:])):
   du=min(.006,(u1-u0)/5);dv=.006
   for part,rect,color in [('stone',(u0+du,v0+dv,u1-du,v1-dv),tint),('left',(u0,v0,u0+du,v1),'a6afb8'),('right',(u1-du,v0,u1,v1),'a6afb8'),('upper',(u0+du,v0,u1-du,v0+dv),'b4bcc5'),('lower',(u0+du,v1-dv,u1-du,v1),'a6afb8')]:
    m.append({'id':f'{name}-{row}-{col}-{part}','polygon':quad(p,*rect),'uv':([[444,1000],[491,977],[491,1030],[444,1052]] if part=='stone' else uv),'tint':color})
components=[]
for name,support,faces in [
 ('main',[[325,230.511],[326,231],[371,253],[378,249],[379,248.429]],[('front',[[326,205],[371,227],[371,253],[326,231]],3,4,'b5c8e0'),('end',[[371,227],[378,223],[378,249],[371,253]],3,1,'d4dfed'),('cap',[[326,205],[333,201],[378,223],[371,227]],1,4,'d6e4ff')]),
 ('terminal-pier',[[368,250.25],[369,251],[377,257],[387,252],[388,251.5]],[('front',[[369,226],[377,232],[377,257],[369,251]],3,1,'b5c8e0'),('side',[[377,232],[387,227],[387,252],[377,257]],3,1,'d4dfed'),('cap',[[369,226],[379,223],[387,227],[377,232]],1,1,'d6e4ff')])]:
 members=[]
 for faceName,p,rows,cols,tint in faces:face(members,faceName,p,rows,cols,tint)
 d={'id':name,'textureResourcePath':texture,'supportLine':support,'members':members,'scope':'Authored masonry on manually identified real main/pier foot, approximately +/-2 source pixels. Left timber fence not included; right low stone extension intentionally omitted to preserve canonical legal ground.'};components.append(d);(out/f'granary-courtyard-wall-{name}-source.json').write_text(json.dumps(d,indent=2)+'\n')
(out/'granary-courtyard-wall-layout.json').write_text(json.dumps({'components':components,'sourceFootBasis':{'main':[[326,231],[371,253],[378,249]],'pier':[[369,251],[377,257],[387,252]],'uncertaintySourcePixels':2,'authoredPierRearFoot':[379,248],'oldRejectedAuthoredPierRearFoot':[379,246],'rearFootReason':'93 fixed actual Torren front-boot lower-edge samples:28inside old inferred base,0inside narrowed base; visible front/side feet unchanged'},'omittedOriginalLowExtension':{'approximateFoot':[395,261],'reason':'Original material occupies legal(24,15) centre(392,248); not carried into selected'},'oldMaskNotGeometry':True},indent=2)+'\n')
