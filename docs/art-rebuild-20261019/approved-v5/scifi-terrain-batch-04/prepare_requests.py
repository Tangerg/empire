import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[4]
pack=ROOT/'packages/story-candidate-02/assets/final-scifi-v1'
ref=str(pack/'masters/terrain/terrain-board-04.png')
style=str(pack/'masters/approved-v5/canonical-terrain/farlight-cargo-hold/master.png')
subjects={
'milight-corona-service':('first','Warm bronze-gray heavy maintenance deck plates, broad copper seams, recessed charcoal vents and restrained amber inset rings reflecting the nearby orange stellar corona. Keep the solar orange as solid inset accents rather than painting a sun or space scene. Four distinct arrangements of this same broad plated material.'),
'milight-inverted-city':('third','Pale ivory warm-gray radial urban plaza plates, turquoise inset circular utility caps and broad charcoal structure seams. Translate the inverted-city architecture into flat overhead walkable urban ground; no towers or hanging buildings. Sparse muted purple inset garden-like color accents retain the reference identity without plants or objects.'),
'milight-rotating-blackout':('fourth','Very dark graphite rotating gravity deck, broad concentric segmented curved plates and clearly readable charcoal-blue bevels with sparse short solid amber emergency light insets. Deep dim blackout mood, but large panel shapes must remain distinct at 32px. No bloom or gradient glow.'),
'milight-vein-ring':('second','Broad vivid turquoise energy-vein channels following an engineered curved ring route, warm pale-gray deck plate bridges and dark graphite channel borders. Opaque cyan material, broad simple geometric seams. Place a strong turquoise junction motif in the central core and wide cyan channel portions across cardinal strips; no perspective bridge.')}
requests=[]
for slug,(col,subject) in subjects.items():
 d=pack/'masters/approved-v5/canonical-terrain'/slug; d.mkdir(exist_ok=False)
 comp=('Exactly four equally sized square material variants in a precise 2-by-2 arrangement occupying the entire square canvas. Each quadrant is an independent overhead square, equal scale, no gutters, divider lines, frames or labels. Quadrants will be sampled whole in top-left, top-right, bottom-left, bottom-right order.' if slug.endswith('corona-service') else 'Exactly one square continuous material, not a sheet and not multiple variants. Concentrate the essential motif within central 40 percent and four cardinal middle-quarter strips for subsequent historical NESW masking.')
 prompt=f'Use case: stylized-concept. Create ONE independently authored game terrain material master for c02-terrain-{slug}. Input image 1 is subject reference ONLY: use its bottom row {col} column, ignore all seven other subjects. Input image 2 is STYLE ONLY: match its chunky matte hand-painted tactical RPG science-fiction language, bold dark contour lines, broad beveled shapes, restrained two-step cel shading; do not copy its cargo motifs or layout. Subject: {subject} Composition: flat orthographic overhead ground texture, no perspective or walls. {comp} Fully fill the canvas with opaque material, every pixel opaque, material extends to every edge. Readable at 32px logical tile size. No text, symbols, labels, arrows, gridlines, borders, blank margins, tiny speckles, glossy 3D, photographic detail or scene framing.'
 req={'prompt':prompt,'referenced_image_paths':[ref,style],'transparent_background':False}
 (d/'prompt.txt').write_text(prompt); (d/'request.json').write_text(json.dumps(req,indent=2)); requests.append({'slug':slug,**req})
(ROOT/'docs/art-rebuild-20261019/approved-v5/scifi-terrain-batch-04/reservation.json').write_text(json.dumps({'topics':['c02-terrain-'+s for s in subjects],'reason':'Next uncompleted manifest-order C02 family after Farlight, Hesha and Kairon','requests':requests},indent=2))
