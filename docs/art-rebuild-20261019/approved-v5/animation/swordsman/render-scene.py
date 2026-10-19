from pathlib import Path
import json,xml.etree.ElementTree as ET,subprocess
from PIL import Image
r=Path(__file__).parent;root=r.parents[1];ns='http://www.w3.org/2000/svg';xl='http://www.w3.org/1999/xlink';out=r/'scene';out.mkdir(exist_ok=True)
c=json.loads((r/'export/sequence.json').read_text());pivot=c['logicalPivot'];names=list(c['config']['frames']);reports=[]
for name in names:
 t=ET.parse(root/'edge-scene-wide/scene.svg');count=0
 for n in t.getroot().iter('{'+ns+'}image'):
  href=n.get('href') or n.get('{'+xl+'}href','')
  if href.endswith('/export-red/swordsman%402x.png'):
   s=float(n.get('width'))/1585;foot=[float(n.get('x'))+800*s,float(n.get('y'))+887*s]
   n.set('x',str(foot[0]-pivot[0]));n.set('y',str(foot[1]-pivot[1]));n.set('width','96');n.set('height','64');href=(r/f'export/{name}@2x.png').as_uri();n.set('href',href);n.set('{'+xl+'}href',href);count+=1
 if count!=3:raise ValueError(f'expected three blue actors, found {count}')
 p=out/f'{name}.svg';t.write(p,encoding='unicode');subprocess.run(['inkscape',str(p),'--export-type=png',f'--export-filename={out/name}.png'],check=True,capture_output=True)
 reports.append({'pose':name,'blueActors':count,'redActors':'unchanged idle','positions':'unchanged'})
frames=[]
for n in ['idle','walk-a','walk-b','walk-a','walk-b','idle','attack','idle']:
 im=Image.open(out/f'{n}.png').convert('RGBA');bg=Image.new('RGBA',im.size,'#e8e2d5');bg.alpha_composite(im);frames.append(bg.convert('RGB'))
frames[0].save(out/'scene-preview.gif',save_all=True,append_images=frames[1:],duration=220,loop=0,disposal=2)
(out/'review.json').write_text(json.dumps({'kind':'deterministic staged-scene animation preview, not live gameplay','frames':reports,'sharedScale':c['config']['scale'],'cameraAndTerrainChanged':False},indent=2)+'\n')
