#!/usr/bin/env python3
"""Produce readable report and proposed contract; repository remains read-only."""
import hashlib,json
from pathlib import Path
O=Path(__file__).resolve().parent
R=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019')
j=json.loads((O/'manifest.json').read_text())
# Explicit filesystem scan, independent of ripgrep ignore rules.
hits=[]
for area in [R/'packages',R/'apps']:
 for p in area.rglob('*'):
  if p.suffix not in ('.ts','.tsx','.css','.js','.html') or 'src' not in p.parts or any(v in p.parts for v in ('node_modules','dist')):continue
  for number,line in enumerate(p.read_text(errors='replace').splitlines(),1):
   for t in j['topics']:
    if t['topicId'] in line:hits.append({'topicId':t['topicId'],'path':str(p.relative_to(R)),'line':number,'text':line.strip()})
(O/'literal-consumer-scan.json').write_text(json.dumps(hits,ensure_ascii=False,indent=2)+'\n')
assert not hits,hits
for t in j['topics']:
 for field in ('source','runtime'):
  p=R/t[field]['path'];assert hashlib.sha256(p.read_bytes()).hexdigest()==t[field]['sha256'],p
j['verification']['literalRuntimeReferences']=0
j['verification']['sourceAndRuntimeFilesUnchangedAtCompletion']=True
j['verification']['originalBoardsVisuallyReviewed']=['prop-board-01.png','prop-board-02.png','prop-board-03.png','prop-board-04.png','scene-board-07.png']
j['verification']['recommendedNative617pxCropsIndividuallyViewed']=j['recommendedFirstFour']
j['verification']['isolatedTestsPassed']={'test_export_overrides.py':17,'test_narrative_card_overrides.py':14,'test_narrative_scene_overrides.py':19,'total':50}
proposal={
 'status':'PROPOSED ONLY; no exporter, catalog, manifest, UI or runtime writes performed',
 'owner':'tools/art/source-overrides.json selects immutable master; canonical manifest remains sole owner of topic/kind/geometry; existing HD manifest owns optional physical density',
 'entrySchema':{'kind':'transparent-prop-v1','prop':{'source':'pack masters path to complete independent native RGBA PNG','sha256':'verified whole file SHA-256'}},
 'canonicalEligibility':{'campaign':'story-candidate-01','category':'narrative-static','kind':'key-prop','size':[128,128],'forbiddenFields':['anchor','frames','variants','frameWidth','frameHeight'],'currentSemanticHolds':['C01-CH01-S00','C01-CH03-S11']},
 'sourceRules':{'trueAlpha':'RGBA; zero-alpha exterior/open gaps; near-opaque core; existing validate_alpha outer-edge<=1 policy; visual review still required','readSource':'reuse source containment/PNG/hash validation','size':'native source must be at least the largest requested export, never upsample a128px legacy runtime into a purported new source','forbiddenTransforms':['chroma key','background removal','bbox fitting','inferred crop','alpha cleanup/replacement','recolor','painted or baked checkerboard','flattening onto a matte']},
 'canonicalExport':{'method':'reuse existing whole-authored-canvas contain, center, LANCZOS fit','size':[128,128],'logicalPadding':2,'target':'existing canonical runtime/narrative-static path','sourcePreserved':True,'alphaValidation':'existing validate_alpha before and after','anchor':'absent; do not add battle-prop center or feet anchors'},
 'optionalHd':{'default':'none; current17 have no HD records or actual consumers','supportedMinimalExtension':{'size':[256,256],'pixelDensity':2,'padding':4,'rendering':'independent from complete source, not upscaled canonical128','registration':'only if explicitly added to current v2 HD manifest and file exists in Vite graph'},'runtimeReaderChangesNeededFor2x':False,'sourceNativeDensityAbove2':'not currently allowed for key-props; requires intentional scoped change in validateCandidate01HdAsset and density tests. Do not quietly weaken all non-scene checks.'},
 'fullsceneExceptions':{'C01-CH03-S11':'hold current opaque128×128 key-prop unchanged; opaque-scene-v1 correctly rejects it. Identity/geometry change needs explicit decision. Do not create a generic fullscene-in-prop fallback.','C01-CH01-S00':'hold loose-bracer key-prop versus hand-interaction scene mismatch; no automatic reinterpretation.','existing41SceneTopics':'opaque scene masters/outputs remain opaque; people/props inside them are not reusable alpha sources'},
 'dispatchChange':'Add explicit key-prop branch in narrative-static plan dispatch; scene remains render_scene, character-card remains compose_character_card, unknown narrative kinds reject explicitly. Do not pass key-props through tactical contracts requiring anchors.',
 'provenance':['kind','whole-source source/sha256/mode/size/alpha','complete-canvas fit, origin, output size, density, padding and resampling','output alpha and pixel SHA-256','visual review required'],
 'requiredTests':[
 'All four recommended IDs export only128×128 RGBA to existing paths and preserve absent anchor/frame metadata',
 'Source bytes, canonical and HD manifests, seven character cards and all unregistered runtime files remain unchanged',
 'Full authored canvas is contained with expected padding; off-center subject remains off-center; disjoint bridle pieces and open holes survive; no bbox or crop',
 'Semi-transparent source pixel survives without a second alpha mask; empty, opaque RGB/RGBA, matte/checkerboard visual QA, edge alpha>=2 rejected; edge1 retained/reported',
 'Source smaller than requested output rejected; optional256px HD is sampled directly from source and density2 only',
 'Hash mismatch, duplicate keys/destination, wrong pack/path, unknown kind, unexpected fields, changed geometry/anchor/frames and malformed HD reject before any write',
 'CH03-S11 and CH01-S00 remain held; opaque scene entry cannot bypass canonical kind/size checks; existing character and scene tests continue passing',
 'Check mode/repeated export pixel-stable; invalid later registration prevents earlier writes',
 'Integrated catalog resolver returns canonical key-prop or explicit2x URL with unchanged128 logical geometry; this does not assert a shipped UI consumer',
 'If adding any narrative key-prop HD entry, fix the existing density test mock to select canonical scene identities instead of all runtime-hd/narrative-static paths; it currently rewrites that whole directory to scene1664×936 geometry'
 ]}
