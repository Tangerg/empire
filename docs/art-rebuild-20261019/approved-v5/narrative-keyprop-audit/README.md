# Narrative key-prop audit

Read-only repository audit, 2026-10-20. Outputs in this directory only. No generated/painted artwork, repository edits, Git commands, source cleanup or runtime replacement.

## Decision

Start with **LEFT-GLOVE, EXILE-HARDTACK, IVRA-BROKEN-BRIDLE, CROWN-FRAGMENT**, each as an independent genuine-alpha reusable prop. The four original617×617 cells have been individually inspected; identities and complete silhouettes are unambiguous. They are visual references only: the original boards are RGB with painted magenta, and their old128px exports are chroma-derived. Do not promote those old pixels into new reusable sources.

The minimal implementation is a bounded `transparent-prop-v1` path in the existing registered-source exporter. Preserve current canonical128×128, absent anchors/frames and all unrelated scene/card contracts. Do not add a second source catalog or reclassify props as tactical battle-props. The machine-readable proposal is in `proposed-export-contract.json`.

## Verified scope and current state

- **17 pending:**15 `C01-PROP-*` plus key-prop-kind `C01-CH01-S00` and `C01-CH03-S11`
- All17 are canonical128×128, `narrative-static`, kind `key-prop`; no anchor, frames, variants, pivot/state atlas, registered source override or HD record
- **No shipped consumers:** fresh extraction of35 story screens,11 choices,16 levels, portrait/chapter bindings and an explicit source-file scan found none. Generic `candidate01AssetUrl`, `candidate01StoryArt` and icon API reachability are not an actual consumer
- **Zero true-alpha original masters:** four1254×1254 RGB prop boards use magenta; one RGB scene board provides CH03-S11. Sixteen old runtime outputs contain alpha, while CH03-S11 is fully opaque
- All17 current outputs were independently reconstructed from original board/cached legacy alpha and compared as decoded pixels: **17/17 exact matches**. Every original board and runtime hash remained unchanged
- At audit snapshot371 total topics were formal, narrative21/80. All17 audited key-props remained pending. The ongoing parent work can advance unrelated totals
- Historical `campaign-props-sheet-02-hd.png` and `manifest-hd.json` are absent. The617px original cells are the highest verified available original references, not invented HD files

## Sourceboard coordinates

Source order comes from the authored80-topic catalog plus AST-read `NARRATIVE_SOURCES`, never alphabetic manifest order. Slots are row-major TL/TR/BL/BR. Coordinates are original pixels, right/bottom exclusive.

| Slot | Complete627×627 cell | Legacy617×617 inset |
|---|---|---|
| TL | [0,0,627,627] | [5,5,622,622] |
| TR | [627,0,1254,627] | [632,5,1249,622] |
| BL | [0,627,627,1254] | [5,632,622,1249] |
| BR | [627,627,1254,1254] | [632,632,1249,1249] |

`original-crops/<topic>/full-cell.png` and `legacy-inset.png` preserve original RGB samples without resampling, cleaning, alpha manipulation or paint. **34 crops** were written and decoded-pixel checked against their source region. Complete cells retain board gutters; neither crop is a final reusable master. Original board paths/hashes, crop hashes, native dimensions and exact slots are in `manifest.json`.

## All pending topics

Every row below shares128×128 / no anchor / no HD / no actual consumer. P01–P04 mean prop-board01–04; S07 means scene-board07.

