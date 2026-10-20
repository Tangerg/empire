# Named actor roster context

Real campaign staging previously omitted actor context, so Torren fell back to generic swordsman despite his reviewed portrait. Resolve only the current battle node's explicit rosterBindings and pass its levelUnitKey to portraitSvg. No inferred IDs, global soldier replacement or roster/rules changes. Red UI test reproduced the generic image; four UI tests now pass, including missing binding, unrelated same-type soldier, Elin and unchanged unit types/definition. Combined asset/UI suite159 tests passes; typecheck passes.

SHAMAN and evacuation driver are formally catalog-bound but currently have no deployed consumer; no scene deployment or live playback claimed. C01 combat catalog now40/40 authored four-pose bundles; full-game acceptance remains separate.
