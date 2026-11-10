# Empire build identity audit

PASS: executable code and all retained PE/CLI bytes are identical, after narrowly normalizing declared build-identity metadata. Raw binaries are **not** identical.

## Exact inputs

Old directory: `checkpoint/final-portable/assemblies/`. New directory: `/workspace/scratch/84c9fe948755/empire-work/godot/.godot/mono/temp/bin/Debug/`.

- Empire.dll: 12606 methods; 143 differing raw bytes, zero unexplained bytes.
  - Old SHA256: `23f3ea10fdbb1bc4f0329c9c995cfe32ff1545a412126b6c90671a0575d9cd9c`
  - New SHA256: `fd0bb3108808b43ca7540e9dc9e6c72248e1f526d049be933e7bc3a483c152e1`
  - Normalized SHA256 (both): `110723a5e3364d36815434a4cc2e768de74aeb2053a9bb8877455213f96f571e`
- Empire.Core.dll: 5351 methods; 144 differing raw bytes, zero unexplained bytes.
  - Old SHA256: `507d7f21336e5baf00e60adfea7ae56e2b3aff34f5d1e0a2af36e7f74d4833fc`
  - New SHA256: `03a8ebc87bbbfa7ad4bf053000f52fbd1ad687f39894cfc95371d6a06097281e`
  - Normalized SHA256 (both): `cc3986f417d5d5581c5260836edb244ab16b326090253bccded7381cb9eb1b4d`

Both informational versions change from `1.0.0+c080a45f9ea8b09a28f96f96a8da4cd400579c78` to `1.0.0+3de50e3906bb8ca78594d53f777a3cc6d04bac27`.

## Method and whole-image proof

The independent harness uses .NET 8 System.Reflection.Metadata / PEReader and never loads or executes the inspected assemblies. It compares every MethodDef's token, name, raw signature, attributes, implementation attributes, RVA, IL bytes, MaxStack, InitLocals, local signature token and blob, and every exception-region field. All match. Metadata token maps are preserved because entire normalized images match.

Only these parsed/narrowly located spans are zeroed: COFF timestamp (4 bytes), module MVID (16), informational-version git commit (40 ASCII), the same git commit in native version resource (80 UTF-16), CodeView entry timestamp (4) and PDB GUID (16), PDB checksum (algorithm retained). All original offsets, sizes and bytes are recorded in each result JSON. No method, field, type, property, event, constant, generic, metadata-reference, initialization-data or managed-resource region is omitted. Whole-image equality therefore also covers all fields/signatures, metadata tables/heaps and resource bytes except the explicitly listed version string. Both assemblies have no ManifestResource rows. CodeView path and age are retained.

## Negative control

Two independent scratch COPY mutations were tested: one IL byte (offset 748) and one Field metadata attribute byte (offset 1969402, XOR 16). Both were rejected with exit code 1 and exactly their one unexplained byte. The IL mutation also changed the method manifest; the field mutation did not, proving the whole-image guard covers non-method metadata. Production binaries were not touched or rebuilt. See `selftest-results/summary.json` and per-case evidence.

## Limits and provenance correction

The originally supplied `checkpoint/assemblies/Empire.dll` is an earlier, different binary (SHA256 d53dc0e8938c2697330cabd15e761f98376690f50a983502fdbcbf5e4f545b4b). It is not evidence for this identity claim. This report uses the explicitly requested 23f3… file from final-portable and its matching Core.

This proves executable-code identity under the stated exclusions, not universal semantic equivalence: reflection/file inspection can observe version and MVID differences. External PDB/SourceLink files were not compared, so no claim is made that SourceLink is their only difference. Assets, runtime/dependencies, native video provenance and gate success remain separate evidence. Native recording provenance is documented independently by the native proof, bound to the recorded old assembly SHA256.

Machine-readable acceptance: `acceptance.json`. Detailed evidence: `results/summary.json`, per-DLL JSON, and one gzip-compressed old method manifest per DLL. `method-manifest-hashes.json` records old/new manifest SHA256 equality and every negative-control manifest hash. Negative-control full method manifests are retained only in scratch, not in this package. Harness: `Program.cs`, `Audit.csproj`.

## Packaging and revalidation

Program.cs was formatted with the repository .editorconfig using targeted `dotnet format whitespace Audit.csproj --include Program.cs`, then checked with `--verify-no-changes`; both exited 0. The formatted harness was rerun in separate scratch: normal comparison exited 0, both negative controls exited 1, and the selftest driver exited 0. See run-status.json and associated logs. Only one gzip-compressed old manifest per DLL is retained; normal new manifests have equal SHA256. All normal and negative-control manifest hashes are retained in method-manifest-hashes.json.
