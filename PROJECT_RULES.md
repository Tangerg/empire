# Empire repository guidance

## Project philosophy

- Prefer designs that are coherent, readable, and easy to explain. Beauty here means that names, responsibilities, dependency directions, and runtime behavior agree with one another.
- Make important relationships explicit. Dependencies belong in declarations, ownership belongs in aggregates, capability identity belongs in contracts, and runtime choices belong in ordinary parameters. Do not infer them from ambient state, call stacks, installation order, ancestor lookup, or hidden globals.
- Choose the simplest model that completely expresses the requirement. Do not confuse simplicity with missing semantics: irreducible complexity should remain visible instead of being hidden behind magic.
- Keep the conceptual structure as flat and orthogonal as the domain permits. Add nesting only when it represents real ownership or composition, never merely to organize implementation details.
- Keep APIs sparse. Every public concept must earn its place, have one precise responsibility, and compose with the existing primitives.
- Optimize for the reader. Prefer ordinary TypeScript, intention-revealing names, small state machines, and local reasoning over clever metaprogramming, implicit proxies, decorators, or surprising control flow.
- Special cases do not justify a second semantic path. Express them through composition or a higher layer unless the underlying abstraction is genuinely different.
- Let practicality correct theory. Use tests, measurements, and the shipped campaign to challenge a design, while preserving the architectural invariants that make the system understandable.
- Never let errors disappear accidentally. Propagate, aggregate, report, or explicitly classify them; silence them only at a deliberate boundary whose behavior is documented.
- Refuse to guess when input, ownership, capability selection, or state is ambiguous. Reject the operation with a precise error and require the caller to make the choice explicit.
- There should be one obvious canonical way to express each semantic operation. A convenience API is acceptable only when it mechanically compiles to that path and owns no second state machine.
- Implement a proven need now, completely. Leave speculative features unimplemented rather than shipping a premature abstraction that must later be replaced.
- Treat explainability as an architecture test. If an implementation is hard to explain in terms of the public model, first assume the model or implementation is wrong.
- Use namespaces deliberately. Stable capability ids, content ids, module boundaries, and package layers should prevent collisions and communicate ownership; do not turn a catalog or a ruleset into a bag of globally mixed names.

## Engine architecture axioms

- The microkernel composes capabilities and knows no game rule. Plugins declare what they `provide`, what they `override`, and which capabilities they consume; the host resolves order, refuses cycles, and refuses a manifest that disagrees with what a plugin actually installed.
- There is exactly one composition root. An engine is built by `createBattleEngine` through the plugins, and a second assembly path — a factory that rebuilds the defaults by hand — is a defect even when it produces the same object.
- A rule is replaced by declaring `overrides` and calling `context.replace`, ordered after the capability's introducer and before every consumer. Substitution that only works when nobody captured the value at install time is not substitution.
- Ports are declared by the consumer that needs them, and `BattleRuleServices` satisfies them structurally. Needing a new rule must not add a module edge, and a port must not grow a field its own module never reads.
- Every extension point is an open registry — a declaration-merged kind map plus `register`/`replace`/`keys`/`clone` on the shared registry base. Comparing a kind to a string literal outside its own module is how the closed union grows back.
- One call shape: `f(dependencies, subjects…, emit)`. Dependencies first, the event channel last, two or more services combined into a named port, and never a required parameter after an optional one.
- Asking and committing are different acts. A query that cannot answer returns `null`; a command that cannot proceed throws. `IllegalActionError` means the order was wrong and is shown to the player; `DomainInvariantError` means the caller was wrong and is a defect. A `catch` that does not bind its error may not produce a value.
- There is no ambient content. A catalog is composed per engine by the application root, so two engines in one process can run different themes, and no library installs content on import.
- State is plain serializable data; rules are objects. Whatever the rules mutate, the clone must copy, the digest must cover, and the save must carry — each of those is fenced by a test rather than by memory.
- The battle is deterministic. Randomness is a seeded counter stream living in the state, and a replay, a clone, or a reloaded save that diverges is a bug in the engine, not in the harness.
- Rich domain models own their invariants: aggregates for multi-entity transitions, entities for their own fields, a read model for projecting the battlefield's layers, and a single writer for changing them. A rule asks the model instead of reassembling the invariant beside it.
- Every rule has exactly one owner, and the owner is named in `docs/engine-capabilities.md`. The same question answered in two places will diverge the moment a second kind of play arrives.
- Geometry belongs to the tiling, not to the modules that measure. Distance, adjacency, facings, and where a cell sits are one strategy's answers.
- Presentation renders the ruleset it was handed. It never reaches for a global catalog, and it asks a strategy for its behaviour rather than for its id.

## Working rules

- Backward compatibility is not a goal during the current development stage. When a design changes, remove obsolete paths instead of adding compatibility layers, fallbacks, aliases, or migrations.
- Fix causes rather than symptoms. Do not accept a stopgap that is intended to be replaced later; make architectural decisions for the long term while breaking changes are inexpensive.
- Grow the system in complete vertical slices. A capability the rules have but no interface can reach is unfinished; say so in the capability table rather than counting it as done.
- Keep components modular and responsibilities sharply separated. Introduce a pattern or abstraction only when it makes an existing responsibility clearer or a real composition point possible.
- Prefer established, maintained libraries when they reduce total complexity or improve reliability. Check existing dependencies, documentation, and types before reimplementing functionality or adding another package.
- Do not optimize from intuition alone. Measure the relevant path, implement the simplest change supported by evidence, and preserve a benchmark or behavioral guard when regression risk is meaningful.
- Prove a behaviour-preserving refactor instead of asserting it. Replay every shipped level and compare the event streams and outcome digests; characterise any intended delta precisely rather than accepting a diff.
- Tests should protect semantics and architectural boundaries, not implementation trivia. Add a guard and then deliberately break the thing it guards: a regex that is always true looks exactly like one that works.
- Keep documentation, public types, runtime behavior, and architecture guards consistent in the same change.
- Judge an engine export by responsibility, abstraction quality, and downstream utility. Repository-local usage is weak evidence either way — but the shipped campaign is real evidence, and a capability it cannot express is a capability worth doubting.
