# Banner guard native quartet

The accepted idle and three new poses are complete. All raw/master files preserve native built-in image-generation output byte-for-byte. Exact generating prompts and black/white visual QA are saved per pose. The rejected first idle and accepted second idle are both retained.

- Idle: both boots planted, both hands grip the pole, sword sheathed
- Walk-a: forward boot toes-up with low heel; rear boot planted flat
- Walk-b: forward boot planted flat; rear heel lifted
- Attack: drawn sword strike with separate sword and flag hands, visibly empty hip sheath and back shield retained

Identity throughout: gray-mustached veteran, ridge kettle helmet, teal/mail tunic, huge brown gloves and boots, rectangular wooden back shield, one complete gray forked banner with one pale horizontal line crossed by exactly three upright ticks. No duplicate hands, flag, sword or hilt.

## Source geometry

Shared scale is locked at44/817, using accepted idle helmet top y164 and sole y981. Suggested support anchors: idle763,981; walk-a761,972; attack670,964; walk-b787,952. Boot ROI evidence and both-sole midpoints are saved in alpha-qa.json.

Native sizes are idle1586×992, walk-a1585×992, attack1584×993, walk-b1586×992. Preserve every source pixel and pad only right/bottom to common1586×993. Required padding is at most2columns and1row, explicitly accepted by the coordinator. No resizing or cropping.

Maximum measured whole-canvas registration translation is attack+5.008568logical px horizontally, with+0.915545 vertically. The proposed104×64 frame, origin8,4, contains all full padded canvases at the shared scale. See registration-evidence.json for exact translated full-canvas and visible bounds. No runtime files were changed.

## Alpha and margins

All native perimeter pixels have alpha≤1. Zero alpha>1 silhouette pixels touch any perimeter. Finials and boots have narrow but complete margins, with no visible clipping. Native faint alpha=1 specks are preserved; no cleanup. Black/white QA shows no visible opaque background, ground, shadow or halo.
