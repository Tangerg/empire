from pathlib import Path
import json,hashlib
b=Path(__file__).parent;p=b/'idle';p.mkdir(exist_ok=True)
prompt='''Use case: style-transfer.
Asset type: C01-UNIT-TROLL, one production tactical game IDLE pose on a genuinely transparent RGBA background.

Image 1 is the exact original troll identity reference, from the fourth row of the full original character atlas. Images 2 and 3 show the approved chunky matte drawing language, not character identities. Redraw ONLY the troll from image 1, preserving his original monster face, colors, outfit, gear and stone hammer while simplifying surface texture into the broad blocky cartoon language of images 2 and 3.

Identity: a heavy broad gray-olive-skinned troll with a large stern square face, broad flat nose, heavy black brows, two upward ivory lower tusks, small dark eyes and black hair combed back and tied in a short bun behind the head. No helmet, beard or horns. Large rounded shoulders, huge thick arms, big hands, short sturdy legs and broad bare gray-olive feet with visible stubby toes. Brown padded hide tunic with tan fur collar and cuffs, a few broad stitched seams on the sleeves, a knee-length hide skirt, dark brown crossed leather chest straps with small muted-brass square buckles, dark belt with central brass square buckle, one brown pouch at the screen-right waist, and the original hanging long gray plate on the screen-left hip with an inset circular spiral and lower rectangular notch. Keep the original earthy leather/fur colors and gray-olive face.

Weapon and pose: preserve the long dark-brown wooden hammer haft wrapped in a few broad ochre grip bands and its huge rectangular dark-gray carved stone hammer head, one circular spiral inset on the broad face and a circular side inset, broad squared beveled stone corners. It is held low across the thighs by the screen-left hand, with the hammer head in front of the screen-right foot. The unarmed screen-right fist rests at his side. Slight three-quarter camera facing screen-right, both feet completely visible and planted apart, full sturdy idle silhouette. This is the original stone-headed blunt melee club, not an axe, metal mace, sword or new magical staff.

Style: emphatically chunky cartoon strategy sprite, exaggerated head, hands, toes and stone hammer, short strong proportions, thick dark outer contour, large flat matte cel-shaded planes with two or three values per material. Fur is several broad angular tufts; leather is simple broad panels with only the few identity-bearing stitch groups. Stone hammer has a handful of large cut planes. Preserve the original spiral/circular carving motifs but do not add any new glyph, emblem, badge or lettering. No realistic skin pores, leather grain, fine scratches, pebble noise, small crack web, watercolor, painterly grit, detailed fur hairs, shiny metal glints or thin highlight clutter.

Canvas and grounding: one complete troll centered on a wide landscape canvas approximately 1586×992. Leave safe transparent padding on all sides, especially under both feet. Aim for complete subject within x400..1200 and y80..910, crown-to-soles about 810 pixels, foot midpoint about x800, lowest soles about y920. Keep the entire hammer and haft visible. No cropped feet or hair. No ground, contact shadow, pedestal, scenery, extra character, text, labels, painted checkerboard, colored background or independent impact effects. Actual transparent background, alpha zero outside the silhouette, no haze or glow.
'''
(p/'prompt.txt').write_text(prompt)
refs=[str(b/'references/source-idle-cell.png'),'/workspace/shared/empire-art-rebuild/approved-v5/animation/stone-golem/idle/master.png','/workspace/shared/empire-art-rebuild/approved-v5/animation/cemetery-colossus/idle/master.png']
request={'tool':'image_gen.imagegen','mode':'built-in','transparent_background':True,'promptFile':'prompt.txt','promptSha256':hashlib.sha256(prompt.encode()).hexdigest(),'referenced_image_paths':refs,'referenceSha256':{s:hashlib.sha256(Path(s).read_bytes()).hexdigest() for s in refs},'noPostprocessing':True}
(p/'request.json').write_text(json.dumps(request,indent=2)+'\n')
print(json.dumps({'prompt':prompt,'refs':refs}))
