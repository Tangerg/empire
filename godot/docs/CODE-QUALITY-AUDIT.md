# Native migration quality audit

Audit baseline: `f8d3a2496c869ed0d452d3f0cb43c99ad1ea0127` (published migration branch).
This is an open remediation inventory, not a quality certificate. Passing replay
and lifecycle checks do not establish architectural compliance.

## Applicable requirements

The repository has one tracked `AGENTS.md`, at its root, which explicitly includes
`PROJECT_RULES.md`. No nested instruction file overrides these rules. The existing
`docs/engine-capabilities.md` is the required capability-owner reference; the native
migration must make its ownership mapping consistent with that reference.

Relevant exact requirements:

- AGENTS, Simplicity: “Reduce understanding and change costs, not line count.”
- AGENTS, Boundaries: “Shape small, cohesive interfaces around consumer needs”.
- AGENTS, Ownership: “Every fact has exactly one representation that may advance it.”
- AGENTS, Readability: “Use the host language's idioms” and “Never turn unexpected failure into apparent success.”
- AGENTS, Verification: “Do not distort production interfaces merely to mock them.”
- PROJECT_RULES, architecture: substitution must work for consumers that captured a capability during installation.
- PROJECT_RULES, architecture: queries unable to answer return null; commands unable to proceed throw; domain invariant failures are defects, not player-order refusals.
- PROJECT_RULES, working rules: remove obsolete paths rather than add compatibility layers; prove behavior-preserving refactors against every shipped level.

## Findings and remediation order

