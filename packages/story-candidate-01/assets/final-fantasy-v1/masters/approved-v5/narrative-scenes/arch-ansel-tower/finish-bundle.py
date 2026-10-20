from pathlib import Path
import hashlib,json
D=Path(__file__).parent;repo=Path('/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019');a=json.loads((D/'audited-source.json').read_text());qa=json.loads((D/'source-qa.json').read_text())
sources=['docs/story-candidates/candidate-01/deep-customized/05-two-peaces.md','docs/story-candidates/candidate-01/deep-customized/09-static-assets.md','docs/story-candidates/candidate-01/deep-novel-v2/05-two-peaces-novel.md','docs/story-candidates/candidate-01/05-two-peaces.md','packages/game-ui/src/styles/campaign.css']
(D/'source-input-hashes.json').write_text(json.dumps([{'path':s,'sha256':hashlib.sha256((repo/s).read_bytes()).hexdigest()} for s in sources],ensure_ascii=False,indent=2)+'\n')
(D/'canonical-design-context.txt').write_text('\n\n'.join('SOURCE: '+s+'\n'+(repo/s).read_text() for s in sources[:4]))
(D/'consumer-recheck.json').write_text(json.dumps({'topicId':a['topicId'],'historicalAuditActualUse':False,'currentReadOnlyScan':'rg -n --glob *.{ts,tsx,js,css} C01-ARCH-ANSEL-TOWER|C01-ARCH-BONE-TOWER|arch-ansel-tower|arch-bone-tower apps packages','matches':[],'directRuntimeConsumerFound':False,'selectorChanges':False,'newConsumerSuggested':False,'cssCoverQA':'hypothetical compatibility only, no current consumer and no browser/live claim'},indent=2)+'\n')
semantic={'topicId':a['topicId'],'label':a['label'],'category':'narrative-static','kind':'scene','originalNarrativeIndexZeroBased':68,'sourcePath':a['source']['path'],'sourceBoardSha256':a['source']['sha256'],'sourceBoardSha256MatchesAudit':True,'cellLabel':'bottom-left','legacyInsetCropBox':[5,632,622,1249],'sourceCellSize':[617,617],'historicalAuditActualUse':False,'semanticReview':{'preserved':['Dark stone round/octagonal dome-topped civic tower with warm window ring','Attached low four-bay treatment arcade with deep green curtains and occupied wooden beds','Gray care canopy with original pale circle-cross mark','Low courtyard walls, guarded entrance, municipal armored shield guards','Pale-clothed caregivers and orderly anonymous adult civilians'],'interpreted':['Visible bronze branches connect tower services to treatment and clean-water basin','A guarded approach and rigid civilian queue express regulated civic order without naming heroes','Forest slopes replace unsupported distant city introduced in rejected first attempt'],'notClaimed':['Exact engineered floor plan or complete four-circuit diagram','Named hero appearance','New faction heraldry','Any particular post-battle choice outcome or successful governance reform'],'caveats':['Centered portrait crop retains the dome, core tower, one occupied bay, water/service junction and guards but clips part of the far-left lower tower facade, the gray care canopy, much of the civilian queue and most right treatment bays.','Finial is intact but close to the native top; the explicit 2px top crop is safe.']}}
(D/'semantic-reconstruction.json').write_text(json.dumps(semantic,ensure_ascii=False,indent=2)+'\n')
(D/'source-review.md').write_text('''# Ansel control-and-healing tower source review

## Source identity and authored context

C01-ARCH-ANSEL-TOWER, 安瑟尔控制治疗塔, is an opaque narrative architectural setting plate. The exact original scene-board-10.png bottom-left cell is original narrative index 68, crop [5,632,622,1249], 617×617 from RGB1254×1254. The board SHA256 matches the recovered audit. Both full board and individual cell were visually inspected before generation.

The original combines a compact dark slate dome/lantern control tower, amber upper windows, attached four-bay stone healing arcade with green curtains and beds, gray side canopy carrying a pale circled care cross, perimeter walls and unusually orderly guarded queues. It is distinct from the separate Holy Light field hospital. The remake preserves these specific forms and civic-control context rather than substituting tents or a general hospital.

Authored chapter five says tower treatment, clean water, defense and behavior control share infrastructure, with healers compelled to obey the ordered list and residents unable to object inside the boundary. The deeper static-assets brief requests visible separation of control, treatment and water. This illustration exposes substantial grouped bronze service conduits, a water basin and guarded approach as readable setting cues. It does not assert a precise circuit schematic, a settled governance outcome or a named hero likeness. Colors remain original slate, muted green, gray and warm amber; no added magic color system.

## Generation and selection

Built-in image_gen, two attempts. Exact initial prompt/request/reference hashes and the selected targeted-edit prompt/request/reference hashes are preserved. Attempt 1 was rejected because the finial was clipped at the native top, the tower was too far left for central narrow cover, and an unsupported distant skyline copied the hospital style reference. Rejected PNGs and rejection reason remain in rejected-v1/.

Attempt 2 restores the complete finial, moves the main tower toward center and replaces the skyline with forested slopes and a road. The style uses stable dark contours, broad matte stone planes, grouped fittings and quiet paving, consistent with the approved hospital/forge scenes. Medical and military/civic control cues remain simultaneously visible.

## Native preservation and export contract

raw.png, native.png and master.png are byte-identical selected tool output, RGB1672×941, effectively opaque. The explicit exact crop [4,2,1668,938] yields source-native1664×936 at density6.5 for logical256×144. No upscaling or programmatic artwork repainting occurred. QA derivatives are separate. The finial is close to the top edge but remains intact after the explicit 2px top trim.

## Visual QA

Inspected full native, exact native crop, canonical256×144 downsample, desktop960×534 cover, centered portrait390×778 cover, staging280×245 inset and366×220 cover. Landscape retains complete primary tower and four-bay arcade, circled care mark, water basin, queue, guards and walls. At256×144 the tower and extended ward remain distinguishable; individual service details are small.

Centered narrow cover retains the complete dome and finial, central/front tower faces, bronze service/water junction, a full occupied bay, part of an attended neighboring cot, guards and two waiting figures. It clips some far-left lower tower masonry, the gray canopy, most of the orderly queue, most outer treatment bays and outer walls. Thus it preserves core identity but not the entire institutional breadth. The staging inset reduces both wings. All primary architecture is intact in the full native landscape; only deliberate outer landscape vegetation extends beyond it.

These are offline hypothetical compatibility checks against existing narrative CSS cover geometries. This exact topic remains catalog-only in both historical audit and a fresh source scan; no direct current runtime consumer was found, no consumer is invented, and no selector was changed. CSS filters, text and overlays are not simulated. No browser/live-game verification is claimed.

## Handoff

Ready for parent review, backup and explicit opaque narrative source registration. This worker made no repository, Git, runtime, selector or formal binding changes. No other topic was added.
''')
(D/'hash.txt').write_text(qa['sha256']+'  master.png\n')
verification={'topicId':a['topicId'],'nativePreserved':qa['rawEqualsNativeEqualsMaster'],'nativeSize':qa['nativeSize'],'effectiveAlphaExtrema':qa['effectiveAlphaExtrema'],'sourceNativeExportCrop':qa['formalIntegerCrop'],'sourceNativeExportSize':[1664,936],'logicalSize':[256,144],'density':6.5,'upscaled':False,'visualQAComplete':True,'nativeAndLogicalAndCenteredNarrowInspected':True,'rejectedAttemptsPreserved':['rejected-v1'],'currentConsumerFound':False,'runtimeOrSelectorEdits':False,'liveGameVerified':False,'masterSha256':qa['sha256']}
(D/'final-verification.json').write_text(json.dumps(verification,indent=2)+'\n')
files=[{'path':str(p.relative_to(D)),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted(D.rglob('*')) if p.is_file() and p.name!='bundle-manifest.json']
(D/'bundle-manifest.json').write_text(json.dumps({'topicId':a['topicId'],'selectedMaster':'master.png','files':files},ensure_ascii=False,indent=2)+'\n')
print(json.dumps(verification,indent=2))
