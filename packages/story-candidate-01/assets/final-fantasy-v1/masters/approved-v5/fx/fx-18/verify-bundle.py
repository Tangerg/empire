from pathlib import Path
import json,hashlib
R=Path(__file__).parent;sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
r=json.loads((R/'final-review.json').read_text())
for k in ['sourceEdgeProof','cohortReview','motionProof']:assert (R/r[k]).is_file(),k
c=json.loads((R/'integration-catalog.json').read_text());assert set(c)=={'schemaVersion','assets'};assert list(c['assets'])==['C01-FX-18']
for f in c['assets']['C01-FX-18']['phases']:
 p=R/f['phase']/'master.png';assert sha(p)==f['sha256'];assert sha(p)==sha(p.with_name('raw.png'))
refs=0
for p in R.rglob('reference-hashes.json'):
 for x in json.loads(p.read_text()):assert sha(R/x['snapshot'])==x['sha256'];refs+=1
for p in R.rglob('source-evidence.json'):
 d=json.loads(p.read_text())
 if 'audit' in d:assert sha((p.parent/d['audit']).resolve())==d['auditSha256']
files=[{'path':str(p.relative_to(R)),'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(R.rglob('*')) if p.is_file() and p.name!='deliverable-manifest.json']
m={'topicId':'C01-FX-18','root':str(R),'status':r['status'],'phaseOrder':r['phaseOrder'],'sourceCount':4,'imagegenAttemptCount':5,'rejectedAttemptCount':1,'referenceSnapshotCount':refs,'sourceComplete':True,'browserObserved':False,'liveGameObserved':False,'repoWrites':False,'files':files}
(R/'deliverable-manifest.json').write_text(json.dumps(m,indent=2)+'\n')
for f in json.loads((R/'deliverable-manifest.json').read_text())['files']:assert sha(R/f['path'])==f['sha256']
print(json.dumps({'verifiedFiles':len(files),'verifiedReferenceSnapshots':refs,'manifestSHA256':sha(R/'deliverable-manifest.json'),'allLinkedEvidenceExists':True},indent=2))