| ID | Finding and evidence at baseline | Required correction | Status |
| --- | --- | --- | --- |
| C01 | `DefaultBattlePlugins.cs:26–48` introduces grids and space in one plugin; line 33 passes its local grids rather than consuming the resolved capability. A declared grid override cannot reach movement/vision. | Split capability introduction from its consumer installation within the single composition root. Test non-default grid overrides through real consumers. Explicitly document correction of this flawed TS edge; preserve default replay semantics. | Resolved-capability installation and closed contexts implemented; independent source re-review and integrated build passed |
| C02 | `KernelCapabilities.cs:23–43` lets a retained installation context provide/replace after composition, bypassing the manifest and ordering checks. Kernel tests currently accept this as source parity. | Close installation contexts after installation, including exceptional exit. Reject late mutation and undeclared late consumption; preserve justified public read contracts. Test rejected writes and unchanged composed behavior. | Resolved-capability installation and closed contexts implemented; independent source re-review and integrated build passed |
| C03 | `CombatForecasts.cs:40–71` returns live catalog JsonObjects through public weapon queries despite the catalog's read-only external boundary. Movement holds a separate content projection, so external writes can split rule knowledge. | Keep definition ownership inside the catalog and expose immutable or detached query results. Trace other returned definition references before selecting the shared boundary. Test mutation isolation and all default replays. | Canonical catalog implemented; external-consumer isolation, representative workload checks and independent source re-review passed |
| C04 | `BattleDamage.Resolve` validates damage only after rounding/clamping; negative and nonfinite nonlethal requests become successful zero/clamped damage. Healing cap/round/write logic also appears separately in default heal and turn regeneration. | Establish one health-transition invariant owner and use the battlefield read model for terrain healing. Validate before mutation/clamping; cover damage, healing, return and progression consumers. | Canonical health and relocation owners implemented; return-owner preflight included; focused source flows, atomicity checks and independent source re-review passed |
| C05 | Default reaction/facing commands accept unknown identifiers and mutate live state; an unknown reaction can be committed and then makes SaveBattle reject that same live state. | Validate command choices through the resolved reaction registry/tiling at the command owner before mutation. Test custom accepted values and atomic rejection of unknown values. | Composed disposition admission covers commands, initial/spawned units and saves; atomic invalid-seed checks and independent source re-review passed |
| C06 | `BattleContentValidator:91–97` owns closed addStatus/removeStatus/forcedMove payload branches, while replaceable hit-effect handlers have no admission contract. Replacing a handler cannot replace its validation. Support reaction behavior also remains keyed by literal `support`. | Move structural/reference admission into each registered handler and demonstrated support behavior into reaction definitions. Remove closed external kind switches; test replaced/new kinds with real references. | Registered handlers own payload admission; reaction definitions own support behavior; affected baselines and independent source re-review passed |
| A01 | `BattleScreen.BindSession` accepts `IBattleSession`, then requires `ITacticalBattleSession` by runtime cast. | Declare the actual required port directly and keep one session collaborator, not two aliases. | Removed obsolete APIs; focused pure/native gates and three compiled mutants passed; aggregate after other owners pending |
| A02 | `GrayFlagSession.GetLegalOrders` repeats action/menu enumeration. Production tactical UI does not use it; source checks and release smoke keep it alive through `BattleScreen.SubmitOrder`. | Remove obsolete enumeration/type/submission API. Exercise the actual tactical controller, HUD and scene input in checks. Do not replace it with a compatibility forwarding layer. | Removed obsolete APIs; focused pure/native gates and three compiled mutants passed; aggregate after other owners pending |
| A03 | `GrayFlagGame.cs:122,137,157,208,264,274,276` catches all exceptions across domain transitions, rendering and persistence and reduces them to ordinary UI failures. | Classify expected storage/user failures at their owner; report unexpected defects distinctly with diagnostics. Verify atomic state and UI lifecycle behavior before changing catches. | Expected-failure classification and atomic view activation implemented and focused checks passed; merged narrative flow pending |
| A04 | `GrayFlagCampaign.Load:107–126` checks a few request identities but accepts missing initial level, empty roster bindings or forged camp-opening state before replacing the live campaign. The Request property exposes the owned mutable object. | One complete application-document reader and immutable request boundary; validate/regenerate authoritative facts before atomic activation. Test rejected input leaves all current state unchanged. | Canonical pending-request regeneration and atomic candidate activation implemented and focused checks passed; merged narrative flow pending |
| P01 | `DynamicStateView.Svg` and `TerrainView.Masked` create procedural textures without the explicit exclusive-ownership teardown used by `OwnedProceduralSprite`. | Trace actual managed/native ownership and prove the leak/teardown behavior before choosing one appropriate procedural-resource owner. Never dispose shared imported textures as if exclusive. | Committed in 1e1e1f7a: exclusive texture/material ownership and transactional configuration cleanup; native lifecycle and direct construction-failure evidence accepted by independent review |
| P02 | DynamicStateView independently reads unit/structure definitions from original raw packs rather than the composed engine catalog. | Pass catalog-derived narrow presentation definitions; do not construct another catalog or allow the renderer to advance definitions. | Renderer now consumes composed immutable catalog; 101 native dynamic-state assertions passed, including changed catalog HP; final aggregate pending |
| P03 | BattleScreen uses material-manifest outside tags to decide which cells accept input. Art projections thereby own an input-eligibility fact. | Obtain interactive-cell facts from the canonical session/battlefield projection; keep material bindings visual-only. | Canonical revision-bound interactive-cell projection implemented across input and fog; real native screen/camera/decoration checks passed with clean exits |
| I01 | Player-facing UI, application refusals, feedback and narrative contain embedded display strings without an owned localization pipeline. | Add stable semantic keys and parameterized Simplified Chinese messages using the agreed native translation boundary. Keep event identity, saved data and hashes language-independent; classify developer diagnostics separately. | Native translation foundation and schema-2 narrative content implemented; narrative/staging checks passed before final merged edits; remaining menu, domain, battle UI and diagnostic conversion pending |
| R01 | Metadata policy is inconsistent: early `.uid`/`.import` companions are committed, later generated companions are untracked. Scratch probe files also remain untracked. | Preserve source UID/import metadata with its source, ignore reproducible caches, keep identified scratch outside production commits, and mechanically verify companion consistency. Never call “tracked clean” a fully clean working directory. | Documented source companion policy and isolated 9-case metadata guard passed; final metadata staging pending |
| R02 | Dense one-line declarations/methods and repeated JSON indexing impede local reasoning; no native formatter gate is established. | First repair ownership/contracts, then apply a scoped host-language formatter and enforce it mechanically. Do not obscure semantic fixes in a giant style-only change. | Scoped .editorconfig and official SDK formatter command prepared; baseline correctly fails dense existing code; apply only after semantic owner freeze |
| R03 | Generic native engine is named GrayFlagEngine and production renderer types retain Candidate/Source prototype terminology. | Review actual responsibilities after ownership changes; remove misleading historical names where they obscure current contracts, without indiscriminate cosmetic renaming. Update all consumers/docs directly, no obsolete aliases. | Parent-requested API/readability pass pending |

## Scope and evidence discipline

JSON is justified at serialized extension/data boundaries and for exact ECMAScript
UTF-16/hash compatibility. Its presence alone is not a defect. The audit targets
concrete mutable leaks, duplicated knowledge, hidden dependencies and runtime
assumptions; wholesale replacement with typed wrappers is not an accepted plan.
Likewise, file length alone does not establish an SRP violation. Each split must
name an independent reason to change or a guarantee the new boundary owns.

No native check will be weakened merely to preserve a source defect that violates
an explicit repository architecture contract. Such intentional corrections require
new behavioral regression tests and a documented compatibility delta. Existing
shipped default battle event/hash streams remain the regression baseline.

Required final integration gates: focused boundary regressions and negative
controls; complete native aggregate with normal teardown; all 16 independent
battle action/event/hash comparisons; reviewed diff and verified incremental push.
Actual GUI appearance, Mac execution and missing authored scenery remain separate
acceptance scopes, not evidence that these architecture findings are resolved.

## Current verification boundary

Composition identity changes are declared in test-only `FixtureRuleset`: the source
manifest remains frozen, the native manifest adds the spatial consumer plugin and
bumps the tactical producer version. Only explicit save/replay document identities
are projected; battle state, events and digests are not normalized. Persistence
checks passed 97 assertions and reference/admission checks passed 145 on the
working tree. These results do not certify concurrent unverified edits.
