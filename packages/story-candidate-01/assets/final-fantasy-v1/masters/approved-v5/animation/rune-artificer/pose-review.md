# Rune-artificer four-pose review

C01-UNIT-RUNE-ARTIFICER binds only c01.rune-artificer, with two c01-14 enemy actors, rune-rebel-2[15,7] and south-artificer[15,12]. Original idle/walk-a/attack/walk-b,32x48 frame,anchor[16,47],footprint1x1 remain the semantic contract. Short left-hand hammer matches blunt melee rune-hammer; right-hand open arched rune-tool supports range2 magic forge-burst. Both use the existing single attack slot, here a clear raised left-hand hammer strike/windup while retaining right-hand rune-tool. This shared-action limitation is documented, not silently fixed. Separate FX-04/07 remain outside character. Legacy near-idle attack and weak same-phase walks are reconstructed as meaningful action/opposite contacts; no rules changes or fifth frame.

## Identity and symbols
Young-adult tan human craftsperson, clean-shaven, large dark eyes, black thick side braid at screen-left and loose side curl. Dark low cylindrical work cap with brown band/rivets and single brass-rimmed flipped-up lavender-gray magnifying lens at screen-right. Slate armor with rectangular four-rivet left shoulder plate, gray collar, rust/brick-red apron, charcoal skirt, brown gloves/belt/boots. Short one-handed rectangular steel hammer in screen-left hand; open arched brass rune-tool with lower crossbar and three blue leaf-shaped pendants in screen-right hand. Silver chisel and orange-black angular inset belt plaque.
Hat lens is filled lavender-gray glass; right-hand brass rune-tool center is a genuinely open transparent aperture, with three solid blue leaf pendants. Original small hammer angular-loop carving is simplified to the accepted angular diamond inset. Original orange-black belt plaque remains a separate geometric mark. No invented sacred meaning, faction symbol, rune alphabet or external glow.

## Geometry
All four selected native canvases are exactly1586x992; no transparent padding or source resizing required.
Maximum source foot shift occurs in walk-a: idle[802,971] minus walk-a[741,956]=[61,15], giving[2.936542670,0.722100656]logical at44/914. Existing limit4 suffices; all complete canvases fit104x64 at origin[8,4]. No independent pose fit.
Shared44/914; canonical sourcefoot[802,971].

### idle
Original cap/lens, tan clean-shaven face, black side braid, slate armor, rust apron, left-hand hammer and right-hand open arched rune-tool retained. Compact hammer face inset simplified to angular diamond, original orange/black belt plaque retained as broad geometric inset. No external FX. Native master and white QA inspected.
Sourcefoot [802, 971]; exact sole midpoint [801.5345453111445, 969.0]; whole-canvas offset [0.0, 0.0]; source visible bounds [373, 44, 1228, 972]; full logical canvas [8.0, 4.0, 84.350109, 51.754923].

### walk-a
Walk-a: original screen-right leg leads to right, screen-left leg trails with bent heel; same left lowered hammer/right arched rune-tool and three blue pendants. Same lens/head proportions. Native and white QA viewed; no baked FX.
Sourcefoot [741, 956]; exact sole midpoint [740.6691684556554, 942.5]; whole-canvas offset [2.93654267, 0.722100656]; source visible bounds [355, 28, 1240, 957]; full logical canvas [10.936543, 4.722101, 87.286652, 52.477024].

### attack
Unmistakable original screen-left hammer raised over shoulder; compact steel head retained, one handle cleanly gripped by the left fist. Screen-right hand retains open arched rune-tool with three blue pendants. Two arms/hands, wide braced feet, no baked FX. Head and lens retain approved dimensions despite bent combat pose. Native and white QA viewed.
Sourcefoot [781, 966]; exact sole midpoint [781.1602466202839, 959.5]; whole-canvas offset [1.010940919, 0.240700219]; source visible bounds [346, 20, 1302, 967]; full logical canvas [9.010941, 4.2407, 85.36105, 51.995624].

### walk-b
Selected attempt03 rebuilt from approved idle alone. Left boot now planted flat; right boot distinctly raised in recovery, with visible angled sole. This opposes walk-a right-down/left-raised contacts. Head/cap/lens and original left hammer/right rune-tool retained. Three blue pendants and belt plaque intact; real alpha arch. Native and white QA viewed. Attempts01/02 repeated walk-a contacts and are rejected/preserved.
Sourcefoot [791, 974]; exact sole midpoint [790.9786458333333, 954.0]; whole-canvas offset [0.529540481, -0.144420131]; source visible bounds [374, 45, 1230, 975]; full logical canvas [8.52954, 3.85558, 84.87965, 51.610503].

## Provenance and QA
Idle,walk-a,attack use first successful native output; walk-b uses corrected attempt03. Attempts01/02 repeated same foot phase and are retained rejected, not exported. Exact prompts and immutable native source files for every attempt retained. All raw/master/native hashes and prompt hashes match. Complete1672x941 original board, unresampled idle/face/gear crops and original HD/runtime strips preserved with hashes. Native and white QA views inspected; black/white composites saved. Each arch aperture has a7x7 all-alpha-zero probe. No repository/runtime writes.
