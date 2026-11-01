from PIL import Image
import numpy as np,json,pathlib
root=pathlib.Path('/workspace/scratch/84c9fe948755');out=pathlib.Path(__file__).parent
im=np.asarray(Image.open(root/'empire-c01/godot/assets/native/c07-layered-v1/barracks-front-lean-to-v2-candidate.png').convert('RGBA'));a=im[:,:,3];q=np.array([[115.,391.],[496.,127.],[947.,383.],[559.,655.]])
def scan(p):
 x0,y0=np.floor(p.min(0)).astype(int);x1,y1=np.ceil(p.max(0)).astype(int);xx,yy=np.meshgrid(np.arange(x0,x1+1),np.arange(y0,y1+1));valid=np.ones(xx.shape,dtype=bool)
 for edge in np.roll(p,-1,axis=0)-p:
  ax=np.array([-edge[1],edge[0]]);pro=p@ax;lo=xx*ax[0]+yy*ax[1]+min(0,ax[0])+min(0,ax[1]);hi=xx*ax[0]+yy*ax[1]+max(0,ax[0])+max(0,ax[1]);valid &= (hi>=pro.min()-1e-8)&(lo<=pro.max()+1e-8)
 vals=a[yy,xx];bad=valid&(vals<245);ys,xs=np.where(bad);pts=[[int(xx[y,x]),int(yy[y,x]),int(vals[y,x])] for y,x in zip(ys,xs)]
 return {'uv':p.tolist(),'touched':int(valid.sum()),'badCount':len(pts),'minAlpha':int(vals[valid].min()),'badBounds':None if not pts else [min(v[0] for v in pts),min(v[1] for v in pts),max(v[0] for v in pts),max(v[1] for v in pts)],'firstBad':pts[:20]},pts
rows=[]
for f in [1,.999,.998,.997,.995,.99,.985,.98,.97]:
 r,pts=scan(q.mean(0)+(q-q.mean(0))*f);r['centroidScale']=f;rows.append(r);print(json.dumps(r));
 if f==1:(out/'original-bad-texels.json').write_text(json.dumps(pts))
(out/'scan-results.json').write_text(json.dumps(rows,indent=2))
