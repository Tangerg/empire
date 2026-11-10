"""Independent read-only review of the proposed gravekeeper idle source extraction."""
from pathlib import Path
from PIL import Image
import hashlib, json
import numpy as np
from scipy.ndimage import label, distance_transform_edt

art=Path(__file__).resolve().parents[1]
repo=art.parents[1]
meta=json.loads((art/'extraction-review.json').read_text())
sha=lambda data: hashlib.sha256(data).hexdigest()
source=np.asarray(Image.open(art/meta['source']).convert('RGBA'))
runtime=np.asarray(Image.open(art/meta['runtimePng']).convert('RGBA'))
assert sha((art/meta['source']).read_bytes())==meta['sourceSha256']
assert sha(source.tobytes())==meta['sourceRgbaSha256']
assert sha((art/meta['runtimePng']).read_bytes())==meta['runtimePngSha256']
assert sha(runtime.tobytes())==meta['runtimeRgbaSha256']
assert [source.shape[1],source.shape[0]]==meta['sourceDimensions']
assert [runtime.shape[1],runtime.shape[0]]==meta['runtimeDimensions']
for ref in meta['references']:
 assert sha((repo/ref['path']).read_bytes())==ref['sha256']
components,n=label(source[:,:,3]>16)
counts=np.bincount(components.ravel())
ids=[i for i in range(1,n+1) if counts[i]>1000]
ids.sort(key=lambda i:np.nonzero(components==i)[1].min())
assert len(ids)==4
significant=np.where(np.isin(components,ids),components,0)
distance,indices=distance_transform_edt(significant==0,return_indices=True)
owners=significant[tuple(indices)]
retained=(distance<=3)&(source[:,:,3]>0)
removed=(source[:,:,3]>0)&~retained
assert int(removed.sum())==27804==meta['removedPixels']
assert int(source[:,:,3][removed].max())==9==meta['maxRemovedAlpha']
assert int(retained.sum())==488890==meta['retainedPixels']
assert np.all(retained[source[:,:,3]>16])
expected=np.zeros_like(runtime)
frames=[]
for index,(cid,frame) in enumerate(zip(ids,meta['frames'])):
 mask=retained&(owners==cid)
 l,t,r,b=frame['sourceBounds']; x,y,w,h=frame['frameRegion']
 crop=source[t:b,l:r].copy(); crop[~mask[t:b,l:r]]=0
 assert crop.shape==(h,w,4)
 assert sha(crop.tobytes())==frame['pixelSha256']
 assert int(mask.sum())==frame['retainedPixels']
 ax,ay=frame['sourceAnchor']; fx,fy=frame['frameAnchor']
 assert [ax-l,ay-t]==[fx,fy]
 yy,xx=np.nonzero(crop[:,:,3])
 padding=[int(xx.min()),int(yy.min()),int(w-1-xx.max()),int(h-1-yy.max())]
 assert min(padding)==meta['transparentPaddingPixels']==12
 expected[y:y+h,x:x+w]=crop
 frames.append({'frame':index,'paddingLTRB':padding,'hash':frame['pixelSha256'],
                'nonzeroAlphaBoundsFromAnchor':[int(xx.min()-fx),int(yy.min()-fy),int(xx.max()+1-fx),int(yy.max()+1-fy)],
                'nativeBoundsFromAnchor':[round(v*meta['presentationScale'],4) for v in [xx.min()-fx,yy.min()-fy,xx.max()+1-fx,yy.max()+1-fy]]})
assert len(set(f['hash'] for f in frames))==4
assert np.array_equal(expected,runtime)
# A single visible RGBA-channel mutation must be rejected by the exact-pixel check.
mutated=runtime.copy(); yy,xx=np.nonzero(mutated[:,:,3]); mutated[yy[0],xx[0],0]^=1
assert not np.array_equal(expected,mutated)
seq=json.loads((art/'diagnostic-sequence.json').read_text())
gif=Image.open(art/'anchored-breathe.gif')
durations=[]
for i in range(gif.n_frames):
 gif.seek(i); durations.append(gif.info.get('duration'))
assert durations==[item['spanMs'] for item in seq['sequence']]
assert sum(durations)==seq['totalMs']
result={'status':'pass-source-extraction-and-offline-registration-only','sourceSha256':meta['sourceSha256'],
 'runtimeSha256':meta['runtimePngSha256'],'sourceSignificantComponents':4,'retainedOriginalRgbaPixels':int(retained.sum()),
 'discardedDetachedPixels':int(removed.sum()),'maximumDiscardedAlpha':9,
 'allAlphaOver16Retained':True,'allFourAtlasComponentsEqualOriginalRgba':True,'singleChannelMutationRejected':True,
 'uniformScale':meta['presentationScale'],'frameOrder':meta['frameOrder'],'playbackFrames':meta['playbackFrames'],
 'playbackSpansMs':meta['playbackSpansMs'],'gifFrameDurationsMs':durations,'frames':frames,
 'limitations':['Native battle playback not reviewed in this report.','Offline diagnostic still frames and GIF timing inspected; continuous realtime playback not claimed.','Production manifest not verified until integration.']}
(Path(__file__).resolve().parent/'independent-source-verification.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps(result,indent=2))
