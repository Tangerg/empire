# Canonical tactical query parity

Run `bash tools/godot/tactical-query-checks/run.sh` from the repository root. The runner sources the existing official toolchain, executes the original TypeScript owners with vite-node, compares native queries, and builds deliberately broken native copies under temporary directories. It does not stage, commit, install, or modify production source for mutation tests.

The oracle executes `recruitOptions`, resource adapter inspection, rank policy, objective handlers, commander queries, tactic options, `activeCasts`/`SpellCastEntity`, area coverage, and the installed content registries. The native harness uses the engine's composed `Catalog` and singleton `Victory` owners. This is query parity, not widget or native interaction acceptance.

Covered contracts:

- Recruitment authored ordering, copied costs, unavailable funds, occupied/capped/foreign sites, unlimited accounts, and the source quirk that each card cost is affordable independently even when duplicate-resource totals prevent dispatch.
- Adapter registration order and nullable current/capacity for player, unit, and weapon accounts; wrong-subject and unknown-adapter failures.
- Rank boundaries and capped next-threshold queries. Existing progression checks independently execute awarding and compare full state/events.
- Every shipped objective kind, nested compound/wrapper progress, runtime hidden filtering, terminal/inactive/cancelled statuses, authored empty labels, composite parts, and JavaScript scalar score coercion.
- Live/dead casts, malformed live clocks, future/partial/due/overdue progress, public names and identity, and last-cast incoming-cell overwrite order.
- Commander absence, ownership, range and identity even with a neutral aura; detached leader/commander snapshots; tactic costs and source availability.
- Every composed catalog family and definition, detached projections, and escaped/unpaired UTF-16 code units in content keys and values.
- Every query leaves its supplied state unchanged.

Eight isolated mutants must compile and fail semantic assertions: removing unaffordable cards, aggregating card costs, leaking hidden objectives, changing a threshold, reversing account order, dropping neutral-aura identity, first-cast overlap precedence, and flattened cast progress. A compiler failure is not counted as killing a mutant.

Off-board card queries return an empty result consistently with the existing native recruitment option boundary; they do not reproduce source flattened-index aliasing. Full command refusal still owns bounds, occupancy, resource totals and caps. No canonical state hashes were changed.
