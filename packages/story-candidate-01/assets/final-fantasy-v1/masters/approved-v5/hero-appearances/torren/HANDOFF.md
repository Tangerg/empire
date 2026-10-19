# Torren stage01 art handoff

Both the four-pose battle source set and the transparent original-spear narrative portrait are accepted by the coordinator. This is a named campaign actor appearance with zero new canonical topic count. Formal repository integration remains coordinator-owned.

## Exact consumer contract

- Campaign roster ID: `torren`
- Exact level actor key: `campaign-torren`
- Existing unit type: `c01.swordsman`
- Named card: `C01-CHAR-TORREN-01`
- Apply the named appearance only through the exact actor-key presentation context. Generic swordsmen retain their independent appearance.
- No new rules alias and no stage02 activation or generation.
- Battle equipment follows the existing border-blade/slash rule: right-hand straight sword and left-arm repaired round shield.
- Narrative portrait retains the original right-hand spear and repaired round shield. This deliberate source/rule equipment distinction is recorded in the source audit; the original card is preserved as evidence.

## Accepted battle registration

Frame order: idle, walk-a, attack, walk-b.

- Shared scale: `44/894`
- Reference source foot: `[742, 964]`
- Source feet in frame order: `[[742,964], [758,950], [534,944], [713,950]]`
- Native sizes: 1586×992, 1586×992, 1586×992, 1585×992
- Common native canvas: 1586×992, with an explicit one-column transparent extension on the right of walk-b only
- Display frame: 104×64; origin: `[8,4]`
- Explicit registration limit: 12
- Attack translation: `[208,20]` source pixels, equivalent to `[10.237136465324385,0.9843400447427293]` logical pixels
- Every whole source canvas fits at the same scale. No frame-specific fitting, trimming or equipment resizing
- Opposite support patterns and the right-hand lunge attack passed native review. Walk-a is a toe-up heel strike, not an entirely airborne front foot. Minor coat mottling is a documented accepted style caveat

Primary files: `bundle-manifest.json`, `registration-evidence.json`, `qa-review.json`, `pose-review.md`, `source-contract-review.md`, `source-evidence.json`.
Native review: `qa-common-scale/review-2x.png`.
Each pose preserves raw/master, the exact request and prompt, reference hashes, alpha QA and white/black composites.

## Accepted narrative portrait

Directory: `../../narrative/char-torren-01/`

- Native master: 1024×1536 RGBA
- SHA-256: `18c041c315a43e1f5a06ad623149572bf16450fd923615b42f14ab8dc62c5d22`
- Visible alpha>127 bounds: `[121,67,963,1462]`
- Visible margins: left 121, top 67, right 61, bottom 74 pixels
- Strict native perimeter alpha>1 count: zero; maximum native edge alpha is 1 and is preserved
- Complete spear, same tired young face, original pendant, gray quilted kit and patched shield
- Fullcanvas 192×224 and 92×112 diagnostics saved without alpha trimming
- Review image: `qa-card-fullcanvas-2x.png`; native white and black composites: `qa-white.png`, `qa-black.png`
- `attempt-01/` preserves the single immutable native attempt and exact request evidence
- Card background remains a separate layer owned by the coordinator

## Verification and acceptance

Coordinator accepted the native battle quartet and measured registration at 22:22 UTC, and portrait source identity and alpha framing at 22:27 UTC on 2026-10-19.

`final-delivery-verification.json` independently rechecks all five selected native sources: raw/master byte identity, RGBA mode, exact request/prompt equality, prompt hash, all reference hashes and strict alpha perimeter. All pass. No source image pixels were altered. No repository files were written by this worker.
