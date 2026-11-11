# Offline evidence reproduction

This archive preserves source-art and static integration evidence only. It does not establish native gameplay, a passing runtime/full gate, legal battle outcomes or performance.

## Files and paths

The preserved `source-row.png`, exact `prompt-v1.txt`, `generation-provenance.json`, six individual RGBA poses, packed atlas, extraction metadata, both-facing native contact sheets, lossless diagnostic frames and nonlooping GIF live one directory above this file. The 6× nearest-neighbor contact sheet and neutral-backed source view used during review are also archived.

The original generation path and original reference arguments in `generation-provenance.json` are historical tool provenance and were copied unchanged. They are not remapped to invented archive paths. `sourceCopy` and `promptFile` identify the portable preserved files. The exact original output and preserved source matched at review time. Independent source verification compares the original path again if it is available and otherwise explicitly records `preservedSourceMatchesAvailableOriginalGeneration: false`; original-path absence is not presented as a newly performed comparison. All actual source RGBA, prompt/reference hashes, crops, timing and diagnostics remain verifiable from this archive and the repository.

The two independent scripts derive their default packet and repository paths from their own locations. They do not depend on the temporary QA directory or the caller's working directory. Optional `--packet` and `--reference-root` arguments remain available.

## Commands

From the repository root, with Python, Pillow, NumPy and SciPy available:

```
python docs/art-gravekeeper-fall-20261111/verify-extraction.py --reference-root .
python docs/art-gravekeeper-fall-20261111/verify-registration.py
python docs/art-gravekeeper-fall-20261111/verify-data-scope.py
python docs/art-gravekeeper-fall-20261111/verify-source.py
python docs/art-gravekeeper-fall-20261111/verify-integration.py
```

The first three existing scripts were inspected, left unchanged and rerun successfully. The independent source verifier uses separate Euclidean dilation masks and reconstructs all 26 diagnostic image areas. It checks both contact magnifications and rejects a visible one-channel mutation. Independent integration verification checks accepted atlas bytes/registration, the exact narrow JSON delta, check registration, UID uniqueness, import-companion presence and tracked production source scope. Documentation/evidence files are not misclassified as production code.

The archived scripts were also run successfully from an unrelated working directory, using their own paths. Their recorded results are:

- `extraction-passed.txt`
- `registration-passed.txt`
- `data-scope-passed.txt`
- `independent-source-verification.json` and `.txt`
- `integrated-verification.json` and `.txt`

To refresh JSON reports from this verification directory:

```
python ../verify-source.py --report independent-source-verification.json
python ../verify-integration.py --report integrated-verification.json
```

`verify-data-scope.py` and the independent integration verifier require a Git checkout containing baseline `ea0eb0513e48ed650c2c04d259bfc4baee7f3021`. A source-only Git archive without history can run extraction, registration and independent source verification, but cannot independently recreate that baseline comparison without supplying the baseline history. This requirement is explicit rather than silently substituting a current file or dropping the scope check.

`prepare-review.py --reference-root /path/to/repo` reproduces the offline art packet in its own directory and is intentionally a writing operation. It was not rerun while packaging: the reviewed PNG/GIF bytes were preserved. None of the verification commands builds, imports or launches the game.
