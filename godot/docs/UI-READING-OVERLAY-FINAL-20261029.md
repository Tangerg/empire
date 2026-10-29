# Final UI reading/overlay checkpoint — 2026-10-29

## Bounded scope

This final batch closes the specific P2/P3 findings from the preceding visual review.
It does not add chapters, change battle rules, redo approved artwork, or claim another
real C01 victory. After this checkpoint, further visual work waits for user feedback.

## Contracts

- A dossier, system menu or battle journal temporarily suppresses the competing
  refusal-feedback surface. The feedback label retains the latest reason, including
  updates received while the overlay is open. Closing restores it unless an existing
  valid selection/cancel/binding transition has already cleared it. Event receipts
  and the bounded battle journal are untouched: suppression is visibility only,
  not deletion, replay, expiry, or a new animation rule.
- Settlement metrics occupy a responsive group no wider than 440px; their labels
  wrap and values keep their exact source facts. This narrows label/value scanning
  distance without squeezing the remainder of the summary.
- Campaign reading uses a localized lightweight hint only when content genuinely
  overflows, together with a wider contrast-preserving scrollbar thumb. The hint
  receives its text through the existing view/text owners, not a second locale or
  gameplay state cache. Short prose reserves no extra hint line.
- Appearance of a reading hint changes the available viewport. `CampaignScroll`
  re-reveals only an already-focused descendant after that layout change, checking
  that focus still belongs to it. It does not steal focus or scroll the whole story
  on every refresh. The menu card also subtracts its actual visible scrollbar width
  when fitting its viewport; a disabled horizontal scrollbar cannot hide overflow.

## Evidence

Evidence uses the `ui3-` prefix under `empire-c01-evidence-recovery`.

Affected native checks passed: Narrative 3306 (35 documents, 91 beats, 11 choices),
Campaign UI 134, Camera integration 277, Tactical HUD 218, Result flow 154,
Navigation 289, Staging 155, Menu 105. These are targeted checks, not a fresh
all-chapter gameplay/replay run. Formatting and source metadata checks pass
(305 unique script UIDs); the localization source guard passes 1195 messages.

Navigation's existing atomic-load teardown fixture caught a disposed-window read
in the initial visibility implementation. Visibility queries now check native
instance validity and tree membership; that fixture passes without weakening it.

Actual GUI inspection found an additional same-dispatch boundary: moving into
the dossier and quickly clicking Close could return focus before the deferred
window-exit handler ran. The handler now remembers that the exit began in its
own dossier, and accepts the handoff only if the main window regained focus.
The test models that ordering before Close; real window exit/focus-loss checks
remain unchanged. This is not a timer, input bypass or persistent selection cache.

Native screenshots: `ui3-after/result-960.jpg` and `menu-640.jpg`;
`ui3-hud/details-1180.jpg`, `restored-1180.jpg`, `details-960.jpg`,
`restored-960.jpg`. The 960 detail image can be compared with the preceding
`ui-polished-v4/details-960-cua.jpg`; the result with `ui2-after/result-960.jpg`.
The refusal disappears only while inspecting the dossier; explicit close restores
the exact refusal, current forecast and focused details opener at both sizes.
The 640 menu exposes keyboard reading guidance, scrollbar and focused action.

Manual save comparison before/after the actual HUD inspection confirms identical
battle state and event list (17 events), SHA256
`934d72b3820675c89b6b19aecbd7e4a357587ec03069c89fdce30ed697346610`.
The game was closed normally. No move, attack, turn or outcome was executed.
The campaign preview is explicitly a no-save/no-engine fixture; HUD inspection uses
the separately retained manual QA battle, not a new completion run.

Deliberate negative controls cover competing feedback over the journal, an unbounded
metric group, an invisible reading hint, and the fast dossier-close handoff. All four were killed by the corresponding
observable assertion; originals were restored byte-for-byte before final builds.
