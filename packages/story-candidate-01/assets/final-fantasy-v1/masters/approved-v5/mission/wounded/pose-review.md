# Wounded mission-unit source quartet

Four native built-in imagegen poses for C01-MISSION-WOUNDED. Order: idle, walk-a, carry-or-work, walk-b.

## Identity and consumers
idle/walk-a/carry-or-work/walk-b. Zero current actors or scene placements. Assisted low-step shuffle and supported rest, no combat or labor; do not invent new wounded actors or bind the wounded shelter to this person.
Preserve warm-tan adult man, dark-brown hair, cream forehead bandage and sling, charcoal patched jacket, muted brown patched trousers, brown boots, wooden crutch with dark blunt tip. No gore, armor or weapon.
Original row and first generated attempt contain ambiguous extra dangling arm/hand. Coordinator approved clear two-arm reconstruction. Injured near arm in sling; uninjured far hand on single crutch. Expanded exact RGB idle source crop preserves hair crossing nominal row boundary. Original poses repeat; new contacts and supported rest are authored distinctly.

## Shared geometry
Coordinator accepted idle crown y38 to supporting lowest sole y952:914source pixels, fixed44/914scale. Canonical source foot778,952. Whole-canvas export proposal104x64 at8,4. Walk-a is1586x992; other3native canvases1585x992. One transparent right-column export pad is required; no native source changes. Maximum whole-canvas registration translation2.5033logical pixels. Crutch measured separately; it cannot define crown-to-sole height. No per-pose fitting.

## idle
Tired warm-tan adult man with dark tousled hair, cream forehead bandage and arm sling, charcoal patched jacket, brown patched trousers and enormous brown boots. Exactly two arms: near elbow bends into sling, one hand at sling right opening; far hand grips the single screen-right crutch. Both boots planted.
Native size[1585, 992]; measured foot[778.0, 952]; whole-canvas logical translation[0.0, 0.0].

## walk-a
Cautious supported short-step phase: screen-right boot forward, toe gently raised with heel low; screen-left boot flat behind. Crutch angles gently forward with tip low to ground. Injured arm remains in sling; no running or large arm motion.
Native size[1586, 992]; measured foot[726.0, 941]; whole-canvas logical translation[2.5032822757111597, 0.5295404814004376].

## carry-or-work
Noncombat supported recuperative rest: both boots planted, knees slightly bent, shoulders rounded toward crutch, head bowed with downcast eyes. Same two hands and crutch; injured arm remains immobilized. No work, attack, healing procedure or new objects.
Native size[1585, 992]; measured foot[776.0, 934]; whole-canvas logical translation[0.0962800875273523, 0.8665207877461707].

## walk-b
Opposite weight-bearing contact: screen-right boot flat and planted, screen-left boot trails with heel raised and toe low. Crutch nearly upright bears weight. Same camera, aids and identity, not mirrored. Generation kept left leg trailing rather than prompt-requested left forward; observed output is a coherent complementary shuffle phase, not a claim of exact requested foot leading.
Native size[1585, 992]; measured foot[788.0, 931]; whole-canvas logical translation[-0.4814004376367615, 1.0109409190371992].

## Provenance and QA
All selected/rejected raw/master files are byte-identical to built-in native output. Exact prompts, request JSON, prompt/request/reference SHA256s, native source paths and dimensions retained. Original board/row/expanded idle pixel verification passed. Five generations, four selected, one rejected-extra-arm attempt retained.
Full-native black/white/scene and registered44logical comparison inspected. Exactly two arms/hands in all selected images; sling and crutch never switch sides. No modern equipment or gore added. All native perimeters alpha<=1 and no alpha>1 pixels outside7px-expanded opaque silhouettes. No source alpha edits or pixels painted.
Coordinator owns runtime export, repository integration, tests and Git. No repository writes performed.
