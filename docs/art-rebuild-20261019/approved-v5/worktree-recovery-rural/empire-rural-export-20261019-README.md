# Recovery audit: empire-rural-export-20261019

Decision: **not cleared for removal under the requested ancestry gate**. This audit changed no candidate contents or Git refs and removed nothing. No bundle was created.

## Candidate and exact recovery evidence

- Exact path: `/workspace/scratch/84c9fe948755/empire-rural-export-20261019`
- Allocated size: **1,839,357,952 bytes (1.71 GiB)**
- HEAD: `16cb145cb626bb6d2c658872a2a85ead38232afc`
- Branch: `refs/heads/art/rural-export-20261019`
- Audited retained main and locally recorded origin/main: `a75589cef304e76aa6184ac492472ca81facc190`
- **5,587 tracked files / 1,822,382,111 file bytes**. Every filesystem byte hash matches HEAD. Every Git blob was read back with `git cat-file --batch` and verified against filesystem SHA256 and Git object SHA1. All blobs are independently reachable in retained origin/main history; zero missing or mismatched blobs.
- All **320 tracked files differing from current main** are included individually in the difference manifest and recover exactly from main history by their recorded Git blob hashes.
- Clean tracked status; zero nonignored untracked files. Full filesystem inventory accounts for every file, symlink, and the regenerable `.git` pointer. No unclassified files or empty directories.
- **43 ignored Python bytecode files / 479,654 bytes**. Every cache was individually hashed and its loaded code object compared with a fresh compilation of its tracked source. All 43 match exactly. No unique ignored art, scripts, or evidence needs separate retention.

## Exact original history and proposed backup

The one commit absent from origin/main ancestry is `16cb145cb626bb6d2c658872a2a85ead38232afc`. `git cherry` returns `- 16cb145cb626bb6d2c658872a2a85ead38232afc`. Its three changed file blobs exactly match retained main commit `e101f84fb537b1d1faad1b839e08fad817dde3c1`; content equality was checked independently of patch equivalence.

The original commit and three tree objects are not reachable from origin/main. The original branch still retains them locally; normal worktree removal does not itself delete that branch. However, this does not meet the requested ancestry/remote-history condition.

A bundle relative to common base `d768b14d16688f64018f8f80435582387758383b` can retain the exact original HEAD and hierarchy. Required object set: **7 objects, 55,009 raw bytes**; independently compressed payload total **18,602 bytes**, so approximately 15–25 KiB is expected. This is an estimate, not a created or verified bundle. The base is an ancestor of both the original HEAD and retained main. See the bundle assessment for the exact seven-object manifest, proposed parent-only commands, and restore procedure.

Before any alternate cleanup plan: parent must approve the recovery alternative, create and verify the bundle, test reconstruction against the retained-main base, checkpoint the evidence and bundle, verify the remote backup, and recheck current status/inactivity. The audit does not waive the strict ancestry requirement.

## Inactivity and retained locations

- Coordinator explicitly confirmed no assigned active worker uses this candidate at 00:49:24 UTC. Active art work reads main and writes shared outputs; engineering uses layout-engineering.
- No incoming symlink from the scanned scratch workspace or shared art-output tree targets this candidate. The activity report records scan roots and exclusions.
- No process cwd/open descriptor visible in this tool namespace targets the candidate. **Host-wide absence cannot be proven: commands use isolated PID namespaces.** Preserve this limitation; do not represent the scan as host-wide.
- Retain main, layout-engineering, shared outputs/source originals, and common Git database `/workspace/scratch/84c9fe948755/empire-push-check-20261019/.git`.
- No fresh remote network check was performed; parent owns it.

## Deliverables

- `empire-rural-export-20261019-summary.json`: decision, counts, hashes, ancestry and blockers
- `empire-rural-export-20261019-tracked.json`: every tracked path, SHA256, Git blob, recovered-object SHA256, and main-history reachability
- `empire-rural-export-20261019-tracked.sha256`: plain per-file SHA256 manifest, paths relative to the candidate
- `empire-rural-export-20261019-tracked-different-from-origin-main.json`: all 320 differing paths
- `empire-rural-export-20261019-ignored.json` and `empire-rural-export-20261019-untracked.json`: individual classification lists
- `empire-rural-export-20261019-bundle-assessment.json`: missing commit, exact object set, common base, proposal
- `empire-rural-export-20261019-retained-links.json`: retained locations and recovery references
- `empire-rural-export-20261019-activity.json`, `empire-rural-export-20261019-filesystem-inventory.json`, and `candidate-selection.json`: activity/inventory checks and candidate selection
- `audit_rural_export.py`: retained read-only audit implementation
- `empire-rural-export-20261019-manifest.sha256`: checksums for the complete report package

Schema follows the previous `docs/art-rebuild-20261019/approved-v5/worktree-recovery` report, adding recovered-object verification and explicit bundle assessment. Clock-tool audit window: 2026-10-20 00:47–00:50 UTC; executor timestamps run approximately eight hours ahead.
