# Reference checks and save-reader contracts

Run from the repository root:

```sh
bash tools/godot/persistence-reference-checks/run.sh /tmp/empire-reference-save.json.gz
python3 tools/godot/persistence-reference-checks/mutation-controls.py /tmp/empire-reference-save.json.gz
```

For a native-only run against the frozen source observations:

```sh
bash tools/godot/persistence-reference-checks/run.sh --frozen
```

`--frozen` never invokes Node and reads the checked-in fixture by default.
`--verify-guard` also runs the four isolated mutation controls. Ordinary source
regeneration without a supplied path uses a unique temporary directory.

The source generator executes the original TypeScript engine; production C# does
not read fixtures or invoke Node. The suite checks 100 save/load cases, 9 catalog
cases, 10 authored-level cases, all 16 shipped campaign opening roundtrips, and 10
registry/reader/structural contracts: 145 checks in total. Successful cases compare
complete JSON state/documents/events, not merely acceptance. Failed actions compare
exact errors and unchanged state. Rejected save and load inputs remain unchanged.

## Deliberate compatibility corrections

`legacy-cases.ts` retains all 94 former future-payload gate cases by their original
names: 75 save cases, 9 catalog cases, and 10 level cases. `compatibility-map.json`
enumerates each case, its former expected acceptance, its observed source boundary,
and its exact source error when present. The remaining entries record new controls.

At the phase each old case actually tested, source rejects 7 and accepts 87. Of
those, 59 old native expectations were stricter than source. These are intentional,
source-backed corrections, not suppressed failures:

- The source save reader checks current rule IDs through the ordered reference
  registry. It does not interpret every queued effect payload or execute its
  placement, selector, status, account, or objective semantics during loading.
- The source catalog checks declared abilities, hit effects, shapes and default
  reactions. An unused account or cost with an unresolved resource ID is not itself
  an invocation of that adapter.
- Authored-level semantic references come from registered handlers. Future spawn
  overrides are not an additional native-only capability gate. The original level
  fixtures can remain in deployment, so opening upkeep has not necessarily run.
- An unknown condition inside a `failOn` objective is not an extra state-reference
  traversal. Its condition owner resolves it when that objective is evaluated.

New delayed-use tests cover unknown base/commander upkeep, status application,
resource changes, spawned unit types/owners/cells, directive zones, and objective
conditions. They establish the same execution-time error and transaction rollback
as source. A separate test follows an unknown future directive through assignment:
the action succeeds, then its now-current ID is rejected at the next save boundary.
An unrecognized resource account and an unused site-resource override remain
losslessly inert through load, save and subsequent valid actions.

No structural safety validator was removed: schema, header, timestamp, map shape
and lengths, live placement, unit/player identities, generated IDs, random counters,
resource account shapes/balances, cast shape/clocks, deployment and transport guards
remain with the single default save inspection. The existing persistence suite
retains its structural negative corpus and passes 96 checks. Its three obsolete
semantic refusals now assert detached source-compatible roundtrips.

## Ownership and substitution

The ten default checks retain source order and first-seen message deduplication.
Register/replace/clone/seal tests cover ordering and independent engines. Custom
condition children and effect references prove that metadata belongs to handlers,
not a second type-switch traversal. Unknown tree kinds stop traversal before their
unknown shape is inspected. Content, level, and current-state hooks are all tested.

Named plugins replace the reference registry and save reader through the real
composition root. Tests verify reader environment identity, both Save and Load
calling that reader, exact named manifest identity, save/reload, and replay. An
opaque custom hit effect carrying a `status` field proves the save reader does not
guess its meaning. Custom check failures preserve their cause and are classified at
the stored-document boundary, matching source. Definition errors preserve distinct
configuration/level exception types and ordered issue lists.

Four isolated mutation controls deliberately break production behavior: bypassing
the reader on Save, removing state references, reversing check order, and guessing
all hit-effect `status` fields. Each builds successfully and then fails the semantic
tests that protect it; a compiler error is explicitly not counted as detection.
