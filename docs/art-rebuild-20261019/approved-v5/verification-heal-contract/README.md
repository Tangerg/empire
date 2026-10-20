# Heal lifecycle contract before replacement art binding

FX17 is an actual `healFx` consumer. BoardView.animateHeal owns a 500ms lifetime; animateHit's 420ms must not be copied onto it. Both retain their existing production behavior.

A headless real BoardView/SVG adapter regression observes phase0 at0ms, phase1 at83.34ms, phase2 at166.67ms and phase3 from250 through499ms; the heal effect is removed at500ms. It checks the actual FX17 HD resolver, centered metadata pivot, +amount independent text and unchanged battle state. The existing hit420ms test also passes. These two deterministic-clock tests are adapter/timing evidence, not live browser rendering.

The exporter now admits only the reviewed FX17 semantic order: healing-onset, leaf-spiral, healing-peak, orbit-dissipation. 31 exporter tests pass, including nonloop and exact-order validation; typecheck passes. This enables the future reviewed art, and does not count unbound source candidates as complete.
