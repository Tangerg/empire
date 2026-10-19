# Bounded transparent-character card exporter checkpoint

Tooling only. No new character/background art, production catalog registration,
card runtime replacement, hero appearance binding or UI change is included.
The work was frozen at this tested point when stone-ground integration became
the next priority. The detailed entry schema is in `tools/art/README.md`.

Implemented through existing `export_overrides.py` and manifest parsing:
- Gray Flag existing192×224 narrative-static/character-card only
- Independent checksummed true-RGBA character and opaque background source files
- Explicit bounded whole-canvas character placement, alpha composited once
- Background center-cover with exact source crop box in the report
- Original sources, all manifests and unregistered runtime outputs retained
- Opaque output clearly labeled a UI composition, not transparent-source proof

Validation:
- `test_narrative_card_overrides.py`:14/14pass, including the actual80-topic
  manifest in an isolated fixture, one/two replacements and79/78unchanged files
- `test_export_overrides.py`:15/15existing static tests pass
- Read-only `export(complete_main, complete_main/catalog, check=True)` with this
  tool: all231current registered static outputs match. Catalog SHA before and
  after was1e8b7aad48883be5438bc27f3fe32ef5c6745b2c8ec09efb75faa221cf018be0
- `py_compile` and `git diff --check` pass

Full application tests/builds were not rerun: no runtime production data or
TypeScript changed. Sparse worktree native source availability is incomplete;
the231-output check explicitly used the complete main checkout read-only.
Logs preserve the focused fixtures and every static-source/output hash report.
No actual generated character card has been accepted or previewed in the UI.
Human source/identity review remains essential: alpha validation cannot detect
painted checkerboards or prove that a transparent subject is the correct person.
