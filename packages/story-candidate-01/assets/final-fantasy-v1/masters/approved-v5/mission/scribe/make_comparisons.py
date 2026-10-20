from pathlib import Path
from PIL import Image
import json, hashlib

B = Path(__file__).parent
ROOT = Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019')
POSES = ['idle', 'walk-a', 'carry-or-work', 'walk-b']
COLORS = {'black': (0, 0, 0), 'white': (255, 255, 255), 'scene': (109, 126, 93)}
FRAME = (104, 64)
def composite(im, color):
    bg = Image.new('RGBA', FRAME, color+(255,))
    bg.alpha_composite(im)
    return bg.convert('RGB')

# The starting inputs here are actual104x64 rendered1x PNGs, not high-resolution views.
tiles = {p: Image.open(B/'qa-export'/f'{p}@1x.png') for p in POSES}
board = Image.new('RGB', (104*4,64*3))
for j,color in enumerate(COLORS.values()):
    for i,p in enumerate(POSES): board.paste(composite(tiles[p],color),(104*i,64*j))
board.save(B/'qa-native44-three-backgrounds.png')
board.resize((board.width*3,board.height*3),Image.Resampling.NEAREST).save(B/'qa-native44-three-backgrounds-3x.png')
idle = Image.new('RGB',(104*3,64))
for i,color in enumerate(COLORS.values()): idle.paste(composite(tiles['idle'],color),(104*i,0))
idle.save(B/'qa-idle-native44-three-backgrounds.png')
idle.resize((idle.width*3,idle.height*3),Image.Resampling.NEAREST).save(B/'qa-idle-native44-three-backgrounds-3x.png')

family = ['border-farmer', 'refugee-adult', 'bridge-laborer', 'scribe']
family_board = Image.new('RGB', (104*4,64*3))
evidence=[]
audit=json.loads((ROOT/'docs/art-rebuild-20261019/approved-v5/mission-consumer-audit/mission-consumer-audit.json').read_text())
for i,name in enumerate(family):
    if name == 'scribe': path=B/'qa-export/idle@1x.png'; frame=Image.open(path)
    else:
        path=ROOT/f'packages/story-candidate-01/assets/final-fantasy-v1/runtime-approved-v5/mission-unit/mission-{name}.png'
        sheet=Image.open(path)
        assert sheet.size==(416,64), (name,sheet.size)
        frame=sheet.crop((0,0,104,64))
    for j,color in enumerate(COLORS.values()): family_board.paste(composite(frame,color),(104*i,64*j))
    topic='C01-MISSION-'+name.upper()
    record=next(t for t in audit['topics'] if t['topicId']==topic)
    evidence.append({'topicId':topic,'source':str(path),'sourceSha256':hashlib.sha256(path.read_bytes()).hexdigest(),'sourceType':'Unintegrated shared QA1x proposal' if name=='scribe' else 'Existing repository-approved1x runtime sheet, idle frame exact crop','nativeFrameSize':[104,64],'rescaling':False,'actualConsumerClassification':record['classification'],'actualConsumerCounts':record['actualConsumerCounts']})
family_board.save(B/'qa-family-actual-bound-native44.png')
family_board.resize((family_board.width*3,family_board.height*3),Image.Resampling.NEAREST).save(B/'qa-family-actual-bound-native44-3x.png')
(B/'qa-family-actual-bound-evidence.json').write_text(json.dumps({'leftToRight':family,'topToBottom':['black','white','scene'],'oneXPolicy':'All inputs are actual104x64 PNG frames. No family member rescaling.3x file is nearest-neighbor enlargement of the already composed1x board.','members':evidence,'liveGamePlayback':False,'scribeDeployed':False},indent=2)+'\n')

reg=json.loads((B/'registration-evidence.json').read_text())
common=reg['commonSourceCanvas']
for name,color in COLORS.items():
    montage=Image.new('RGB',(round(common[0]*.45)*2,round(common[1]*.45)*2),color)
    for i,p in enumerate(POSES):
        im=Image.open(B/p/f'qa-{name}.png')
        tile=im.resize((round(im.width*.45),round(im.height*.45)),Image.Resampling.LANCZOS)
        montage.paste(tile,((i%2)*round(common[0]*.45),(i//2)*round(common[1]*.45)))
    montage.save(B/f'qa-native-montage-{name}.png')
walk=[composite(tiles[p],COLORS['scene']).resize((416,256),Image.Resampling.NEAREST) for p in ['walk-a','walk-b']]
walk[0].save(B/'qa-walk-6.25fps.gif',save_all=True,append_images=walk[1:],duration=160,loop=0)
