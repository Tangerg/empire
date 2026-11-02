from pathlib import Path
from PIL import Image
import numpy as np,json,hashlib,argparse
root=Path(__file__).resolve().parent
parser=argparse.ArgumentParser();parser.add_argument('--check-input',type=Path);args=parser.parse_args()
import sys
tool_root = next((parent / 'tools/godot' for parent in root.parents
                  if (parent / 'tools/godot/native_image_oracle.py').is_file()),
                 root.parent / 'empire-c01/tools/godot')
sys.path.insert(0, str(tool_root))
from native_image_oracle import read_native_rgba_png as read
if args.check_input:
 read(args.check_input)
else:
 rows=[]
 for name,left,right in [('pruned-empty-supports','platform-leaf','platform-pruned-leaf'),('selected-integration','platform-pruned-leaf','selected-northwest')]:
  a,ai=read(root/f'native-lossless-{left}.png');b,bi=read(root/f'native-lossless-{right}.png')
  assert a.shape==b.shape
  difference=np.abs(a[72:].astype(int)-b[72:].astype(int))
  count=int(np.any(difference!=0,axis=2).sum());maximum=int(difference.max())
  rows.append({'contract':name,'inputs':[ai,bi],'worldChangedPixels':count,'maxChannelDelta':maximum,'excludedCaptionHeight':72})
  if count or maximum:raise ValueError('Native framebuffer equivalence failed: '+name)
 result={'scope':'Direct Godot viewport GetImage/SavePng, PNG signature+RGBA validated. Static1.25same-camera C07 fixture; not gameplay. Only72px caption excluded. Supersedes previous CUA JPEG-based numerical claim.', 'pairs':rows}
 (root/'native-lossless-framebuffer-equivalence.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
