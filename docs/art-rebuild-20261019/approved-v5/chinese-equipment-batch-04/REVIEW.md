# C03 equipment batch 04

Four complete, independently generated built-in imagegen source candidates. This worker only wrote the four new approved-v5 equipment master directories and this batch evidence directory. Shared source registry, canonical/package manifests, runtime, progress, implementation and Git were not changed.

## Scope and canonical identity

The next four canonical manifest identities after c03-equip-3f6fea2f are:

- c03-equip-4377f3e4 筑城守备: open toolbox, broad mason's trowel, two masonry blocks.
- c03-equip-4566788b 军医: medicine box, broad bandage roll, corked ceramic jar.
- c03-equip-4619e546 宁朝禁军: indigo-backed iron cuirass and spear, restrained military-guard vocabulary.
- c03-equip-4c02a584 刀盾卒: curved dao and round wooden shield.

All four lacked an approved-v5 stage and source directory at reservation. The medic, imperial guard and sword-and-shield infantry already had older remake-20261019 registry entries; these do not count as approved-v5 completion and were left unchanged. Candidate selection used canonical manifest identities and approved-v5 progress, not the legacy registry count. The total parent progress snapshot at selection was 629, but this worker does not increment it.

Canonical-topic-manifest, package manifest, 07-topic-catalog, 02-asset-scope and ART-DIRECTION were checked. Scope classifies fortification garrison as engineering, medic as support and both infantry roles as infantry. ART-DIRECTION explicitly requires a medicine box for medic and indigo/restrained gold for Ning army. The objects are role-based game visual interpretations, not new mechanics or claims of exact historical loadouts.

The full original equipment atlas and approved broad-matte token source were visually inspected. They informed the textual design prompts only, and were not passed as tool reference images because these were new generations rather than edits. Exact reference hashes and this distinction are recorded in each source-evidence.json.

### Legacy mismatch audit

All four are explicit canonical-role reconstructions rather than exact legacy-slot recovery:

- Fortification garrison's current unremade legacy runtime shows grain sacks and bundled crops, not construction equipment.
- Medic's preserved original-evidence.png shows a siege tower. The older remake runtime already shows bandages and a jar, but lacks the new medicine-box emphasis.
- Imperial guard's preserved original-evidence.png shows a straw-hatted peasant with a fork. The older remake runtime has armor and a weapon.
- Sword-and-shield infantry's preserved original-evidence.png shows a lone straight sword. The older remake runtime already pairs a round shield and curved weapon.

Original evidence and current pre-integration runtime were separately copied and hashed. See legacy-native-enlarged-review.png, each -legacy-native.png, each -before-runtime.png, and per-source evidence. No legacy source was overwritten.

## Visual review

Reviewed all complete native generated sources, the complete white/black/olive source composites, and actual 48x48 white/black/olive exports. The four groups are distinguishable at native size: construction tools/blocks, bright bandage and medicine box, dark-blue armored cuirass, and round shield with red grip. Complete outer contours remain within the canvas. No opaque backing plate, ground, cast shadow, checkerboard or white halo was observed. Contours are heavy and the surfaces use broad matte planes, without granular mosaics or metal microfaceting.

Limitations are retained and disclosed, not cleaned:

- Builder stone and iron have faint low-contrast surface variation in the native source. The toolbox has more than two broad facets per material. These do not form visible grain or mottled clusters at native48.
- Medic box has several sparse elongated wood marks and added brass corner fittings; bandage has more fold planes than requested. Its large roll dominates the front, but the box handle and green jar remain readable.
- Guard armor uses four paired plate bands and large shoulder plates rather than a historically exact lamellar construction. Added gold trim is more extensive than the requested simple collar edge, and broad bevel highlights remain. It reads as a stylized fantasy guard kit; the specific Ning identity still depends on palette and canonical label. No exact historical authenticity is claimed.
- Shield has eight large plain rim rivets, and its boss has a single broad highlight. Neither was requested; both remain simple and readable. The dao and shield vocabulary, rather than a unique historical pattern, establishes the role.
- The two weapon tips have narrower source margins than the requested 8 percent, but remain fully contained and pass unchanged source and native-edge checks.

No bounded corrections were needed or attempted. Parent review may accept or hold these candidates; production integration and live-game acceptance are not claimed.

## Integrity and unchanged export contract

Each new master directory contains raw.png, master.png, exact prompt.txt, source-evidence.json and alpha-qa.json. All four are byte-identical raw/master 1254x1254 RGBA PNGs, copied directly from separate built-in imagegen calls. No source crop, repaint, recolor, resampling, alpha removal, cleaning, binarization or surgery was performed.

The existing exporter contract is unchanged: native 48x48, anchor [24,24], padding 2, full authored canvas contained to 44x44 at [2,2], LANCZOS. No HD substitution or tight-alpha crop.

Source perimeter alpha1 residue was preserved under the existing edgeMaxAlpha <=1 guard:

- Fortification garrison: 35 perimeter pixels at alpha1.
- Medic: 62 perimeter pixels at alpha1.
- Imperial guard: 10 perimeter pixels at alpha1.
- Sword-and-shield infantry: 3 perimeter pixels at alpha1.

Every native48 perimeter is exactly alpha0. The existing export_overrides.plan passes all four in isolation, and each saved native preview is pixel-identical to its planned output. A separate temporary isolated root containing unchanged canonical manifests and these exact master bytes successfully ran export and export(check=True) for all four. This did not write production runtime. See isolated-export-check.json and export-plan-qa.json.

## Parent handoff

- integration-candidates.json: isolated source registry fragment, ready for parent review.
- delivery-manifest.json: canonical identities, source paths, hashes and evidence paths.
- batch-alpha-integrity-qa.json: exact byte-integrity and alpha checks.
- native-review-white-black-olive.png: true 48px review, not enlarged.
- full-source-review.png: full-resolution complete sources on olive.
- full-source-review-white-black-olive.png: uncropped reduced source contact sheet.
- Each -source-white-black-olive.png: complete source at native resolution on three backgrounds.
- generation-spec.json and persist.py: exact prompts and one-time pre-registration persistence/isolated-verification procedure.

The parent alone owns acceptance, registration, formal runtime export/check, progress updates and final integration tests.

## Integration-owner acceptance

Independently inspected the complete source contact sheet and true native48 white/black/olive review. Four roles read distinctly, with broad matte silhouettes; minor material highlights/wood marks are accepted rather than removed. Sources selected and formal-export/check verified with existing native geometry and guards unchanged. Canonical-role reconstruction is explicit because legacy atlas cells do not consistently represent their labels. This is source/catalog acceptance, not C03 live-scene acceptance.
