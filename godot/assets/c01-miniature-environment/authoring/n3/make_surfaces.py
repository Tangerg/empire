"""Original authored PBR tile surface, not a lighting/scene backplate."""
from pathlib import Path
from PIL import Image
import numpy as np,json,os
R=Path(os.environ['C01_N3_OUTPUT']);R.mkdir(parents=True,exist_ok=True);size=1024
palette={'plaster':[.52,.46,.35],'oak':[.23,.14,.075],'worn_oak':[.34,.23,.12],'inset':[.075,.10,.105],'stone0':[.185,.186,.172],'stone1':[.20,.20,.18],'stone2':[.216,.213,.199],'slate_cap':[.18,.19,.24],'iron':[.095,.10,.105],'canvas':[.37,.40,.40],'canvas_stripe':[.54,.50,.41]}
a=np.zeros((size,size,3),float);a[:]=[.18,.19,.24];normal=np.zeros_like(a);normal[:]=[.5,.5,1]
uv={}
for i,(name,c) in enumerate(palette.items()):
 x=(i%8)*128;y=(i//8)*128;a[y:y+128,x:x+128]=c;uv[name]=[(x+64)/size,1-(y+64)/size]
y,x=np.mgrid[:768,:1024];row=y//128;tx=(x+(row%2)*42.6667)/85.3333;col=np.floor(tx);u=tx-col;v=(y%128)/128
# Soft stone edge relief and slight overlapping course thickness; no painted cast shadow.
edge=np.minimum(np.minimum(u,1-u)*85.3333,np.minimum(v,1-v)*128);t=np.clip(edge/4,0,1);bevel=t*t*(3-2*t)
height=.006*bevel+.005*v*bevel
variation=np.sin(col*7.1+row*13.3)*.045+np.sin(col*3.7-row*9.1)*.025
weather=.012*np.sin(x*.041+y*.025)+.008*np.sin(x*.11-y*.07)
base=np.array([.175,.18,.225]);roof=base[None,None,:]*(1+variation[:,:,None]+weather[:,:,None]);roof*= (.88+.12*bevel[:,:,None])
a[256:]=roof
# Derivatives in physical metres: roof patch is about3.8m by1.2m.
dy,dx=np.gradient(height);nx=-dx/(3.8/1024);ny=dy/(1.2/768);nz=np.ones_like(nx);length=np.sqrt(nx*nx+ny*ny+nz*nz);normal[256:]=np.stack((nx/length*.5+.5,ny/length*.5+.5,nz/length*.5+.5),2)
srgb=np.where(a<=.0031308,a*12.92,1.055*np.maximum(a,0)**(1/2.4)-.055)
Image.fromarray(np.uint8(np.clip(np.rint(srgb*255),0,255))).save(R/'c01_n3_surface_albedo.png');Image.fromarray(np.uint8(np.rint(normal*255))).save(R/'c01_n3_surface_normal.png');(R/'surface-palette.json').write_text(json.dumps({'colors':palette,'uv':uv,'roof_uv_rect':[0,0,1,.75],'tile_columns':12,'tile_courses':6},indent=2))
