# Narrative family and seven-hero identity audit

Read-only audit of all80Gray Flag narrative topics, their21native source boards,
existing crop order, actual runtime files, and shipped UI consumers. No images
were generated and no narrative source/catalog/runtime binding is changed.

- `review.md`: findings, opacity/current UI versus required transparent source
  distinction, first-four-card scope, confirmed two scene/key-prop mismatches.
- `inventory.json`: all80paths/hashes/sizes/alpha, exact source slots/crop boxes,
  source-board hashes, direct consumers, contract hashes and audit checkpoint.
- `hero-identity-contract.md`: seven named identities versus actual rule and
  battle mapping; generation constraints and minimal future appearance binding.
- `hero-references.json`: seven old portraits/original board cells, seven current
  battle strips, all28native action paths/hashes and published-sequence proof.
- `audit.py`, `collect_hero_references.py`, `audit.log`: read-only reproducer.

Run against a complete checkout; native boards and cached legacy prop mattes are
required. Give a separate output directory to keep this captured evidence fixed:

```sh
python3 docs/art-rebuild-20261019/approved-v5/narrative-family-audit/audit.py /absolute/repo /tmp/narrative-audit
python3 docs/art-rebuild-20261019/approved-v5/narrative-family-audit/collect_hero_references.py /absolute/repo /tmp/narrative-audit
```

The audit imports only pure legacy crop/fit helpers; it never invokes the builder
or chroma subprocess. Existing cached prop mattes are read as evidence. All21
source boards and current narrative outputs were visually inspected; all7battle
HDstrips were inspected. Native action references are byte-hashed and their
published sheets match current runtime; this does not substitute for new action
acceptance or certify the seven named identities as already consistent.

Results:64opaque outputs reproduce exact RGBA pixels;16transparent legacy props
match RGB but have documented small alpha differences.18topics have direct
story/HUD adapter consumers;62are catalog assets without demonstrated direct
consumers there. The latest requirement is genuine RGBA character masters plus
independent backgrounds if UI composition needs one. Legacy opaque cards are
observations, not approval to author opaque character masters.

Known identity problems: Laiya uses an older moustached generic flag guard;
Mirelle uses a bearded male generic gravekeeper; Roderick uses a younger foot
mace soldier although rules explicitly require mounted/lance cavalry. Ivra's
juvenile versus mature-looking growth sheet needs stage review. No gameplay or
presentation binding has been changed to conceal these mismatches.

No live browser/GPU claim is made. This artifact audits source/data and existing
UI code; actual future generated cards/hero appearances require complete UI and
BoardView acceptance with a fixed source snapshot.
