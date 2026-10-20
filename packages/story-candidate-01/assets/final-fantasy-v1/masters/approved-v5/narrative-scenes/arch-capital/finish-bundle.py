"""Validate this completed source bundle and refresh its file manifest only."""
from pathlib import Path
from PIL import Image
import hashlib,json
D=Path(__file__).parent
sha=lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
a=json.loads((D/'audited-source.json').read_text())
f=json.loads((D/'final-verification.json').read_text())
g=json.loads((D/'generation-result.json').read_text())
for p in D.rglob('*.json'): json.loads(p.read_text())
assert sha(D/'original-full-board.png')==a['source']['sha256']
assert len({sha(D/n) for n in ['raw.png','native.png','master.png']})==1
assert sha(D/'master.png')==f['masterSha256']==g['sha256']
im=Image.open(D/'master.png')
assert im.size==(1672,941)
assert im.convert('RGBA').getchannel('A').getextrema()==(255,255)
assert Image.open(D/'qa-source-native-1664x936.png').size==(1664,936)
assert Image.open(D/'qa-logical-256x144.png').size==(256,144)
assert f['sourceNativeExportCrop']==[4,2,1668,938] and f['density']==6.5 and not f['upscaled']
assert sorted(p.name for p in D.glob('rejected-v*') if p.is_dir())==f['rejectedAttemptsPreserved']
(D/'bundle-manifest.json').write_text(json.dumps({'topicId':a['topicId'],'selectedMaster':'master.png','files':[{'path':str(p.relative_to(D)),'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(D.rglob('*')) if p.is_file() and p.name!='bundle-manifest.json']},indent=2)+'\n')
print(a['topicId'], 'PASS', sha(D/'master.png'))