| Topic | Original index (zero-based) | Source/slot | Classification and pre-generation concern |
|---|---:|---|---|
| C01-PROP-FIRST-GREY-FLAG | 14 | P01 TL | HOLD source-cutoff: full pole end is unavailable. Do not copy the flat cut into an allegedly complete transparent master. |
| C01-PROP-FREE-OATH-STONE | 15 | P01 TR | Identity scope hold for a narrative-accurate final-ending asset. Preserve this specific singular canonical object until intentionally redesigned. |
| C01-PROP-CROWN-FRAGMENT | 16 | P01 BL | Clear original silhouette; complete subject, no text or symbol transcription needed. |
| C01-PROP-DRAGON-ALLIANCE-CLASP | 17 | P01 BR | Clear source silhouette; confirm cloth versus wing-material interpretation before adding new lore. |
| C01-PROP-LEFT-GLOVE | 18 | P02 TL | Recommended; do not substitute CH01-S00's distinct metal-rimmed bracer or Roderick's literal left glove. |
| C01-PROP-EXILE-HARDTACK | 19 | P02 TR | Recommended; complete subject and simple countable identity. |
| C01-PROP-CONTROL-FRAGMENT | 20 | P02 BL | Source identity clear but symbol semantics require a brief before generation; do not hallucinate readable spell text. |
| C01-PROP-THREE-PEOPLES-CLASP | 21 | P02 BR | Clear complete original; avoid turning it into a crown, single empire insignia or welded unbreakable ring. |
| C01-CH01-S00 | 44 | P04 BR | HOLD scene-versus-prop identity. Preserve current key-prop metadata and bytes; do not silently use the bracer as the planned scene or remap to LEFT-GLOVE. |
| C01-CH03-S11 | 54 | S07 TL | Evidence-backed classification correction: change canonical kind to scene while preserving opaque128×128, exact topic/path and pixels; never chroma-key or redesign this cemetery. |
| C01-PROP-ADA-MEDICINE | 73 | P03 TR | HOLD semantic mismatch: do not call two bottles dual-language merely because their colors differ. Exact fictional text is not specified; two visibly distinct abstract writing systems may be proposed for review. |
| C01-PROP-IVRA-BROKEN-BRIDLE | 74 | P03 BL | Recommended; complete multi-part object and clear story semantics. |
| C01-PROP-EXPIRING-MANDATE | 75 | P03 BR | HOLD temporal mechanism interpretation if matching novel; original release pin and detachable construction remain mandatory. |
| C01-PROP-NAME-TAGS | 76 | P03 TL | Semantic brief required for labels and living/dead pairing; preserve multiple individual markers, not one generic medallion. |
| C01-PROP-CASUALTY-REGISTER | 77 | P04 TL | Complete source, but dense marks fail at128px. Historical output has only27 fully opaque pixels, so it is not a transparency quality reference. |
| C01-PROP-GRANARY-KEY | 78 | P04 TR | HOLD semantic mismatch. Do not claim three-party governance from three decorative rings without deciding whether to preserve source or redesign. |
| C01-PROP-EVACUATION-MAP | 79 | P04 BL | Symbol semantics and route topology require review; preserve arrows, river crossings and differentiated routes, not random decorative glyphs. Historical output has only4 fully opaque pixels. |

## First four, ready for a new-art brief

1. **C01-PROP-LEFT-GLOVE**, P02 TL: worn blue-gray cylindrical wrist guard with rope tie, tan repairs and a dark hollow opening. The stable English ID is not permission to draw a fingered glove. Do not substitute CH01-S00's separately mapped bracer
2. **C01-PROP-EXILE-HARDTACK**, P02 TR: three plain rough tan biscuits bundled in gray cloth. Preserve scarcity and simple food; no decorative pastries or heraldry
3. **C01-PROP-IVRA-BROKEN-BRIDLE**, P03 BL: broken brown buckled strap, separate torn red-brown strap and two separated metal bit/ring halves. Keep every loose piece and open gap; do not reassemble a restraint or attach it to a dragon
4. **C01-PROP-CROWN-FRAGMENT**, P01 BL: incomplete curved bronze/gold crown band, angular bevels and diamond boss. Clearly broken, dignified/practical rather than villainous spikes; no invented insignia

The references are original detailed-style identity evidence. They are not authorization to copy the superseded rendering style. New sources should follow the already approved chunky style while preserving these identities.

## Semantic and cutoff risks found before generation

