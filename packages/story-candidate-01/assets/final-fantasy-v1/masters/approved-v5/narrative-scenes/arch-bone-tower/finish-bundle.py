from pathlib import Path
import hashlib,json
D=Path(__file__).parent;repo=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019');a=json.loads((D/'audited-source.json').read_text());qa=json.loads((D/'source-qa.json').read_text())
sources=['docs/story-candidates/candidate-01/deep-customized/06-war-of-seven-towers.md','docs/story-candidates/candidate-01/deep-customized/09-static-assets.md','docs/story-candidates/candidate-01/deep-novel-v2/06-war-of-seven-towers-novel.md','docs/story-candidates/candidate-01/06-war-of-seven-towers.md','packages/game-ui/src/styles/campaign.css']
(D/'source-input-hashes.json').write_text(json.dumps([{'path':s,'sha256':hashlib.sha256((repo/s).read_bytes()).hexdigest()} for s in sources],ensure_ascii=False,indent=2)+'\n')
(D/'canonical-design-context.txt').write_text('\n\n'.join('SOURCE: '+s+'\n'+(repo/s).read_text() for s in sources[:4]))
(D/'consumer-recheck.json').write_text(json.dumps({'topicId':a['topicId'],'historicalAuditActualUse':False,'currentReadOnlyScan':'rg -n --glob *.{ts,tsx,js,css} C01-ARCH-ANSEL-TOWER|C01-ARCH-BONE-TOWER|arch-ansel-tower|arch-bone-tower apps packages','matches':[],'directRuntimeConsumerFound':False,'selectorChanges':False,'newConsumerSuggested':False,'cssCoverQA':'hypothetical compatibility only, no current consumer and no browser/live claim'},indent=2)+'\n')
semantic={'topicId':a['topicId'],'label':a['label'],'category':'narrative-static','kind':'scene','originalNarrativeIndexZeroBased':69,'sourcePath':a['source']['path'],'sourceBoardSha256':a['source']['sha256'],'sourceBoardSha256MatchesAudit':True,'cellLabel':'bottom-right','legacyInsetCropBox':[632,632,1249,1249],'sourceCellSize':[617,617],'historicalAuditActualUse':False,'semanticReview':{'preserved':['Dominant round dark stone military tower with open circular battlement and command lectern','Timber projecting galleries, warm pointed windows and lantern posts','Curved left/front stone stair and raised rock base','Framed individual memorial/name tablets on wood supports with original simple finials','Anonymous armored returned soldiers and distinct hooded keepers','Cool pine mist and warm amber lamp palette'],'interpreted':['A modest public name-ledger position below the tower makes the authored name-index and individual-confirmation brief visible','Large quiet ground planes and simplified tablet groups support small-size readability','Pale bone faces on several anonymous guards make the returned military identity legible'],'notClaimed':['Named Mirelle, Oren, Laiya or Unbannered appearance','Specific faction allegiance or new heraldry','A literal chosen consent/governance outcome','Exact historically named inscriptions or a canonical map of the tower'],'caveats':['Centered narrow portrait retains main stone tower mass, full top battlement and posts, door guards and individual name-check position, but clips the outer timber gallery ends, part of the curved stair and most peripheral tablet/soldier/forest context.','Canonical256×144 retains the military tower and register arrangement; tiny bone faces and ledger marks are not individually legible at that size.']}}
(D/'semantic-reconstruction.json').write_text(json.dumps(semantic,ensure_ascii=False,indent=2)+'\n')
(D/'source-review.md').write_text('''# Bone Army Tower architectural setting source review

## Canonical identity and source mapping

C01-ARCH-BONE-TOWER, 白骨军塔, is a full opaque narrative architecture scene. It is original narrative index69, scene-board-10.png bottom-right cell, exact inset crop [632,632,1249,1249],617×617 from RGB1254×1254. Source board SHA256 matches the recovered audit. The full board and extracted cell were visually inspected before generation.

The original source is dominated by a stout circular dark-stone military tower with open round battlement/command top, warm narrow windows, timber projecting galleries, lantern supports, a curved stone staircase and raised rocky base. Around it are individually framed hooded name/memorial tablets, soldiers and dark-robed keepers, with cool pine mist behind. These distinctive forms are retained; the remake does not substitute detached cemetery props or an invented giant skull castle.

## Authored semantics

Chapter six describes nameless dead recruited into an obedient army at the tower, competing attempts to subsume individual identities, and separate questions/records for each returned person. The deep-customized static-assets brief specifically requests a name index and individual-confirmation area. The new setting therefore contains one modest name-ledger station with separate tags and a single soldier before an ordinary recorder, plus separate keepers and waiting military figures. This is a functional setting cue, not a portrait of named heroes, a depiction of the Unbannered, or a claim that any specific political/consent outcome has occurred.

Pale bone faces on a few anonymous armored guards make the returned-dead military role visible. They have ordinary dark eye sockets, no glow and no gore. Keepers remain living human role figures. Original gray/blue-gray/slate/amber color language and simple tablet finials are retained; no new faction crest, allegiance flag or magic-color system was invented.

## Generation and correction

Built-in image_gen, two attempts. Exact prompts, requests and reference hashes are saved for both. Attempt1 was preserved under rejected-v1/ because ground/masonry had dense texture flecks and the troops read as ordinary humans. The selected targeted edit keeps the architecture and layout while simplifying paving/rock planes and making some existing guards visibly skeletal. The approved result uses broad matte planes, stable dark contours, robust masonry, grouped fittings and restrained foliage. No programmatic artwork repainting occurred.

## Native and export verification

raw.png, native.png and master.png are byte-identical selected output, RGB1672×941 and effectively opaque. Exact crop [4,2,1668,938] yields source-native1664×936 at density6.5 for logical256×144. No upscaling. Entire tower-top battlement/posts and primary tower remain intact in native and exact landscape crop. QA crops/downsamples are separate artifacts.

## Visual QA and framing limits

Inspected the full native master, exact1664×936 crop, canonical256×144,960×534 desktop cover,390×778 centered narrow cover,280×245 staging inset and366×220 cover. Full landscape preserves tower, galleries, stair, scattered name tablets, military figures and forest clearing. The simplified ground separates each functional group, and the tower dominates over the memorial structures. At256×144 the military architecture and register arrangement remain clear, but tiny faces and ledger marks are not individually readable.

The centered portrait retains the full central stone tower mass, full battlement/lantern top, doorway, both pale bone-faced door guards, most of the descending stair and the individual ledger station with one soldier and living recorder. It clips outer timber gallery ends, some of the left stair curve, most peripheral tablet stations, the waiting file and the broader forest. Thus the primary military/identity-check meaning survives, while the full memorial/register ensemble requires landscape. The staging inset retains more surroundings but also crops distant wings.

All cover QA is offline hypothetical compatibility using existing narrative CSS geometries. Neither the historical audit nor a fresh read-only TS/TSX/JS/CSS scan found a direct current consumer for this exact topic. No selector or new consumer is invented. Filters, overlays and text are not simulated; no browser or live-game evidence is claimed.

## Handoff

Ready for parent review, backup and explicit opaque narrative registration. No repository, Git, runtime, selector, formal binding or unrelated topic changes were made by this worker.
''')
(D/'hash.txt').write_text(qa['sha256']+'  master.png\n')
verification={'topicId':a['topicId'],'nativePreserved':qa['rawEqualsNativeEqualsMaster'],'nativeSize':qa['nativeSize'],'effectiveAlphaExtrema':qa['effectiveAlphaExtrema'],'sourceNativeExportCrop':qa['formalIntegerCrop'],'sourceNativeExportSize':[1664,936],'logicalSize':[256,144],'density':6.5,'upscaled':False,'visualQAComplete':True,'nativeAndLogicalAndCenteredNarrowInspected':True,'rejectedAttemptsPreserved':['rejected-v1'],'currentConsumerFound':False,'runtimeOrSelectorEdits':False,'liveGameVerified':False,'masterSha256':qa['sha256']}
(D/'final-verification.json').write_text(json.dumps(verification,indent=2)+'\n')
files=[{'path':str(p.relative_to(D)),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted(D.rglob('*')) if p.is_file() and p.name!='bundle-manifest.json']
(D/'bundle-manifest.json').write_text(json.dumps({'topicId':a['topicId'],'selectedMaster':'master.png','files':files},ensure_ascii=False,indent=2)+'\n')
print(json.dumps(verification,indent=2))
