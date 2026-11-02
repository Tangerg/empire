"""Strict native readback equivalence: chosen geometry migration, not JPEG capture."""
from pathlib import Path
import json,sys
import numpy as np
root=Path(__file__).resolve().parent
tools=next((p/'tools/godot' for p in root.parents if (p/'tools/godot/native_image_oracle.py').is_file()),root.parent/'empire-c01/tools/godot')
sys.path.insert(0,str(tools))
from native_image_oracle import read_native_rgba_png
before,a=read_native_rgba_png(root/'whole-cost-candidate65.png')
after,b=read_native_rgba_png(root/'whole-selected65.png')
if before.shape!=after.shape:raise ValueError('Native frame dimensions changed')
delta=np.abs(before[72:].astype(int)-after[72:].astype(int))
result={'scope':'Same Godot viewport .65 camera and actor; only72px diagnostic caption excluded; no actions. Native RGBA signature/mode checked.', 'inputs':[a,b], 'worldChangedPixels':int(np.any(delta!=0,axis=2).sum()),'maxChannelDelta':int(delta.max())}
(root/'selected-native-equivalence.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps(result,indent=2))
if result['worldChangedPixels']!=0:raise AssertionError('Selected migration changed native world pixels')
