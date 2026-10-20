# C01-ARCH-SILVERWOOD — source and visual review

## Original source evidence
- Label: 银林树城 (Silverwood tree-city), original narrative index 9
- Verified source: packages/story-candidate-01/assets/final-fantasy-v1/masters/narrative/scenes/scene-board-01.png, bottom-right slot, legacy inset box [632,632,1249,1249]
- Original board SHA256: 4ccf940515763d1b92d10fc9909e8e64ff3382d995fb5b4b9b2f8777157364be
- The extracted 617×617 cell was inspected directly. Its identity is a multi-level living broadleaf tree settlement: large tree trunks, timber decks, rope bridges, ladders, hanging baskets, green-roof small central hall, forest gardens, small anonymous green-clad residents. No flag or readable writing is visible.
- The remake retains that setting; it does not borrow Redstone's tower, Three Bridges' rivers or their heraldry. The supporting references are style only.

## Consumer/chronology check
- packages/story-candidate-01/src/story.ts:250–262: brief15 is Silverwood mother-root, year3 early spring; aftermath15 is tree-city. The source is a general intact location plate. No named hero was introduced, and no new storyline, battle, flag or phase selector was created.
- The plate is also chapter3's common choices, staging and results background through candidate-01-story.ts. This makes a neutral setting preferable to newly painted suffering or post-battle damage.
- Original/source map and exact consumer lists are retained in audited-source.json. No claim that the tree-city itself is a board structure.

## Generation provenance
- Built-in image_gen; one attempt; input roles and constraints are in exact request.json/prompt.txt and reference-hashes.json
- Full authored generated source is 1672×941 RGB, intentionally opaque; native.png and master.png are byte-identical copies of raw.png
- No pixel repainting, recoloring, alpha manipulation, compositing, sharpening or content transformation was applied programmatically
- Explicit formal export crop is [4,2,1668,938], retaining 1664×936, exact integer16:9; master remains whole and unchanged
- All qa-* images are offline inspection derivatives only, never alternative source art

## Visual acceptance reviewed
- Native: three large tree clusters, multiple readable broad timber decks, connected rope walks, central green-roof hall and grounded roots/path preserve setting identity
- Style: dark confident contours, matte wood planes, simplified leaf clusters and toy-like residents match approved narrative family; no photorealistic texture, no glossy rendering, no text or invented flags
- Actual proposed 512×288 export: the tree-city and center hall read clearly; fine garden details are subordinate at this size
- Canonical256×144: viewed in the final QA step; main trunks, gold platforms, green roof and winding route remain a recognizable silhouette
- Story desktop and narrow mobile center-cover, staging inset and mobile staging: inspected from the exact proposed512×288 crop. Narrow mobile retains the central hall/platform/tree, branching rope routes and foreground path; peripheral trunks are naturally cropped by cover
- Title-mobile cover is included as a labeled stress test only. This topic is NOT a title/menu consumer
- Opaque background is intentional full-scene art. This is not a transparent sprite completion

## Limits
Offline source and crop inspection only. No browser screenshot, live gameplay acceptance, runtime exporter execution, formal binding, repository edits or Git operation was performed by this worker. Parent owns integration and final approval. No rejected attempts were produced.
