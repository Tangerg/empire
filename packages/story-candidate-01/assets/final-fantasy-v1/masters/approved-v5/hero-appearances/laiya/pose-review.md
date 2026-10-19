# Laiya named hero quartet review

Named original C01-CHAR-LEIA-01:18-year-old woman, brown side braid, exposed head, gray-blue quilted/mail-edged armor, short wrap/silver clasp, brown belt/trousers/boots, left steel wrist guard, plain torn gray banner held in right hand. Original named flag has no heraldry. No generic older male face/helmet/back shield or generic white flag marks.

c01.laiya uses border-blade and gray-oath. Coordinator explicitly approved adding one modest short straight sword in complete belt scabbard absent from original named portrait. Sword remains sheathed during idle/walks, left hand draws for attack while right retains banner; empty scabbard stays at original hip. Single attack slot serves both rule weapons; independent FX01/06 not baked into art. No weapon/rule changes.

## Shared source geometry
All original canvases preserved byte-for-byte. Common1586x993: idle1586x992 needs1transparent bottom row; walk-a and selectedwalk-b1585x992 need1right column+1bottom row; selectedattack1584x993 needs2right columns. No cropping, stretching or per-pose fit.
Separate visually inspected boot ROIs, alpha>127. Each boot sole is its lowest pixel; bottom4rows meanx per boot, averaged across two boots. Source y is lowest boot sole; pole tip excluded. Whole-canvas translation at shared44/784. Raised boot remains measured for stable two-leg center, not falsely labeled planted.
Selectedwalk-b anchor834,972 versus idle879,985 needs source delta45,13, maximum2.525510204logical. Existing explicit limit4 is sufficient. Every complete1586x993 padded canvas fits104x64 at origin6,4.

## Revisions
Attack correction leaves both boot soles at959/981; upper banner shifts around12native px to right and contours have minor redraw differences. No global resize. Walk-b needed3native candidates: onlyattempt03 actually changes support. Its raised boot is89nativepx above planted sole, a clearly exaggerated but readable recovery step. No hidden claim that prior failed poses passed.

### idle
Approved named Laiya idle, exposed18-year-old female face/brown side braid, original right hand holds plain torn gray banner, left wrist guard visible, modest short sword fully sheathed at original right hip. Calm two-boot support.
Source foot[879, 985], exact midpoint[878.6116748768472, 985]; source delta[0, 0]; logical offset[0.0, 0.0]; complete canvas bounds[6.0, 4.0, 95.01020408163265, 59.72959183673469].

### walk-a
Forward stride, image-right boot in heel-strike with toe up, opposite scabbard-side boot trailing. Right hand retains banner and left guarded arm stays empty. Short sword remains fully sheathed, braid and equipment sides preserved.
Source foot[847, 983], exact midpoint[847.3210661313706, 983]; source delta[32, 2]; logical offset[1.7959183673469388, 0.11224489795918367]; complete canvas bounds[7.795918367346939, 4.112244897959184, 96.8061224489796, 59.84183673469388].

### attack
Selected attempt-02: clear left-hand compact straight-sword strike, original wrist guard on sword arm, right hand retains full plain gray banner. Empty open-mouth scabbard remains attached to original right hip; no duplicate hilt or raised scabbard. Both boots brace. Exposed blade shortened by native imagegen from about315 to195-200px; no programmatic weapon edit.
Source foot[849, 981], exact midpoint[849.4636217948718, 981]; source delta[30, 4]; logical offset[1.683673469387755, 0.22448979591836735]; complete canvas bounds[7.683673469387755, 4.224489795918367, 96.6938775510204, 59.95408163265306].

### walk-b
Selected attempt-03: opposite support is actually present. Image-left/scabbard-side boot planted low at y972, other whole boot lifted with lowest pixel y883,89source pixels higher. Face/camera/right banner hand/left wrist guard/braid/sheath side retained. It is a raised-knee recovery step rather than an exact mirrored silhouette of walk-a. Attempts01/02 retained because they kept the same incorrect supporting leg.
Source foot[834, 972], exact midpoint[834.1041718815186, 972]; source delta[45, 13]; logical offset[2.5255102040816326, 0.7295918367346939]; complete canvas bounds[8.525510204081632, 4.729591836734694, 97.53571428571428, 60.45918367346938].

## Provenance
All selected native/raw/master bytes, exact prompt hashes and reference hashes verified. All12negative-space probes are49pixels alpha0. Superseded native outputs remain immutable in per-pose attempt directories. Common-scale proof uses one shared affine scale on entire canvases, measured translations and transparent padding only; its white/black backgrounds and2x enlargement are diagnostics, never master replacements.

Complete named hero quartet ready for coordinator common-scale/native review; no formal appearance binding claimed
