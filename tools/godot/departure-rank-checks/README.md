# Departure and rank policy parity

Run `bash tools/godot/departure-rank-checks/run.sh` from the repository. Set
`EMPIRE_TOOLCHAIN_ROOT` to the existing toolchain if it is not beside the checkout.
`--frozen` uses the committed source oracle without Node or TypeScript. Add
`--verify-guard` to compile and reject sixteen isolated semantic mutants.

The offline TypeScript source oracle is generated through the original
`createBattleEngine` and original domain owners. It is a test-only dependency;
there is no TypeScript runtime bridge in the native application. The runner
compiles native core source into a temporary standalone console project and
compares 48 fixtures with 337 exact assertions: results, error kinds/messages,
ordered events, state digests at every emitted event, full resulting state, final
digest, and ordered registry keys. Separate native assertions protect the exact
injected catalog, grid, damage owner, and registry identities.

The fixtures cover:

- Default, fractional, changed, and custom non-monotonic rank policies; invalid
  thresholds; rounding, non-positive progress, no demotion, query delegation,
  career-before-rank event order, combat, capture, healing, and support consumers.
- Default commander-then-cast order; appended, replaced, reversed, custom-only,
  and empty registries; exceptions and partial state; nested commander routs;
  shared handler snapshots detached from casualty markers.
- Lethal and nonlethal damage, missing targets, morale routing, surrender,
  scenario withdrawal, forced collision, ordinary combat, counter kills, delayed
  casts, parting shots, status damage, and passenger loss without extra departure
  announcements.
- Duplicate/unknown/sealed registry failures, stable replacement position, and
  shallow independent mutable clones.

Each source fixture that promises a custom departure consequence or rank-policy
consumer asserts that the source actually reaches that behavior. Mutation checks
must compile and must fail their named assertion, not merely return a nonzero
exit code. They guard detached catalog recreation, hardcoded rank/threshold
policies, lost/reordered career awards, rank demotion, departure reordering,
private default fallback, duplicate defaults, missing lethal/rout/surrender/
scenario dispatch, swallowed failures, and wrong snapshot ownership.

The runner retains artifacts under the printed temporary path so failures can
be inspected. Mutants never edit the real source tree. Existing damage, combat,
progression, and transport oracle suites remain independent default-policy
regression checks.
