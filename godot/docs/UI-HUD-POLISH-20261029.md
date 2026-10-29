# Native UI/HUD polish checkpoint — 2026-10-29

## Scope

This batch follows the rebuilt native recovery checkpoint. It preserves the approved
faithful-v4 character/scene/HUD artwork, map registration and battle rules. No chapter,
balance edit, installer or replacement C01 victory is included.

## Observable fixes

- **Readable battle history:** direct top-bar entry, bounded selectable/scrollable
  journal, Chinese close control, Esc isolation, and the existing 60-run retention and
  consecutive-repeat policy. Empty events no longer erase the last readable toast.
  Open history projects new receipts; rebinding closes obsolete windows and clears
  history through the existing binding contract.
- **Unambiguous receipts:** attacks, counters, support, parting and healing name both
  participants. Forced movement and collision are visible separately. Collision HP
  is the committed `hpAfter`, never an invented whole-action total.
- **Visible forecasts:** existing interception/reaction/support/collateral/death facts
  are on the forecast plate. Primary-strike HP is explicitly labeled as primary, not
  the eventual total after all extra effects. The calculation-details entry opens
  the existing modifier section expanded.
- **Reachable calculation details:** the selection controller owns `PreviewTarget`
  independently of the live activation cursor. Crossing ordinary map ground on the
  way to the HUD preserves the target's displayed prediction. Enter on non-target
  ground still refuses rather than executing the retained preview. The hint explains
  when the preview is held. Cancel/revision/rebind and genuine context exit clear it.
- **Explicit dossier exit:** a localized visible `关闭 Esc` button and native Esc
  both close only the dossier, restoring focus to the current opener without
  dropping the prediction or committing a command. Modifier defense rows name
  their source once; bottom plate padding leaves room below the operation hint.
- **Native focus boundaries:** map exit resolves its next input owner after Godot's
  dispatch; stale callbacks are invalidated by newer input/binding/lifecycle changes.
  An owned embedded dossier gaining focus is not mistaken for leaving the game.
  Active pointer gestures are canceled immediately on window focus loss; actual
  application focus loss still clears the inspection context.
- **Rejection feedback:** a contrasting material-backed notice replaces low-contrast
  text directly over grass. A successful new selection or cancellation clears stale
  refusal text. Terminal conclusion feedback is preserved.

## Why the pointer repair required more than a hover exception

Actual GUI testing exposed three independent boundaries: a mouse crosses ordinary
map cells before reaching a bottom HUD button; `MouseExited` occurs before the next
hovered native control is installed; opening the embedded details window transfers
native window focus. Caching a forecast in the HUD or retaining every cursor on
leave would hide these distinctions and could dispatch an obsolete target. The
selection owns the preview, the native viewport owns input/focus, and the session
alone projects battle facts. Each boundary is covered directly.

## Evidence

Current native evidence is retained outside the repository under
`empire-c01-evidence-recovery`. The focused gate uses isolated temporary save roots.
Results are finalized below only after successful process exits and teardown checks.

- Pure selection controller: 505 checks, including held-preview/non-target Enter,
  leave and cancel semantics.
- HUD: 218 assertions, including six simultaneous secondary forecast facts, actual
  visible bounds at 960/1180, expanded calculation details and current controls.
- Campaign UI: 88 assertions, including 75 long receipts, bounded 60-run history,
  keyboard entry/close, scroll region and non-overlapping localized close control.
- Camera integration: 273 assertions, including actual `Viewport.PushInput` traversal
  from an opaque enemy pixel across ground into the details button, real click/open,
  window/focus/excluded-map/rebind cleanup, and existing camera/busy/gesture contracts.
- BattleScreen real-session receipts and defense-label contracts passed.
- Result flow: 154 assertions; lighting lifetime: 97 assertions across three real
  load/rebind/restart/exit cycles. All focused scene processes exited 0 without
  teardown errors or leaked instances.
- Negative control: an isolated copy that updates `PreviewTarget` on ordinary
  ground failed the direct held-preview assertion; the working source was never
  mutated. All 26 maintained controller mutants were killed, including the new
  ordinary-ground preview mutant; the original leave-inspection mutant was
  re-anchored to the new code without changing its intended behavioral violation.
  The unmodified pure controller passed all 505 checks again.
- Final build: zero warnings/errors; formatter and source metadata passed; PO guard
  accepted 1194 nonempty, unique Chinese messages.

Actual GUI baseline covered menu, story, chapter briefing, staging and opening HUD.
The UI QA save is a separate turn-2 first-level battle, reached by one ordinary archer
move and one normal AI round solely to expose an attack forecast. Subsequent polished
runs load that unchanged manual save and inspect rather than replaying a victory.
The final run first entered from the campaign-only autosave (which starts at the
staging boundary), then used normal manual Load to restore the retained turn-2
QA state before checking details. No transient staging battle is victory evidence.

Verified screenshot: `ui-polished-v4/details-1180-cua.jpg`, an unmodified actual game
window screenshot (JPEG supplied by CUA, not a browser image). Visible close restores the opener focus; Enter reopens that focused
button rather than firing an attack; Esc closes only the dossier and retains the
forecast. Intermediate `ui-polished` and
`ui-polished-v2` images document diagnosed failures, not the final acceptance state.

At 960×640 the same visible close/Esc/reopen paths passed; the original CUA image
`ui-polished-v4/details-960-cua.jpg` preserves the actual window, including an
expected invalid-ground notice from the negative interaction probe. No text or
close control is clipped. Native manual Save after inspection preserved the exact
`battle.state` and `events` hash:
`934d72b3820675c89b6b19aecbd7e4a357587ec03069c89fdce30ed697346610`.
The application closed normally. The cloud GUI has no ALSA output device and
reported audio-driver fallback at startup; this is not an audio/platform acceptance.

## Boundaries

- The earlier full recovery gate remains its own evidence. This UI batch does not
  relabel it as a new 16-level replay or normal-AI victory.
- Primary strike/counter forecasts do not yet compute a complete force-move/collision
  end state; the HUD discloses its actual scope instead of summing incompatible facts.
- Legacy TypeScript feedback wording is historical comparison, not native acceptance;
  see `BATTLE-FEEDBACK.md`. No legacy source state is adapted to weaken native admission.
- GitHub publication requires restored write access. Local commits and private
  incremental bundle/patch/log backups are separate from a verified remote push.