- **FIRST-GREY-FLAG:** the pole visibly runs out of the panel at its bottom. Legacy bbox-fitting turns this cut source into an apparently padded128px export, so passing edge-alpha checks on that export cannot prove the original subject is complete. The story also requires an unremoved bright blue thread, which is not clear in the old cell. Hold until a complete-pole interpretation is explicitly briefed
- **ADA-MEDICINE:** old art contains two bottles; the story describes a cracked bottle with two retained national scripts/labels. Two differently colored bottles do not satisfy the two-script meaning. No literal fictional label copy is supplied
- **GRANARY-KEY:** old art shows one key with three colored large rings. The novel explicitly gives three independently held keys to merchant, militia and refugee representatives and requires two holders present. Decide source preservation versus narrative redesign before drawing
- **EXPIRING-MANDATE:** a dial-like clasp plus detachable pin is clearly shown; the novel adds falling silver sand. Detachment and Tasha's external removal are essential. Do not casually replace the device with an ordinary clock
- **FREE-OATH-STONE:** keep singular canonical identity separate from planned plural FREE-OATH-STONES. The source is a paired standing-stone/ring object; the ending describes seven differently shaped decentralized stones that cannot become one crown. No new plural ID or count is authorized by this audit
- **CONTROL-FRAGMENT, NAME-TAGS, CASUALTY-REGISTER, EVACUATION-MAP:** glyphs/marks carry meaning, but no approved literal writing system, name list, or map labels are supplied. Do not hallucinate readable canon text. Control fragment lacks refusal structure; name tags distinguish living/dead individuals; ledger records individual casualties; evacuation map needs understandable separate routes and river crossings. Sheet02 expressly forbids readable text and existing heraldry
- **Legacy transparency is not the quality target:** ledger output has only27 alpha255 pixels, evacuation map only4. Most visible samples are partially transparent after historical matting. Use genuine native RGBA masters, check opaque object cores and open gaps on contrasting backgrounds, and preserve all source alpha rather than recreating the old cleanup

## Keep full-scene exceptions explicit

**CH03-S11 is an opaque cemetery painting**, not an isolated name-tag source. Its scene-board07 TL panel contains people, statue, graves, paths, fence and autumn trees. Its canonical record nevertheless says key-prop128×128. Both original source and authored lore unequivocally identify a scene: deep-customized09 section3 (lines65–81) lists CH03-S11 under scenes, and deep-customized03 lines7–25 separately names the cemetery scene and PROP-NAME-TAGS prop. **Propose an explicit canonical kind correction from key-prop to scene while retaining128×128, exact topic/asset/path/label, current opaque pixels, absent anchors/frames and pending remake status.** This is a documented classification correction, not semantic redesign or a new topic. No change was made in this audit. `PROP-NAME-TAGS` already owns isolated markers. Do not secretly remap, chroma-key the cemetery, recrop it to16:9, or invent a fullscene-in-prop fallback. Extend the existing opaque-scene renderer coherently to the declared square scene geometry as described below.

**CH01-S00 is a separate scene-versus-object mismatch.** The source is a loose bracer; the authored brief is Roderick tying Leia's guard through hand interaction. It stays held until the intended identity is chosen. Neither LEFT-GLOVE nor a new hand scene should silently overwrite it.

The41 ordinary narrative scene topics remain intentionally opaque setting/action plates. Their people and props do not become reusable alpha assets merely because they appear in a painting. The seven independently transparent character masters and their opaque192×224 UI composites remain separate and untouched.

## Actual UI assumptions

`candidate-01-story.ts` resolves exact topic IDs generically. `campaign-game.ts:303–316` emits the same scene twice as a blurred wash and foreground backdrop; `campaign.css:180–213` sets viewport-sized images with `object-fit: cover`. That is an **opaque fullscene presentation contract**, with dark overlays. A transparent isolated prop passed into this generic story API would be enlarged/cropped as a background. The current campaign never does so for these17 topics; making them transparent requires no current UI change. A future prop presentation needs its own explicitly chosen contain/placement surface, not accidental reuse of the story-backdrop path. They are not battlefield occupancy/depth/structure consumers.

## Smallest correct exporter extension

1. Keep `source-overrides.json` as the sole selected-master owner. Add strict `{kind: transparent-prop-v1, prop: {source, sha256}}` entries for approved subjects only
2. Add an explicit narrative key-prop dispatch branch. Restrict it to existing C01 narrative-static/key-prop128×128, with no anchor/frame/variant fields. Reject unknown kinds explicitly. Keep CH01-S00 on hold and exclude CH03-S11 from transparent registration while its documented scene classification is corrected; do not fabricate a second geometry owner
3. Reuse existing pack-contained PNG/hash reader, real-alpha validation and whole-authored-canvas fit. For canonical128×128 use2px padding; preserve open gaps and source RGBA, no crop/bbox/chroma/recolor. Source size must not be smaller than the requested physical export. Report full source geometry/alpha, exact fit, output density/alpha/hash and remaining visual QA
4. Default to no HD record because these17 have none and no consumer. If explicit2× output is wanted, register256×256 at density2, scale padding to4 and sample directly from the same native source. Existing TypeScript resolver already supports that exact2× case. No manifest synthesis or128px upscaling
5. If source-native physical output above2× is needed later, make a scoped key-prop density contract change and tests; current non-scene density validator correctly rejects it. Do not weaken every tactical/non-scene asset. Also fix `candidate-01-hd-density.test.ts`'s directory-wide mock before adding even2× narrative props: it currently turns every `runtime-hd/narrative-static/` entry into1664×936 scene geometry

