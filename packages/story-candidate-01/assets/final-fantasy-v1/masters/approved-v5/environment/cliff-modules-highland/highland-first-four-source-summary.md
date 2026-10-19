# Highland original slots 0-3: native source handoff

Four complete 1254 x 1254 RGBA sources are saved with exact prompts, raw/master copies, provenance, alpha QA, independently estimated source feet and physical contact landmarks. All raw/master/generator hashes match. No source pixels were modified. Perimeter alpha is at most 1; slots 2/3 have entirely zero-alpha outer edges. The diagnostic contact sheet uses the same full-canvas display scale for every source, without fitting individual silhouettes.

## Identity and registration
The original and current atlas manifest contain `cells: []`; the semantic catalog contains no highland entries. These `highland-*` names are working file aliases only. Original indices and cellOrder labels are preserved. No per-cell footprint, obstruction, passability or heightDelta is invented. Atlas-level contract: 4 x 4; 128 logical / 256 HD; anchor (64,127); bottom-anchor module sorting. Runtime adapter, final scale and any semantic binding remain parent integration work.

Actual usage audit: no draws in the 16-level snapshot at f8789f8. The current scene source match is c01-01's deny list. Therefore all four are catalog-only. There are no new scene placements.

## Shape distinctions
- Slot 0 straight-south: a long continuous wall, gray rock-cap strip, upper-left to lower-right axis; projected foot (627,1070)
- Slot 1 end-west: a compact oblong terminus, lower-left to upper-right cap axis, raised rear-right; projected foot (627,1118)
- Slot 2 end-east: original pixels actually show a compact deep three-sided inward wall. This approved original shape is preserved, with open transparent foreground. Token/index are not remapped; projected foot (627,990)
- Slot 3 concave-corner: a wider, shallower, flared inward wall, distinct from slot 2; projected foot (627,956)

The open-corner feet intentionally lie in transparent foreground and are separate from the rear inner-wall footing. Physical landmarks are in each evidence file. Source feet are visual estimates, never alpha-box bottoms.

## Source crop evidence
Full 1254 original keyed/alpha boards and original HD slots were inspected. All 16 complete-subject references and original geometry observations are available in the reference folder. Slot 3 crosses its nominal fractional quarter by 0.5 px on the left, recovered in the complete-subject reference. Diagnostic original crop coordinates are not new registration coordinates.

## QA limits
Strong-silhouette margins (left, top, right, bottom): slot 0 [40,177,38,179]; slot 1 [49,163,25,126]; slot 2 [40,288,23,253]; slot 3 [20,298,15,296]. Requested larger margins were not fully met, but all silhouettes are complete and clear of source edges. Alpha-1 residue on slots 0/1 was retained under the approved source gate. Nothing was normalized or cleaned.

Artifacts: highland-first-four-complete-native-manifest.json; highland-first-four-semantic-manifest.json; highland-first-four-contact.png. Each source directory holds raw.png, its named master, prompt.txt, alpha-qa.json, source-evidence.json, evidence.json, semantic-notes.md and native-manifest-entry.json. No repository changes were made.
