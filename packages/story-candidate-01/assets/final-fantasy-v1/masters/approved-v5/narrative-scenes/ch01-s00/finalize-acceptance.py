from pathlib import Path
from PIL import Image
import json,hashlib
O=Path('/workspace/shared/empire-art-rebuild/approved-v5/narrative-scenes/ch01-s00')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def dump(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n')
accept={'accepted':True,'reviewedAt':'2026-10-20T04:22:15Z','scope':'Exact full square1254 source and native128 canonical only; formal export/integration parent-owned','review':'Parent reviewed corrected full square plus native128 contact sheet: left-arm fastening, both faces and intimate pre-battle interaction pass. Accept for square scene formal export only; wide/portrait cover diagnostics remain explicitly unsuitable. Please finalize hashes/provenance, no more generation. I will integrate whole1254 source and128 canonical, preserving prologueS01 selector.','sourceSha256':sha(O/'master.png'),'sourceCropBox':None,'noCurrentConsumerAdded':True}
dump(O/'coordinator-acceptance.json',accept)
for n in ['source-qa.json','generation-provenance.json']:
 p=O/n;x=json.loads(p.read_text());x['status']='Coordinator accepted exact square source and128 canonical; formal integration parent-owned';x['coordinatorAcceptance']=accept;dump(p,x)
p=O/'semantic-review.json';x=json.loads(p.read_text());x['sourceStatus']='Coordinator accepted for square scene formal export only; non-square cover diagnostics explicitly unsuitable';x['coordinatorAcceptance']=accept;dump(p,x)
p=O/'final-verification.json';x=json.loads(p.read_text());x['coordinatorAcceptance']=accept;x['sourceAccepted']=True;dump(p,x)
p=O/'source-review.md';s=p.read_text();h='\n## Coordinator acceptance\n\n';s=s.split(h)[0]+h+'2026-10-20 04:22UTC: parent reviewed the full square and exact128×128 proof and accepted the corrected left-arm fastening, both named faces and intimate pre-battle interaction. Accepted for square scene formal export only. Wide and portrait cover diagnostics remain explicitly unsuitable. Parent will integrate the whole1254 source and128 canonical while preserving prologueS01. No additional generation requested.\n';p.write_text(s)
p=O/'bundle-manifest.json';m=json.loads(p.read_text());m['sourceStatus']='Coordinator accepted for square export only';m['coordinatorAcceptance']=accept;m['files']=[]
for p in sorted(O.rglob('*')):
 if p.is_file() and p.name!='bundle-manifest.json':
  v={'path':str(p.relative_to(O)),'bytes':p.stat().st_size,'sha256':sha(p)}
  if p.suffix=='.png':
   im=Image.open(p);v.update(size=list(im.size),mode=im.mode)
  m['files'].append(v)
dump(O/'bundle-manifest.json',m)
print('Finalized',len(m['files']),'files; source',m['sourceSha256'])
assert all(sha(O/x['path'])==x['sha256'] for x in m['files'])
print('All bundle file hashes verify')
