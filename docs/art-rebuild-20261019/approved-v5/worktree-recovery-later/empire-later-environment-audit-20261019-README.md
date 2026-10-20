# Recovery audit: empire-later-environment-audit-20261019

Selected as the sole next recovery candidate. **Not cleared for deletion yet:** parent must preserve and verify exact original history in main and its remote before cleanup. This worker did not delete data, modify a worktree, create a bundle, or mutate Git.

## Candidate and byte verification

- Path: `/workspace/scratch/84c9fe948755/empire-later-environment-audit-20261019`
- Branch: `refs/heads/dot/extend-fortifications-contract-20261019`
- HEAD: `c044fa5979c80dba1da7b54cc7b858d05319e096`
- Allocated bytes: **2,382,974,976 (2.22 GiB)**; apparent bytes: **2,360,944,155**
- Audited main and locally recorded origin/main: `092039d21f56fe30324660c2ac0a5584099cad62`
- **7,330 tracked files / 2,360,363,964 content bytes**. Every file was independently SHA256-hashed, Git-blob-hashed, and compared with bytes streamed back from Git. All passed; every exact blob is reachable in audited origin/main history. Executable bits also match HEAD.
- 309 paths differ from current same-path main content; their exact historical blobs remain recoverable and are listed separately.
- Both initial and final tracked/untracked status were clean; HEAD remained unchanged. Nonignored untracked files: **0**.
- The full disk inventory accounts for every file and symlink plus the regenerable `.git` link pointer. No uncovered files, absent manifest entries, or empty directories.

## Ignored content and unique evidence

All 156 ignored entries are classified and individually SHA256-hashed:

- **91 dependency symlinks** into the retained main worktree's node_modules. This audit hashes link text and validates target existence; it does not duplicate or remove those targets.
- **15 local workspace dependency symlinks**, eleven into tracked packages and four into tracked apps. Their target paths and tracked-file counts are recorded. They can be recreated after restoration.
- **50 Python bytecode files / 571,157 bytes**. Every loaded code object exactly equals a fresh compile of its tracked source with the original filename and optimization level. These are regenerable caches, not unique source or evidence; regenerated headers need not be byte-identical.

No unique untracked art, ignored art, failed candidates, scripts, or evidence was found. No raw/shared outputs are within the deletion candidate.

## Exact history gap and bundle proposal

Original HEAD is not an ancestor of main or origin/main. The one missing commit is `c044fa5979c80dba1da7b54cc7b858d05319e096`. Its implementation is retained as main commit `17ff6a10de60e66f9dd0867686c1dba4545fb039`; all three changed source blobs are exactly identical and stable patch IDs match `7e73256fa30f073561eb7ad6b53a32e1a42660f1`.

A common-base-relative bundle can preserve the exact original commit and tree hierarchy:

- Required prerequisite base: `f8789f8fdfd54926a2485b088ca01974bb2536b3`, an ancestor of both original HEAD and retained main
- Range: `f8789f8fdfd54926a2485b088ca01974bb2536b3..c044fa5979c80dba1da7b54cc7b858d05319e096`
- Seven required objects: one commit, three trees, three blobs; **81,953 raw bytes** and **24,292 independently zlib-compressed payload bytes**. Actual Git bundle size is unmeasured and may be smaller with deltas.
- The three changed blobs already exist in main history. The original commit and three tree objects do not; patch equivalence alone is insufficient preservation.

Parent-only proposed creation command, using a chosen retained report directory:

`git -C /workspace/scratch/84c9fe948755/empire-art-rebuild-20261019 bundle create <retained-report>/later-environment-original-head.bundle dot/extend-fortifications-contract-20261019 ^f8789f8fdfd54926a2485b088ca01974bb2536b3`

Before deletion, parent must verify the bundle (including original HEAD and prerequisite), test original-history reconstruction, checkpoint bundle and reports in main, verify that exact checkpoint at the intended remote, and perform fresh status/HEAD/ignored/inactivity checks. Keep the original branch and common Git database. This audit does not authorize removal or waive the preservation condition.

## Restoration prerequisites

With the existing common Git database and retained local branch, use `git worktree add <empty-path> dot/extend-fortifications-contract-20261019`.

With only a complete clone of the preserved main branch, first verify the retained bundle SHA256 and `git bundle verify <bundle>`. Main must contain prerequisite `f8789f8fdfd54926a2485b088ca01974bb2536b3`. Fetch the bundle's original branch into a new local recovery ref, then add a worktree at that ref. Verify restored HEAD equals `c044fa5979c80dba1da7b54cc7b858d05319e096` and validate the per-file SHA256 manifest from the restored directory. A shallow clone missing the prerequisite is insufficient.

The ignored manifest records all dependency links. Recreate their parent directories and links when needed; map old-worktree prefixes to the chosen new path for local workspace links. Main-target dependency links require retained or reinstalled main dependencies. Python 3.12 bytecode can be regenerated from restored tracked sources. None of these caches is needed to recover source/art content.

## Inactivity evidence and limitations

Coordinator explicitly confirmed at 01:39:23 UTC that no active assigned worker or workflow uses this candidate or its branch. Active integration uses main; engineering worktree remains reserved separately.

No incoming symlinks from the scanned scratch workspace or shared art-output tree target this candidate. No visible process cwd/executable/open descriptor targets it. **Tool commands run in an isolated PID namespace: these observations do not prove host-wide process inactivity.** The activity manifest preserves the exact coordinator confirmation, scan roots, exclusions, and visibility limits.

Protected paths remain main, layout-engineering, push-check (especially its common `.git`), and all shared art/source/failed-candidate outputs. Atlas-contracts received preliminary status/ancestry inspection only and is not selected.

## Files and integrity

- `-summary.json`: decision, identities, sizes, status, verification, remaining gates
- `-tracked.json` / `-tracked.sha256`: complete per-file exact recovery records
- `-tracked-different-from-origin-main.json`: 309 historical/current-main differences
- `-ignored.json` / `-untracked.json`: exhaustive ignored and untracked records
- `-filesystem-inventory.json`: full accounting and `.git` pointer hash
- `-activity.json`: task ownership and process/link checks
- `-bundle-assessment.json`: exact seven-object recovery proposal and patch provenance
- `-retained-links.json`: retained paths and content recovery references
- `later-candidate-selection.json`: why this candidate alone was selected
- `audit_later_environment.py`: read-only audit implementation
- `-manifest.sha256`: report-package integrity

Audit window: 2026-10-20 01:38–01:42 UTC. Executor filesystem timestamps run approximately eight hours ahead; platform UTC reminders are used above. No live remote network verification was performed; it remains the parent sole writer's responsibility.
