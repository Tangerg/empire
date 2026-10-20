# C01-SCENE-NURSERY-DEBATE source review

## Selection and exact source

Selected built-in imagegen attempt 1. Raw, native and master are byte-identical RGB 1672×941. The explicit source-native crop [4, 2, 1668, 938] yields 1664×936 at density 6.5, for canonical 256×144 logical size. This is intentionally opaque complete scene art. No programmatic painting or upscaling was used; derivatives are ordinary crop/resampling only.

Canonical label: 银林育儿屋争议. Original source is scene-board-08.png, RGB 1254×1254, SHA256 dcf8beb9cd77c6f5b6884e94ae4ed18fc73c335c192bb83bfbf2b015cc107bd7. Topic is original narrative index 60, bottom-left cell 2, explicit inset crop [5, 632, 622, 1249], exactly 617×617. Centered ImageOps.fit 256×144 reconstruction matches the old runtime pixels. Neighboring cells were not used. The original cell shows a discussion on a round living-tree platform with rope rails, green canvas and golden-green foliage; it does not clearly depict the nursery's children or beds.

## Narrative interpretation and identity limits

The chapter-three final novel, deep-novel-v2/03-elder-peoples-novel.md lines 379–439, supplies the missing nursery context. Twelve beds exist, eleven are empty, and five families care for one newborn. Silverwood's despair does not justify transferring corruption onto human families; neither people may reduce the other's losses to population totals. Later shared-dream witness is voluntary, interruptible and does not immediately eliminate disagreement.

This illustration reconstructs the topic as a face-to-face dispute among unnamed adult caregivers and village representatives before that experiment. It is not a literal freeze-frame of the named Ailanwei/Leia exchange. Parent confirmed there is no approved standalone Ailanwei or Freda identity and explicitly permitted unnamed people rather than inventing named likenesses. The silver-haired elf, russet-clad human mother and other caregivers are not new canon cards or approved depictions of named heroes. Approved ch06-s29, ch04-s19 and ch07-s36e supplied only the matte chunky illustrated style.

Five adults remain awake and engaged, with serious opposing open-hand gestures. A single fully swaddled sleeping infant is attended in a branch cradle. Three visible empty cradles communicate scarcity within this limited view; they are not a retcon of the story's twelve-bed total. The human representative's ordinary closed birth register makes her own families present in the dispute. The mother is a representative reconstruction, not an asserted character from this exact visit. The living-tree locality, rope-railed platform, green canvas and warm forest remain legible. Plain leaf-shaped cloak fasteners are mundane foliage decoration, not a named faction emblem. No new faction heraldry is claimed.

There are no coercive roots or tethers, waking adults in trance, magic beams, army threats, imposed resolution or battle. The plate preserves an unresolved ethical disagreement and actual care rather than making a generic magical conflict or a smiling agreement.

## Visual QA and crop limitations

Inspected full native master, exact 512×288 and 256×144 previews, and the cover contact sheet. At 256×144, the two central opposing figures and open hands, single occupied cradle, attentive caregiver and the empty cradles remain readable. Facial nuances and book detail are clearer at 512/native. The source-native edge crop removes no essential face or newborn.

Hypothetical 960×534 desktop cover and 366×220 mobile staging cover retain the debate and attended newborn. The 280×245 staging inset keeps both debating figures and the baby but loses the left witness and almost all of the seated caregiver; treat it as context-limited. Centered 390×778 portrait cover removes the human mother's face, clips the elf's face, and excludes the baby's face and seated caregiver. Portrait composition safety FAILS and destroys the balanced debate. Do not adopt that cover mode without a separate layout/art-direction decision.

These are offline centered CSS-cover geometry simulations, not browser screenshots or live evidence. Filters, overlays and text are not simulated. No exact topic/slug static consumer was found in packages TypeScript, TSX or CSS. Asset registration alone proves no game use. The bundle is source/export-preview ready only; no repository, runtime, selector, gameplay or Git changes were made.
