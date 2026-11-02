"""Bounded provenance check of eight existing comparison groups; no engine execution."""
from pathlib import Path
from PIL import Image
import numpy as np,json,hashlib,argparse
parser=argparse.ArgumentParser();parser.add_argument('--repo',type=Path,default=Path('empire-c01'));args=parser.parse_args()
base=args.repo/'godot/docs/art-provenance/c07-layered-v1'
groups=[
 ('depth-first', ['depth-draw-batch-checkpoint/full-scene-cost-selected-1.json.png','depth-draw-batch-checkpoint/full-scene-cost-batched-selected-1.json.png'],0),
 ('depth-abba',[f'depth-draw-batch-checkpoint/depth-abba-{v}.json.png' for v in ['A1','B1','B2','A2']],0),
 ('depth-gpu',[f'depth-draw-batch-checkpoint/gpu/surface-order-{v}.png' for v in ['before','after']],0),
 ('blend-half-pixel',[f'surface-batching-audit/surface-blend-{v}-z2-s0.5.png' for v in ['unbatched','batched']],0),
 ('formal-fence-batching',['production-surface-audit/formal-fence-after-60-12.png','surface-batching-audit/surface-batch-real-60-12.png'],0),
 ('opaque-certificate',['opaque-material-certificate-audit/depth-draw-build-phases.json.png']+[f'opaque-material-certificate-audit/opaque-certificate-native-{v}.json.png' for v in ['first','second']],0),
 ('nw-old-pruning',['northwest-formal-structure-study/whole-gate-leaf-joinery-default125.png','northwest-formal-structure-study/whole-gate-pruned-default125.png'],72),
 ('nw-old-selected',['northwest-formal-structure-study/whole-gate-pruned-default125.png','northwest-formal-structure-study/selected-northwest-default125.png'],72)]
rows=[]
for name,names,top in groups:
 images=[];inputs=[]
 for n in names:
  p=base/n;raw=p.read_bytes()
  with Image.open(p) as im:
   inputs.append({'path':n,'actualFormat':im.format,'mode':im.mode,'magic':raw[:8].hex(),'sha256':hashlib.sha256(raw).hexdigest(),'nativeLosslessEligible':raw[:8]==b'\x89PNG\r\n\x1a\n' and im.format=='PNG' and im.mode=='RGBA'});images.append(np.array(im))
 comparisons=[]
 for other in images[1:]:
  assert other.shape==images[0].shape
  delta=np.abs(images[0][top:].astype(int)-other[top:].astype(int));comparisons.append({'changedPixels':int(np.any(delta!=0,2).sum()),'maxChannelDelta':int(delta.max())})
 rows.append({'group':name,'inputs':inputs,'excludedCaptionHeight':top,'decodedComparisonsToFirst':comparisons,'conclusion':'Existing lossless evidence retained' if all(v['nativeLosslessEligible'] for v in inputs) else 'Lossy decoded screenshot equality only; superseded by new native-lossless NW captures'})
report={'scope':'Eight named high-value groups only. Header/mode/hash and direct decoded comparison; no gameplay, performance rerun, or generalized withdrawal.', 'groups':rows}
(Path(__file__).parent/'historical-image-oracle-audit.json').write_text(json.dumps(report,indent=2)+'\n');print([(r['group'],r['conclusion'],r['decodedComparisonsToFirst'])for r in rows])
