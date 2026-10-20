# Offline experience-lab resource pipeline repair

Snapshot: 497 formally bound topics; original art bytes and all HD outputs retained.

The previous `viteSingleFile()` defaults embedded every binary dependency as a base64 module literal before Rollup transformed/rendered the bundle. Three isolated builds exited 137, with measured peak RSS between 2.81 and 3.64 GiB; this is evidence of resource pressure, not a proved kernel OOM diagnosis. The previous failures remain in `verification-484`.

The experience-lab now leaves binary resources external during code generation, uses Vite's supported `renderBuiltUrl` hook for deterministic placeholders, then replaces them with original-byte data URLs during `generateBundle`. The existing singlefile plugin still owns JS/CSS HTML inlining. Unknown binary types, lost references, missing files and remaining external binary outputs fail the build. Source query cache suffixes are discarded for data URLs; SVG fragments are retained.

Validation:
- TypeScript typecheck passes.
- Three fixture tests pass: static image import, dynamic import + `new URL`, CSS image and WOFF2 font, public SVG with query/fragment, exact byte hashes, one-file output, unchanged source files, and unsupported binary fail-closed behavior.
- All four application production builds pass on this snapshot, including experience-lab's 902 transformed modules.
- The resulting HTML contains all 662 audited binary resources, 174,813,801 original bytes, with exact SHA-256 and byte-length matches. No resource token or external HTML/CSS resource remains. The generated artifact is the sole file in its output directory.
- Isolated successful measured build: 29.09 seconds, peak RSS 2,168,640 KiB. First successful build: 21.07 seconds, 2,151,704 KiB. Timing varies; compare the recorded workload rather than claiming a universal speedup.

`resource-integrity.json` records the exact final HTML hash and all resource hashes. Reproduce with `npm run build:apps`, then `python3 tools/build/verify_single_html.py --html dist/experience-lab/index.html --build-log <captured-build-log> --report <report.json>`.

The 234 MB HTML is a build artifact, not checked into Git. Sources, code and reproducible evidence are committed. This proves packaging and byte integrity, not live browser rendering. The previous full suite's three timeouts and unchanged isolated 10/10 pass remain separately documented in `verification-475`; this focused build repair is not a claim that an entire later full suite was run.
