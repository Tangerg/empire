"""Place formally bound blue/red sheets in the approved staged fixture."""
from pathlib import Path
import hashlib,json,subprocess,xml.etree.ElementTree as ET
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[2]
PACK=ROOT/'packages/story-candidate-01/assets/final-fantasy-v1'
DOC=ROOT/'docs/art-rebuild-20261019/approved-v5'
OUT=DOC/'bound-team-animation';OUT.mkdir(exist_ok=True)
NS='http://www.w3.org/2000/svg';XL='http://www.w3.org/1999/xlink'
manifest=json.loads((PACK/'manifest-final-fantasy-v1.json').read_text())
hd=json.loads((PACK/'manifest-tactical-runtime-hd.json').read_text())
blue=next(a for a in manifest['assets'] if a['topicId']=='C01-UNIT-SWORDSMAN')
blue={**blue,'hdPng':next(a['png'] for a in hd['assets'] if a['topicId']==blue['topicId'])}
red=json.loads((PACK/'manifest-authored-team-variants.json').read_text())['variants'][0]
records={'blue':blue,'red':red};hashes={};order=['idle','walk-a','attack','walk-b']
for team,record in records.items():
 path=PACK/record['hdPng'];hashes[team]=hashlib.sha256(path.read_bytes()).hexdigest();sheet=Image.open(path).convert('RGBA');w,h=record['frameWidth']*2,record['frameHeight']*2
 for i,pose in enumerate(order):sheet.crop((i*w,0,(i+1)*w,h)).save(OUT/f'{team}-{pose}.png')
for pose in order:
 tree=ET.parse(DOC/'edge-scene-wide/scene.svg');counts={'blue':0,'red':0}
 for node in tree.getroot().iter('{'+NS+'}image'):
  href=node.get('href') or node.get('{'+XL+'}href','')
  if '/export-red/' not in href or 'swordsman' not in href:continue
  team='red' if 'red-swordsman' in href else 'blue';i=counts[team];counts[team]+=1
  foot=([(272,191),(304,223),(272,255)] if team=='blue' else [(432,191),(400,223),(432,255)])[i]
  record=records[team];node.set('x',str(foot[0]-record['anchor'][0]));node.set('y',str(foot[1]-record['anchor'][1]));node.set('width',str(record['frameWidth']));node.set('height',str(record['frameHeight']));href=(OUT/f'{team}-{pose}.png').as_uri();node.set('href',href);node.set('{'+XL+'}href',href)
 if counts!={'blue':3,'red':3}:raise ValueError(counts)
 path=OUT/f'{pose}.svg';tree.write(path,encoding='unicode');subprocess.run(['inkscape',str(path),'--export-type=png',f'--export-filename={OUT/pose}.png'],check=True,capture_output=True)
frames=[]
for pose in ['idle','walk-a','walk-b','walk-a','walk-b','idle','attack','idle']:
 im=Image.open(OUT/f'{pose}.png').convert('RGBA');canvas=Image.new('RGB',(im.width,im.height+62),'#e8e2d5');canvas.paste(im,(0,62),im);draw=ImageDraw.Draw(canvas);draw.text((12,8),'FORMALLY BOUND BLUE / RED FOUR-POSE SHEETS',fill='#26372b');draw.text((12,26),'Staged 3v3 fixture; selected scenery; fixed positions; offline compositing, not live gameplay',fill='#26372b');draw.text((12,43),'Ground / bridge layer remains the approved calibration fixture; source PNG colours preserved',fill='#26372b');frames.append(canvas)
frames[0].save(OUT/'both-teams-formal-binding.gif',save_all=True,append_images=frames[1:],duration=220,loop=0,disposal=2)
frames[0].save(OUT/'idle-labeled.png')
(OUT/'provenance.json').write_text(json.dumps({'kind':'formally bound sheets in staged calibration scene, not live gameplay','sourceSha256':hashes,'manifestFiles':['manifest-final-fantasy-v1.json','manifest-tactical-runtime-hd.json','manifest-authored-team-variants.json'],'poses':order,'actors':6,'bothTeamsUseSwordsmanIdentity':True,'mapOrActorPositionsChanged':False,'rightSideFacing':'existing horizontal mirror','sourceAlphaModified':False,'sceneLayerLimit':'approved staged material/bridge fixture, not full formal ground integration'},indent=2)+'\n')
