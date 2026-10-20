# Authored level validation checks

Run `bash tools/godot/level-validation-checks/run.sh` from the repository root.

The original TypeScript level validator supplies ordered error-level diagnostics
for 16 GrayFlag campaign levels, the sample level, and focused invalid/valid
variants covering every source validation section. The native runner compares
issue text, order, deduplication, and the source fractional-index failure, then
checks actual engine admission, input ownership, and validator snapshot isolation.
Warnings are intentionally omitted. Arbitrary malformed JSON schema emulation is
not claimed; source numeric-domain guards are covered where explicitly present.

The production composition applies additional native execution-capability guards
after these source-compatible checks. Their intentional unsupported-extension
refusals are tested separately in `persistence-reference-checks` rather than
rewriting source expectations.
