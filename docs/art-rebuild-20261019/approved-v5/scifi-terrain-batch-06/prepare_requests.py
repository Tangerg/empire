import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[4]
pack=ROOT/'packages/story-candidate-02/assets/final-scifi-v1'
refs=[str(pack/'masters/terrain/terrain-board-01.png'),str(pack/'masters/approved-v5/canonical-terrain/farlight-cargo-hold/master.png')]
subjects={
'soler-blue-ice':('first','Broad pale blue ice slabs with saturated cobalt bevels and deep blue chunky crevices. Preserve cold solid BLUE ice identity and simple angular interlocking slab silhouettes. Flatten to a usable overhead ground surface, no cliffs or perspective.'),
'soler-brittle-ice':('second','Pale icy cyan brittle ice plates divided by broad dark blue branching fissures, very few large flat irregular plates, restrained white surface highlights. Preserve lighter thin fractured ice identity, distinct from saturated solid blue ice. No pebbles or ice piles.'),
'soler-deep-factory':('fourth','Deep navy steel factory floor, broad dark blue square and angular plating with heavy blue-black seams, one broad inset black ventilation grille and very sparse large bolt caps. Preserve industrial deep blue factory identity, no warm lighting or yellow fittings.'),
'soler-heated-walkway':('third','Warm gray charcoal metal walkway plates with broad amber-orange inset HEATING strips in dark housings across the center and reaching the four cardinal regions. Preserve warm industrial heated-walkway identity and restrained luminous orange slits, not a lava surface. No railings, lamps, perspective scene or directional arrows.')}
requests=[]
for slug,(col,subject) in subjects.items():
 d=pack/'masters/approved-v5/canonical-terrain'/slug;d.mkdir(exist_ok=False)
 comp=('Exactly four equal square material variants in a precise 2-by-2 arrangement filling the canvas, each quadrant an independent overhead square at equal scale. No gutters or frames; whole quadrants sampled top-left, top-right, bottom-left, bottom-right.' if slug=='soler-blue-ice' else 'Exactly one square continuous material, not a sheet or variants. Broad essential subject motif remains legible in central 40 percent and four cardinal middle-quarter strips under historical NESW masks.')
 prompt=f'Use case: stylized-concept. Create ONE independently authored game terrain material master for c02-terrain-{slug}. Input image 1 is subject reference ONLY: its bottom row {col} column, ignore all other subjects. Input image 2 is STYLE ONLY: chunky matte hand-painted tactical RPG science-fiction language, bold dark contours, broad flat color planes, restrained two-step cel shading. Do not transfer its cargo motifs into this subject. Subject: {subject} Composition: strictly flat orthographic overhead terrain. {comp} Every pixel opaque; material extends to every edge. Strongly simplify for 32px logical tile readability: only a handful of broad shapes, flat fills, no tiny mottled detail. No text, symbols, labels, gridlines, blank margins, borders, tiny speckles, scratch microtexture, photographic detail, glossy 3D or scene framing.'
 req={'prompt':prompt,'referenced_image_paths':refs,'transparent_background':False}
 (d/'prompt.txt').write_text(prompt);(d/'request.json').write_text(json.dumps(req,indent=2));requests.append({'slug':slug,**req})
(Path(__file__).parent/'reservation.json').write_text(json.dumps({'topics':['c02-terrain-'+s for s in subjects],'reason':'Next four canonical manifest-order topics after Nereia. Original topic catalog lists Soler blue-ice, brittle-ice, heated-walkway, deep-factory at indices 4–7; original full board 01 bottom row columns 1–4 confirms subjects.','requests':requests},indent=2))
