# Independent archer fall review

Baseline: `4c563eda`. Reviewed checkout: `empire-archer-fall-20261111/repo`. Read AGENTS.md and PROJECT_RULES.md. No production edits, external actions, or pushes made.

## Current finding

**Art continuity issue in candidate source-row.png, poses 4→5 (zero-based 3→4).** In pose 4 the left hand grips the middle of the upright bow. In poses 5 and 6 the same hand grips the right endpoint of the horizontal bow, while the full curved bow extends left underneath the body. The bow's grip/string geometry does not visibly follow the rotation of the original held object. This looks like the grip slides to the bow tip rather than the hand retaining its original middle grip. Recommend correcting the last two drawings, or depicting a clear release of the bow. This is subtle at native size, but relevant to the explicitly requested held-bow continuity review. Do not label the source flawless without resolving or consciously accepting this limitation.

## Art inspection performed

Viewed actual pixels of:
- `art/source-row.png`
- `art/extracted-v1.png`
- `art/native-size-contact-3x.png` (both mirrored directions)
- Baseline `empire-work/godot/assets/painted-units-v1/unit-archer-v2.png`
- Baseline `empire-work/godot/assets/painted-actions-v1/archer-breathe-v1.png`

The blue hood, white sleeves, leather boots/bracers, bow and quiver match the established character. No obvious body-scale pumping at the proposed uniform 0.096 scale. Buckle → knees → collapse → ground → still reads clearly. Both mirror previews retain stable ground contact. Final hand/bow edges are complete, not visibly severed. Independently measured extracted alpha bounds for all six frame regions: each has exactly four clear pixels on all four sides. Frame dimensions are 262×426, 297×396, 292×372, 364×303, 453×239, 440×226. Source canvas is 2172×724. This proves padding, not anatomical continuity or final imported Godot appearance.

## Test and scope review

No concrete test-code defect found in the current diff.

- `AnimationTorrenFallChecks.cs`: all 24 Require expressions in shared helper bodies are unchanged after stripping diagnostic strings and whitespace. Same frame/sample loops remain. Former hard-coded idle/recoil lookups now arrive as parameters; caller supplies the original Torren clips. Visibility mutation still removes the same named actor death binding for Torren. This refactor preserves original assertion strength.
- `AnimationArcherFallChecks.cs`: validates exact asset/topic, six named distinct RGBA pictures, 60/70/90/110/110/180 ms half-open spans, 620 ms total, after-clip death and 160 ms fade. Elin has no actor override and resolves the same clip instance and base appearance. Generic and Elin are each exercised west/east through shared boundaries.
- Shared boundaries assert every pose and phase edge, full opacity through 620 ms, final alpha fade without shrinking, fixed root/contact, sprite crop/anchor/facing/filter/art identity, shadow source, no action accent, retirement once at 780 ms, duplicate event publication once each and no extra delay.
- Shared scope test verifies killed flag and missing After actor do not synthesize a death animation; hpAfter zero alone remains impact-only.
- Shared result/lifetime/visibility tests retain result gating, cancelled living/dead settlement, stale callback immunity, rebind, tree exit, reduced motion, hidden-before-art-lookup behavior and explicit missing-binding rejection.
- `AnimationArcherRecoilChecks.cs`: handoff retains 75 ms release + 420 ms impact + 40 ms fatal beat. Authored fall begins only at 535 ms; full opacity at 1155, half fade at 1235, absent only at 1315. Before/After JSON remains checked.
- `AnimationDeathPolicyChecks.cs` and `AnimationReactionChecks.cs`: exceptions now include only generic c01.archer and campaign-torren. All other enumerated bindings retain static policy expectations.
- Archer idle and shot test expectation changes match the intentional new death binding. No attack preview, production behavior, battle engine, fixture, or data changes existed in the reviewed diff yet.

## Verification status and next gate

Static review complete. Art candidate review complete with the continuity issue above. Native build/test execution, final manifest/binding review, and runtime negative controls NOT run by this reviewer yet because production data was still being prepared. Runtime-looking contact sheet is a generated preview, not proof of imported native playback.

