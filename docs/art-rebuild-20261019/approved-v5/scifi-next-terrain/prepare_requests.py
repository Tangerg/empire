import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[4]
pack=ROOT/'packages/story-candidate-02/assets/final-scifi-v1'
ref=str(pack/'masters/terrain/terrain-board-03.png')
style=str(pack/'masters/approved-v5/canonical-terrain/farlight-cargo-hold/master.png')
subjects={
'kairon-separation-ring':('fourth','Broad curved beige-gray segmented orbital ring pavement with charcoal recessed seams and sparse amber edge markings, against muted midnight violet void. Translate the ring identity into an overhead material of wide interlocking curved deck slabs, not a perspective bridge.'),
'kairon-train-platform':('first','Warm beige-gray heavy railway platform plates, a broad central round cyan-inset mechanism and sparse amber safety strips, charcoal metal seams. Four distinct arrangements of this same material, with readable large plates and cyan mechanical insets.'),
'kairon-vacuum-breach':('third','Broken gray-brown spacecraft deck slabs surrounding dark navy vacuum gaps, with large exposed graphite conduit shapes and sparse cyan mechanical accents. One bold dark angular breach near the central core, with broad cracked deck fragments crossing cardinal strips. No transparency; the vacuum is painted opaque navy.'),
'kairon-vertical-city':('second','Pale warm-gray urban deck plates, broad charcoal structural seams and cyan utility channels, compact teal inset panels. Translate the vertical city identity to an overhead walkable deck material, with cyan conduits and engineered slabs, not tall buildings or a perspective city scene.')}
requests=[]
for slug,(col,subject) in subjects.items():
 d=pack/'masters/approved-v5/canonical-terrain'/slug; d.mkdir(exist_ok=False)
 comp=('Exactly four equally sized square material variants in a precise 2-by-2 arrangement occupying the entire square canvas. Each quadrant is an independent overhead square, equal scale, no gutters, divider lines, frames or labels. Quadrants will be sampled whole in top-left, top-right, bottom-left, bottom-right order.' if slug.endswith('train-platform') else 'Exactly one square continuous material, not a sheet and not multiple variants. Concentrate the essential motif within central 40 percent and four cardinal middle-quarter strips for subsequent historical NESW masking.')
 prompt=f'Use case: stylized-concept. Create ONE independently authored game terrain material master for c02-terrain-{slug}. Input image 1 is subject reference ONLY: use its top row {col} column, ignore all seven other subjects. Input image 2 is STYLE ONLY: match its chunky matte hand-painted tactical RPG science-fiction language, bold dark contour lines, broad beveled shapes, restrained two-step cel shading; do not copy its cargo motifs or layout. Subject: {subject} Composition: flat orthographic overhead ground texture, no perspective or walls. {comp} Fully fill the canvas with opaque material, every pixel opaque, material extends to every edge. Readable at 32px logical tile size. No text, symbols, labels, arrows, gridlines, borders, blank margins, tiny speckles, glossy 3D, photographic detail or scene framing.'
 req={'prompt':prompt,'referenced_image_paths':[ref,style],'transparent_background':False}
 (d/'prompt.txt').write_text(prompt); (d/'request.json').write_text(json.dumps(req,indent=2)); requests.append({'slug':slug,**req})
(ROOT/'docs/art-rebuild-20261019/approved-v5/scifi-next-terrain/reservation.json').write_text(json.dumps({'topics':['c02-terrain-'+s for s in subjects],'reason':'Next uncompleted manifest-order C02 family after Farlight and Hesha','requests':requests},indent=2))
