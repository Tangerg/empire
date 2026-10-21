# Historical TypeScript source documents

These documents are inputs to the preserved TypeScript behavior oracle. They are
not native runtime content and are excluded from production exports. The native
campaign-level documents and localization metadata under `godot/data` are authored
independently. `export-grayflag.ts` may refresh source evidence here, but must not
overwrite native gameplay or localized wording.

The source-v1 documents contain the original player names/objective labels and
old battle outcome representation. Current native admission intentionally rejects
those obsolete shapes. Do not strip fields or remap old saves at runtime.