Existing `validate_alpha` permits at most one8-bit alpha step on outer edges and retains it. An edge≥2 fails. This numerical test cannot alone prove native authoring, detect every checkerboard/matte, or find a subject already cut inside its canvas. Preserve complete master bytes and visually inspect native pixels, holes, light/dark composites, canonical size and any real consumer; no painting/cropping should be used to conceal defects.


### Documented square-scene correction for CH03-S11

Current original+lore evidence is sufficient to recommend correcting its canonical kind to `scene`, with **128×128 preserved**. No generation or semantic redesign is needed to establish that classification. Apply it as a separate coherent classification change when authorized, update the derived inventory/transparency description, and keep the existing runtime image unchanged until a reviewed remake exists. It still counts as one pending canonical topic.

Reuse `opaque-scene-v1` rather than adding a duplicate exporter. Let the existing `render_scene` accept the current256×144 shape and the documented128×128 square scene shape, keeping all unframed/no-anchor/opaque-master checks. Derive exact crop aspect by `(right-left)*asset.height == (bottom-top)*asset.width` and source minimum size from canonical dimensions and density. `sourceCropBox:null` on a square native master means the whole source; a reviewed crop, if needed, must be square. Do not stretch or automatically16:9-crop it.

No HD registration or resolver change is required merely to correct the canonical kind. Before a future square-scene HD registration, update `validateCandidate01HdAsset`'s scene geometry guard and integrated scene tests to accept the square logical shape and exact physical ratio. Keep existing wide-scene sizes/ratios unchanged. The current directory-wide density-test mock also needs correction because it assumes all narrative physical images are1664×936. Proposed tests cover byte-preserved classification, correct square source/crop handling, wrong aspect/alpha rejection, unchanged wide scenes and no key-prop bypass.

## Verification and proposed regression coverage

Ran existing read-only/temporary-fixture Python suites with bytecode writing disabled:

- `test_export_overrides.py`:17 passed
- `test_narrative_card_overrides.py`:14 passed
- `test_narrative_scene_overrides.py`:19 passed
- **50 total passed**, logs retained. The reporting shell's final `tail -5` invocation was invalid; each test process had already completed successfully, independently confirmed from all three logs

A read-only `plan()` probe rejects all17 current key-prop registrations through the character-card validator: `only existing Gray Flag 192x224 character-card contracts supported`. This is the concrete missing branch. No existing test currently proves genuine-alpha narrative key-prop export.

Required new tests are enumerated in `proposed-export-contract.json`: exact128 logical geometry; no added anchor/frames; whole-canvas and detached-piece preservation; semi-alpha without double masking; source/hash/path/schema/geometry/error cases before writes; no upsampling; optional2× direct source sampling; existing scene/card rejection boundaries; unregistered assets/manifests/master bytes untouched; reproducible `--check`; and real catalog URL selection without pretending there is a shipped UI consumer.

No new exporter was implemented, no full suite/typecheck/build was run, and no browser/live-game visual acceptance is claimed. This is the requested source/contract audit, not formal asset completion.

## Deliverables

- `manifest.json`:17 complete records, hashes, crop coordinates, exact canonical/HD/source/export state, fresh consumers, visual/semantic review, exact local lore source references and exporter failure evidence
- `original-crops/`:34 pixel-exact original reference crops only
- `proposed-export-contract.json`:bounded proposed schema, classification, ownership, HD limits and required tests
- `consumers.json`, `literal-consumer-scan.json`:fresh actual-use extraction and zero direct source references
- `build-audit.py`, `finalize-audit.py`, `classification-addendum.py`, `collect-consumers.cjs`:reproducible shared-output-only tooling. Run collect, build, finalize, then classification addendum in that order
- `*-tests.log`, `build-audit.log`:verification details
