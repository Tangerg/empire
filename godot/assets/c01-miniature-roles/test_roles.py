"""Deterministic texture checks; run beside draw_roles.py after regeneration."""
import sys
sys.dont_write_bytecode = True
import hashlib
import importlib.util
import json
from pathlib import Path
from PIL import Image, ImageOps

ROOT=Path(__file__).parent
spec=importlib.util.spec_from_file_location('draw_roles',ROOT/'draw_roles.py')
art=importlib.util.module_from_spec(spec);spec.loader.exec_module(art)
QA = art.diagnostic_output()
manifest=json.loads((ROOT/'manifest.json').read_text())
level=json.loads((ROOT.parents[1]/'data/grayflag-campaign-levels.json').read_text())[0]
assert len(level['units'])==19
assert {u['unit'] for u in level['units']}==set(manifest['unitRoles'])
assert manifest['footAnchor']==[32,56]
results={'canonicalUnits':19,'coveredTypes':9,'frames':18,'roles':{}}
for role in art.ROLES:
    pair=Image.open(ROOT/(role+'-views.png')).convert('RGBA')
    assert pair.size==(128,64)
    frames=[];record=[]
    for rear in [False,True]:
        f=pair.crop((64*int(rear),0,64*(int(rear)+1),64))
        assert f.tobytes()==art.role_image(role,rear).tobytes()
        alpha=list(f.getchannel('A').tobytes());assert set(alpha)=={0,255}
        assert all(f.getpixel((x,y))[3]==0 for x,y in [(0,y) for y in range(64)]+[(63,y) for y in range(64)]+[(x,0) for x in range(64)]+[(x,63) for x in range(64)])
        assert max(y for y in range(64) for x in range(64) if f.getpixel((x,y))[3])==56
        frames.append(f)
        record.append({'bbox':list(f.getbbox()),'opaquePixels':alpha.count(255),'partialAlpha':0,'bottomPixelY':56})
    assert frames[1].tobytes()!=ImageOps.mirror(frames[0]).tobytes()
    assert hashlib.sha256((ROOT/(role+'-views.png')).read_bytes()).hexdigest()==manifest['roles'][role]['sha256']
    results['roles'][role]=record
(QA/'qa.json').write_text(json.dumps(results,indent=2)+'\n')
preview=Image.new('RGBA',(64*len(art.ROLES),128),(98,107,98,255))
for i,role in enumerate(art.ROLES):
    pair=Image.open(ROOT/(role+'-views.png')).convert('RGBA')
    preview.alpha_composite(pair.crop((0,0,64,64)),(i*64,0))
    preview.alpha_composite(pair.crop((64,0,128,64)),(i*64,64))
preview.save(QA/'roles-native.png')
gray=ImageOps.grayscale(preview).convert('RGBA');gray.putalpha(preview.getchannel('A'));gray.save(QA/'roles-grayscale-silhouette.png')
preview.resize((3456,768),Image.Resampling.NEAREST).save(QA/'roles-6x.png')
print('PASS: 19 canonical C01 units / 9 types; 18 native frames; exact reconstruction, alpha, perimeter, y56, no rear mirror.')
