"""Checks visual atlas contracts without changing or simulating gameplay receipts."""
import sys
sys.dont_write_bytecode = True
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw
from draw_actions import pose
from draw_roles import diagnostic_output
ROOT=Path(__file__).parent
QA = diagnostic_output()
meta=json.loads((ROOT/'action-sheets.json').read_text())
contracts=json.loads((ROOT.parents[1]/'data/selected-assets.json').read_text())['actorClips']
report={'scope':'unbound visual assets; no gameplay event tests','roles':{}}
preview=Image.new('RGBA',(13*64,6*64),(98,107,98,255))
for row,(role,entry) in enumerate(meta['roles'].items()):
    im=Image.open(ROOT/(role+'-actions.png')).convert('RGBA')
    assert hashlib.sha256((ROOT/(role+'-actions.png')).read_bytes()).hexdigest()==entry['sha256']
    for source_action, target_clip in entry['sourceActions'].items():
        assert source_action in ['attack','cast']
        assert target_clip in entry['clips']
    counts={}
    for name,clip in entry['clips'].items():
        if 'releaseFrame' in clip:
            assert sum(clip['durationsMs'][:clip['releaseFrame']])==contracts[clip['existingClipContract']]['releaseMs']
        for view in ['frontFrames','backFrames']:
            unique=set()
            for phase,(x,y,w,h) in enumerate(clip[view]):
                assert w==h==64 and x+w<=im.width and y+h<=im.height
                f=im.crop((x,y,x+w,y+h));assert set(f.getchannel('A').tobytes())=={0,255}
                assert f.tobytes()==pose(role,'attack' if name=='cast' else name,phase,view=='backFrames').tobytes()
                assert all(f.getpixel(pt)[3]==0 for pt in [(0,y) for y in range(64)]+[(63,y) for y in range(64)]+[(x,0) for x in range(64)]+[(x,63) for x in range(64)])
                unique.add(f.tobytes())
            assert len(unique)>=3
            counts[name+'-'+view]={'frames':len(clip[view]),'distinct':len(unique)}
    report['roles'][role]=counts
    preview.alpha_composite(im.crop((0,0,im.width,64)),(0,row*64))
preview.save(QA/'actions-native.png');preview.resize((1664,768),Image.Resampling.NEAREST).save(QA/'actions-2x.png')
(QA/'action-qa.json').write_text(json.dumps(report,indent=2)+'\n')
print('PASS: source reconstruction, pure alpha, clear edge guards, distinct poses, existing75/160/300/320ms release boundaries.')