(O/'proposed-export-contract.json').write_text(json.dumps(proposal,ensure_ascii=False,indent=2)+'\n')
j['proposedExportContract']='proposed-export-contract.json'
(O/'manifest.json').write_text(json.dumps(j,ensure_ascii=False,indent=2)+'\n')
header='''# Narrative key-prop audit

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
'''
lines=[header]
for t in sorted(j['topics'],key=lambda t:t['source']['originalNarrativeIndexZeroBased']):
 b=Path(t['source']['path']).stem.replace('prop-board-','P').replace('scene-board-','S')
 lines.append(f"| {t['topicId']} | {t['source']['originalNarrativeIndexZeroBased']} | {b} {t['source']['slot']} | {t['visualReview']['riskOrHold']} |\n")
lines.append('''
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

**CH03-S11 is an opaque cemetery painting**, not an isolated name-tag source. Its scene-board07 TL panel contains people, statue, graves, paths, fence and autumn trees. Its canonical record nevertheless says key-prop128×128. Keep its current bytes/kind/size unchanged and held. `PROP-NAME-TAGS` already owns isolated markers. Do not secretly remap, chroma-key the cemetery, or weaken `opaque-scene-v1` to accept wrong kinds. If the user later chooses scene reclassification, change the authoritative canonical contract and callers/tests together; do not introduce a magic fallback.

**CH01-S00 is a separate scene-versus-object mismatch.** The source is a loose bracer; the authored brief is Roderick tying Leia's guard through hand interaction. It stays held until the intended identity is chosen. Neither LEFT-GLOVE nor a new hand scene should silently overwrite it.

The41 ordinary narrative scene topics remain intentionally opaque setting/action plates. Their people and props do not become reusable alpha assets merely because they appear in a painting. The seven independently transparent character masters and their opaque192×224 UI composites remain separate and untouched.

## Actual UI assumptions

`candidate-01-story.ts` resolves exact topic IDs generically. `campaign-game.ts:303–316` emits the same scene twice as a blurred wash and foreground backdrop; `campaign.css:180–213` sets viewport-sized images with `object-fit: cover`. That is an **opaque fullscene presentation contract**, with dark overlays. A transparent isolated prop passed into this generic story API would be enlarged/cropped as a background. The current campaign never does so for these17 topics; making them transparent requires no current UI change. A future prop presentation needs its own explicitly chosen contain/placement surface, not accidental reuse of the story-backdrop path. They are not battlefield occupancy/depth/structure consumers.

## Smallest correct exporter extension

1. Keep `source-overrides.json` as the sole selected-master owner. Add strict `{kind: transparent-prop-v1, prop: {source, sha256}}` entries for approved subjects only
2. Add an explicit narrative key-prop dispatch branch. Restrict it to existing C01 narrative-static/key-prop128×128, with no anchor/frame/variant fields. Reject unknown kinds explicitly. Keep both chapter exceptions on hold through registration policy; do not fabricate a second geometry owner
3. Reuse existing pack-contained PNG/hash reader, real-alpha validation and whole-authored-canvas fit. For canonical128×128 use2px padding; preserve open gaps and source RGBA, no crop/bbox/chroma/recolor. Source size must not be smaller than the requested physical export. Report full source geometry/alpha, exact fit, output density/alpha/hash and remaining visual QA
4. Default to no HD record because these17 have none and no consumer. If explicit2× output is wanted, register256×256 at density2, scale padding to4 and sample directly from the same native source. Existing TypeScript resolver already supports that exact2× case. No manifest synthesis or128px upscaling
5. If source-native physical output above2× is needed later, make a scoped key-prop density contract change and tests; current non-scene density validator correctly rejects it. Do not weaken every tactical/non-scene asset. Also fix `candidate-01-hd-density.test.ts`'s directory-wide mock before adding even2× narrative props: it currently turns every `runtime-hd/narrative-static/` entry into1664×936 scene geometry

Existing `validate_alpha` permits at most one8-bit alpha step on outer edges and retains it. An edge≥2 fails. This numerical test cannot alone prove native authoring, detect every checkerboard/matte, or find a subject already cut inside its canvas. Preserve complete master bytes and visually inspect native pixels, holes, light/dark composites, canonical size and any real consumer; no painting/cropping should be used to conceal defects.

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

- `manifest.json`:17 complete records, hashes, crop coordinates, exact canonical/HD/source/export state, fresh consumers, visual/semantic review and exporter failure evidence
- `original-crops/`:34 pixel-exact original reference crops only
- `proposed-export-contract.json`:bounded proposed schema, classification, ownership, HD limits and required tests
- `consumers.json`, `literal-consumer-scan.json`:fresh actual-use extraction and zero direct source references
- `build-audit.py`, `finalize-audit.py`, `collect-consumers.cjs`:reproducible shared-output-only tooling
- `*-tests.log`, `build-audit.log`:verification details
''')
(O/'README.md').write_text(''.join(lines))
print('Report and proposed contract written; direct source references:',len(hits))
