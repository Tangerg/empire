# Recovery audit: empire-next-active-audit-20261019

Audit only. No worktree files were changed or removed.

- Exact path: `/workspace/scratch/84c9fe948755/empire-next-active-audit-20261019`
- Allocated size: **2,322,558,976 bytes (2.16 GiB)**
- Detached HEAD: `654b382ca93f3a66a648d51f12f68fa981f8471c`
- Audited main HEAD: `8c10b0fd60aa7801365f208a10713d0489927d3d`
- HEAD is an ancestor of local main and locally recorded origin/main. All tracked evidence is retained in main history; recovery does not depend on this worktree's reflog.
- **7,165 tracked files, 2,301,107,483 file bytes**: SHA256 recorded; every actual file Git blob hash matches HEAD; zero mismatches.
- Tracked modifications: none. Nonignored untracked files: none. Final porcelain status: clean.
- Ignored contents: 91 dependency symlinks into retained main; 15 reproducible local workspace package symlinks; 2 Python bytecode files totalling 15,722 bytes whose `.py` sources are tracked. All paths and hashes/link texts are in the ignored manifest. No ignored raw art or unique audit evidence.
- No incoming symlink from any other directory under the scratch workspace or shared art-output tree targets this worktree.
- No process visible to this tool namespace had a cwd/open descriptor under the worktree. Active agent inventory had no task assigned here. **Host-wide absence is not proven: tool shell commands use isolated PID namespaces. Parent must confirm ownership/activity before removal.**
- Shared common Git database is `/workspace/scratch/84c9fe948755/empire-push-check-20261019/.git`. That worktree is explicitly excluded and must be retained.
- Active main and `empire-layout-engineering-20261019` are also explicitly excluded.

## Recovery

From a retained repository, `git worktree add <new-path> 654b382ca93f3a66a648d51f12f68fa981f8471c` reconstructs every tracked file. Dependencies and bytecode are regenerable. No unique uncommitted evidence was found.

## Decision

Data/recovery checks passed for this candidate. Parent owns authorization, host-process/task ownership check, current remote-backup verification, and any individual normal `git worktree remove`. This report is not permission to delete original art, failed candidates, shared outputs, the common Git root, or active worktrees.

## Files

- `empire-next-active-audit-20261019-summary.json`: machine-readable assessment
- `empire-next-active-audit-20261019-tracked.json`: all tracked paths, byte sizes, Git object IDs, actual SHA256 and exact blob match
- `empire-next-active-audit-20261019-ignored.json`: all ignored paths, symlink targets, hashes, source recoverability