Recommended minimal negative controls in isolated exact-tree copies:
1. Set c01.archer death binding to static.death: new identity/binding assertions must fail.
2. Shorten archer.fall terminalFadeMs to 80: declaration or end-boundary assertion must fail.
3. Repeat a frame index / duplicate a frame's bytes: ordered-frame or distinct-picture assertion must fail.
4. Break named fallback for campaign-elin: same-instance/both-mirror playback assertion must fail.
5. Publish/retire authored death at 620 ms: shared boundary/result gating assertion must fail.
6. Offset one frame anchor or mirror sign in playback: CheckClipSprite/contact assertion must fail.

Final production allowlist: one archer-fall-v1 asset/PNG, one archer.fall clip, and c01.archer death binding. Reconfirm old manifest assets/clips and all other action bindings are structurally unchanged. Full required native checks and unchanged battle fixtures remain necessary before completion.

## Follow-up: candidate-v4 source review

Viewed actual `art/candidate-v4.png` (not the unreplaced source-row). The earlier source candidate was rejected for the bow grip continuity issue above. **Candidate v4 resolves that issue at source-art level:** in both terminal poses, the left elbow is bent and the palm is over the central wrapped grip; bow limbs extend to both sides and the string connects the endpoints. The right arm continues to cushion the head. Character identity, equipment, body proportions and six-pose progression remain coherent. The final bow tip is complete, although close to the source canvas right edge; extraction must retain the whole tip and add/retain required transparent padding. Low-alpha colored fringe/noise remains visible around source silhouettes and needs the same auditable extraction handling, then fresh extracted-image/contact-sheet review. Source art accepted for extraction; final anchoring, native scale and imported runtime approval still pending. Candidate v5 was not reviewed or adopted.

## Final v4 extraction/contact acceptance

**No remaining art blocker in the final v4 extraction and contact previews.** Inspected the refreshed `extracted-v1.png` and both-facing `native-size-contact-3x.png`. Central grip correction survives extraction; both bow tips and final hand outline are complete. Fall progression and settled contact are stable in both mirrors at uniform scale 0.096, with no conspicuous body-size jump or translation artifact. Terminal source anchors are [1398,604] and [1870,604].

Executed `python3 art/verify-extraction.py`: passed, six original RGBA components with no rescaling/redrawing; removed 8,986 detached pixels, maximum alpha 13/255. Independently checked source-row bytes equal candidate-v4 bytes, atlas dimensions 2111×429 RGBA, every frame has exactly 4 px alpha-clear padding on all sides, and every frameAnchor equals sourceAnchor minus sourceBounds origin.

Final extracted PNG SHA-256: `94e93bf371ab33943a3426a76ea49858273ca52a13d36b3331ba7e11e54dffb5`.
Final source SHA-256: `853a260f5d165a9e6f95110195a111d1f4aa8417237f45bef1cb420ee8b0214f`.

This supersedes the original candidate-art blocker only. Production manifest/binding integration, imported Godot runtime, required native suite and mutation checks remain separate gates, not claimed by this image review.

## Integration and explicit legacy-fixture correction

Reviewed final manifest/binding diff and the `CheckStrikeAndDeath` correction. The opening campaign already contains south-sword as c01.swordsman, immediately before south-bow (c01.archer). Selecting south-sword explicitly replaces the old incidental Last() selection for a test whose stated contract is static 220 ms death. The added during-clip/length assertion makes that intent explicit. Removing After by target ID is necessary and correct. All existing impact, fatal beat, 0.5 alpha / 0.825 scale at 110 ms, billboard-pivot and retirement assertions remain intact. No production change or authored fixture substitution is hidden here.

Read `verification/animation-initial.txt`: Torren and Archer fall dedicated checks report passing before failure at the legacy `Death uses source 220 ms fade and 35 percent shrink` assertion. This supports the specific fixture correction, but is not a full-suite pass.

Independent structured JSON comparison against 4c563eda passed: removing only the new archer.fall clip and archer-fall-v1 asset makes the manifest identical to baseline. Restoring only c01.archer/death to static.death makes all art bindings identical to baseline. Installed PNG hash, regions, anchors and frame spans match the accepted extraction exactly; death policy is after-clip, fadeMs 160. No integration blocker found. Full native rerun remains pending.
