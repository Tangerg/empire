#!/usr/bin/env python3
"""Read-only recovery audit; writes reports only in the specified shared folder."""
import collections
import hashlib
import importlib.util
import json
import marshal
import os
from pathlib import Path
import stat
import subprocess
import types
import zlib

ROOT = Path('/workspace/scratch/84c9fe948755')
WORKTREE = ROOT / 'empire-rural-export-20261019'
MAIN = ROOT / 'empire-art-rebuild-20261019'
OUT = Path('/workspace/shared/empire-art-rebuild/approved-v5/worktree-recovery-next')
NAME = WORKTREE.name
AUDITED_AT = '2026-10-20T00:47:10Z'
COORDINATOR_CONFIRMATION = {
    'received_at_utc': '2026-10-20T00:49:24Z',
    'source': '/root/inventory_empire_art_rebuild collaboration message',
    'confirmation': 'Coordinator confirms all active assigned workers use main for reads and shared for generated outputs; completed engineering uses layout-engineering. No active coordinator-assigned task uses rural. Scribe completed. Root is interrupted.',
    'scope': 'Assigned agent/task ownership only; does not assert host-wide process inactivity.',
}
ENV = dict(os.environ, GIT_OPTIONAL_LOCKS='0', PYTHONDONTWRITEBYTECODE='1')

def git(*args, data=None):
    return subprocess.check_output(['git', '-C', str(WORKTREE), *args], input=data, env=ENV)

def dump(suffix, value):
    path = OUT / f'{NAME}-{suffix}.json'
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + '\n')
    return path.name

def zpaths(*args):
    return [p.decode() for p in git(*args, '-z').split(b'\0') if p]

def tree(commit):
    result = {}
    for record in git('ls-tree', '-r', '-z', commit).split(b'\0'):
        if record:
            info, path = record.split(b'\t', 1)
            mode, kind, oid = info.decode().split()
            result[path.decode()] = dict(mode=mode, type=kind, git_blob=oid)
    return result

def ancestor(a, b):
    return subprocess.run(['git', '-C', str(WORKTREE), 'merge-base', '--is-ancestor', a, b], env=ENV).returncode == 0

def hashes(path):
    st = path.lstat()
    if stat.S_ISLNK(st.st_mode):
        data = os.readlink(path).encode()
        return len(data), hashlib.sha256(data).hexdigest(), hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()
    sha = hashlib.sha256()
    blob = hashlib.sha1(b'blob ' + str(st.st_size).encode() + b'\0')
    with path.open('rb') as stream:
        for data in iter(lambda: stream.read(1024 * 1024), b''):
            sha.update(data)
            blob.update(data)
    return st.st_size, sha.hexdigest(), blob.hexdigest()

head = git('rev-parse', 'HEAD').decode().strip()
remote_main = git('rev-parse', 'origin/main').decode().strip()
main_head = git('rev-parse', 'main').decode().strip()
base = git('merge-base', head, remote_main).decode().strip()
initial_status = git('status', '--porcelain=v1', '--untracked-files=all').decode()
head_tree, main_tree = tree(head), tree(remote_main)
main_history_objects = {line.split(b' ', 1)[0].decode() for line in git('rev-list', '--objects', remote_main).splitlines()}
object_ids = sorted({item['git_blob'] for item in head_tree.values()})
object_check = git('cat-file', '--batch-check=%(objectname) %(objecttype) %(objectsize)', data=('\n'.join(object_ids) + '\n').encode()).decode().splitlines()
objects = {parts[0]: parts[1:] for parts in map(str.split, object_check)}
object_payloads = {}
reader = subprocess.Popen(['git', '-C', str(WORKTREE), 'cat-file', '--batch'], stdin=subprocess.PIPE, stdout=subprocess.PIPE, env=ENV)
for oid in object_ids:
    reader.stdin.write((oid + '\n').encode())
    reader.stdin.flush()
    header = reader.stdout.readline().decode().split()
    if len(header) != 3 or header[1] != 'blob':
        raise RuntimeError(f'Cannot recover blob {oid}: {header}')
    remaining = int(header[2])
    object_sha256 = hashlib.sha256()
    object_blob = hashlib.sha1(b'blob ' + str(remaining).encode() + b'\0')
    while remaining:
        block = reader.stdout.read(min(1024 * 1024, remaining))
        if not block:
            raise RuntimeError(f'Truncated recovered blob {oid}')
        object_sha256.update(block)
        object_blob.update(block)
        remaining -= len(block)
    if reader.stdout.read(1) != b'\n':
        raise RuntimeError(f'Missing batch delimiter after blob {oid}')
    object_payloads[oid] = dict(sha256=object_sha256.hexdigest(), recovered_git_blob=object_blob.hexdigest())
reader.stdin.close()
if reader.wait() != 0:
    raise RuntimeError('Git object recovery read failed')
tracked = []
for path, item in head_tree.items():
    size, sha256, blob = hashes(WORKTREE / path)
    oid = item['git_blob']
    check = objects[oid]
    tracked.append(dict(path=path, **item, bytes=size, sha256=sha256,
        actual_git_blob=blob, matches_committed_blob=oid == blob,
        object_exists=check[0] == 'blob', object_size=int(check[1]) if check[0] == 'blob' else None,
        recovered_git_object_sha256=object_payloads[oid]['sha256'],
        recovered_git_object_bytes_verified=object_payloads[oid]['sha256'] == sha256 and object_payloads[oid]['recovered_git_blob'] == oid,
        blob_reachable_from_retained_origin_main_history=oid in main_history_objects,
        current_origin_main_same_path_blob=main_tree.get(path, {}).get('git_blob'),
        recover_command=f'git cat-file blob {oid}'))
tracked_name = dump('tracked', tracked)
print(f'Hashed {len(tracked)} tracked files: {sum(row["bytes"] for row in tracked):,} bytes', flush=True)

ignored_paths = zpaths('ls-files', '--others', '--ignored', '--exclude-standard')
untracked_paths = zpaths('ls-files', '--others', '--exclude-standard')
ignored = []
for relative in ignored_paths:
    path = WORKTREE / relative
    size, sha256, blob = hashes(path)
    row = dict(path=relative, bytes=size, type='symlink' if path.is_symlink() else 'file', sha256=sha256, git_blob=blob)
    if path.is_symlink():
        row.update(target=os.readlink(path), resolved_target=str(path.resolve()), target_exists=path.exists(), classification='unclassified symlink')
    elif path.suffix == '.pyc' and path.parent.name == '__pycache__':
        source = Path(importlib.util.source_from_cache(str(path)))
        source_relative = str(source.relative_to(WORKTREE))
        row.update(source=source_relative, source_exists=source.is_file(), source_tracked=source_relative in head_tree,
            source_git_blob=head_tree.get(source_relative, {}).get('git_blob'), classification='Python bytecode requiring source comparison')
        data = path.read_bytes()
        row['matches_current_python_magic'] = data[:4] == importlib.util.MAGIC_NUMBER
        if source.is_file() and row['matches_current_python_magic']:
            original = marshal.loads(data[16:])
            if isinstance(original, types.CodeType):
                opt = int(path.stem.rsplit('.opt-', 1)[1]) if '.opt-' in path.stem else 0
                compiled = compile(source.read_bytes(), original.co_filename, 'exec', dont_inherit=True, optimize=opt)
                row['compiled_code_matches_tracked_source'] = compiled == original
                row['code_original_filename'] = original.co_filename
                if compiled == original and row['source_tracked']:
                    row['classification'] = 'reproducible Python bytecode; compiled code exactly matches tracked source'
    else:
        row['classification'] = 'UNCLASSIFIED; retain before cleanup'
    ignored.append(row)
ignored_name = dump('ignored', ignored)
untracked = []
for relative in untracked_paths:
    size, sha256, blob = hashes(WORKTREE / relative)
    untracked.append(dict(path=relative, bytes=size, sha256=sha256, git_blob=blob, classification='UNCLASSIFIED; retain before cleanup'))
untracked_name = dump('untracked', untracked)

disk_files, empty_dirs = set(), []
for directory, dirs, files in os.walk(WORKTREE, followlinks=False):
    for name in list(dirs):
        path = Path(directory) / name
        if path.is_symlink():
            disk_files.add(str(path.relative_to(WORKTREE)))
            dirs.remove(name)
    for name in files:
        disk_files.add(str((Path(directory) / name).relative_to(WORKTREE)))
    if not dirs and not files:
        empty_dirs.append(str(Path(directory).relative_to(WORKTREE)))
covered_files = set(head_tree) | set(ignored_paths) | set(untracked_paths) | {'.git'}
inventory = dict(file_or_symlink_count=len(disk_files), tracked_count=len(tracked), ignored_count=len(ignored), untracked_count=len(untracked),
    uncovered_files=sorted(disk_files - covered_files), listed_but_absent=sorted(covered_files - disk_files), empty_directories=empty_dirs,
    git_link_control_file=dict(path='.git', contents=(WORKTREE / '.git').read_text().strip(), classification='regenerable linked-worktree pointer; shared Git database retained'))
dump('filesystem-inventory', inventory)

processes, process_errors = [], []
for proc in Path('/proc').iterdir():
    if not proc.name.isdigit():
        continue
    matched = []
    for link in [proc / 'cwd', proc / 'exe', proc / 'root']:
        try:
            target = os.readlink(link)
        except (FileNotFoundError, PermissionError):
            continue
        if target == str(WORKTREE) or target.startswith(str(WORKTREE) + '/'):
            matched.append(dict(path=str(link), target=target))
    try:
        fds = list((proc / 'fd').iterdir())
    except (FileNotFoundError, PermissionError) as exc:
        process_errors.append(dict(pid=proc.name, error=type(exc).__name__))
        fds = []
    for fd in fds:
        try:
            target = os.readlink(fd)
        except (FileNotFoundError, PermissionError):
            continue
        if target == str(WORKTREE) or target.startswith(str(WORKTREE) + '/'):
            matched.append(dict(path=str(fd), target=target))
    if matched:
        processes.append(dict(pid=proc.name, matches=matched))

incoming, scan_errors = [], []
def walk_error(exc):
    scan_errors.append(str(exc))
for scan_root in [ROOT, Path('/workspace/shared/empire-art-rebuild')]:
    for directory, dirs, files in os.walk(scan_root, followlinks=False, onerror=walk_error):
        if Path(directory) == WORKTREE:
            dirs[:] = []
            continue
        dirs[:] = [name for name in dirs if name not in {'.git', '.codex', '.agents', '.aws'}]
        for name in dirs + files:
            path = Path(directory) / name
            if path.is_symlink():
                target = str(path.resolve())
                if target == str(WORKTREE) or target.startswith(str(WORKTREE) + '/'):
                    incoming.append(dict(path=str(path), link_text=os.readlink(path), resolved_target=target))
activity = dict(visible_process_matches=processes, process_scan_errors=process_errors,
    coordinator_task_ownership_confirmation=COORDINATOR_CONFIRMATION,
    process_check_limitation='Tool commands run in an isolated PID namespace. Host-wide cwd/open-file/process inactivity cannot be proven here. Parent must confirm task ownership and external process inactivity.',
    active_agent_inventory=['/root (interrupted)', '/root/inventory_empire_art_rebuild', '/root/inventory_empire_art_rebuild/animate_grayflag_name_registrar', '/root/inventory_empire_art_rebuild/animate_grayflag_scribe', '/root/inventory_empire_art_rebuild/animate_grayflag_wall_laborer', '/root/inventory_empire_art_rebuild/audit_safe_worktree_recovery', '/root/inventory_empire_art_rebuild/redraw_grayflag_chapter_backdrops'],
    agent_inventory_note='Names/statuses from collaboration.list_agents at 2026-10-20T00:45:38Z. Names do not prove cwd or worktree ownership; parent confirmation required.',
    incoming_symlink_references=incoming, incoming_symlink_scan_errors=scan_errors,
    incoming_symlink_scan_roots=[str(ROOT), '/workspace/shared/empire-art-rebuild'],
    incoming_symlink_scan_exclusions=['candidate contents', '.git', '.codex', '.agents', '.aws'])
dump('activity', activity)

missing_commits = git('rev-list', '--topo-order', head, '^' + remote_main).decode().splitlines()
bundle_objects = []
for line in git('rev-list', '--objects', head, '^' + base).decode().splitlines():
    oid, _, path = line.partition(' ')
    kind = git('cat-file', '-t', oid).decode().strip()
    content = git('cat-file', kind, oid)
    bundle_objects.append(dict(object_id=oid, type=kind, path=path, uncompressed_bytes=len(content),
        independent_zlib_bytes=len(zlib.compress(content)), reachable_from_origin_main_history=oid in main_history_objects))
cherry = git('cherry', remote_main, head).decode().splitlines()
changed = []
for path in zpaths('diff-tree', '--no-commit-id', '--name-only', '-r', head):
    row = next(row for row in tracked if row['path'] == path)
    changed.append(row)
different_from_current_main = [row for row in tracked if row['git_blob'] != row['current_origin_main_same_path_blob']]
dump('tracked-different-from-origin-main', different_from_current_main)
bundle = dict(status='assessment only; no bundle or pack file created; no Git refs modified',
    original_head=head, missing_commits_from_origin_main=missing_commits, git_cherry=cherry,
    common_base=base, common_base_is_ancestor_of_original_head=ancestor(base, head), common_base_is_ancestor_of_origin_main=ancestor(base, remote_main),
    matching_main_commit='e101f84fb537b1d1faad1b839e08fad817dde3c1',
    missing_commit_changed_tracked_files=changed, bundle_range=f'{base}..{head}', objects=bundle_objects,
    object_count=len(bundle_objects), uncompressed_object_bytes=sum(row['uncompressed_bytes'] for row in bundle_objects),
    independent_zlib_payload_bytes=sum(row['independent_zlib_bytes'] for row in bundle_objects),
    estimated_bundle_bytes='Approximately 15-25 KiB; object payload raw total 55,009 bytes. Exact pack/bundle size not measured because creation is not authorized.',
    proposed_parent_only_create_command=f'git -C {MAIN} bundle create <retained-audit-path>/rural-original-head.bundle art/rural-export-20261019 ^{base}',
    proposed_verify_command=f'git -C {MAIN} bundle verify <retained-audit-path>/rural-original-head.bundle',
    recovery='A common-base-relative bundle retains the exact original commit, tree hierarchy, and changed blobs. A clone of retained main supplies the prerequisite base; fetching the bundle branch into a new local ref can recreate the original HEAD exactly. It is not standalone without the prerequisite base.',
    required_before_cleanup='Parent authorization of recovery alternative, creation and verification of actual bundle, isolated reconstruction test against a retained-main base, Git checkpoint and remote backup verification, final inactivity/status checks. This assessment does not waive the strict ancestry gate.')
dump('bundle-assessment', bundle)
retained = dict(common_git_database=git('rev-parse', '--git-common-dir').decode().strip(), main_worktree=str(MAIN),
    retained_origin_main=remote_main, retained_main=main_head, source_remote=git('remote', 'get-url', 'origin').decode().strip(),
    source_main_url=f'https://github.com/Tangerg/empire/commit/{remote_main}',
    exact_changed_blob_provenance_commit='e101f84fb537b1d1faad1b839e08fad817dde3c1',
    exact_changed_blob_provenance_url='https://github.com/Tangerg/empire/commit/e101f84fb537b1d1faad1b839e08fad817dde3c1',
    tracked_blob_recovery='Each tracked.json item names its exact Git blob and git cat-file recovery command. All true reachability results use a full independently enumerated origin/main object set.',
    current_local_original_branch='refs/heads/art/rural-export-20261019',
    original_head_recovery_limitation='Exact original commit/tree history is currently reachable from a local branch but not origin/main. Do not delete the branch or treat cherry-equivalence as original-commit backup.',
    unique_untracked_evidence=[], retained_ignored_symlink_targets=[], previous_audit='/workspace/shared/empire-art-rebuild/worktree-recovery-audit',
    comparison_schema=str(MAIN / 'docs/art-rebuild-20261019/approved-v5/worktree-recovery'))
dump('retained-links', retained)

final_status = git('status', '--porcelain=v1', '--untracked-files=all').decode()
allocated_bytes = int(subprocess.check_output(['du', '-s', '-B1', str(WORKTREE)]).split()[0])
mismatches = [row['path'] for row in tracked if not row['matches_committed_blob'] or not row['object_exists'] or row['object_size'] != row['bytes'] or not row['recovered_git_object_bytes_verified']]
unrecoverable = [row['path'] for row in tracked if not row['blob_reachable_from_retained_origin_main_history']]
nonreproducible = [row['path'] for row in ignored if not row['classification'].startswith('reproducible Python bytecode')]
summary = dict(audited_at_utc=AUDITED_AT, timestamp_note='Clock tool UTC used; executor filesystem/system timestamps are approximately eight hours ahead.',
    path=str(WORKTREE), allocated_bytes=allocated_bytes, head=head, branch=git('symbolic-ref', '-q', 'HEAD').decode().strip(),
    main_head=main_head, origin_main_snapshot=remote_main, head_reachable_from_main=ancestor(head, main_head), head_reachable_from_origin_main=ancestor(head, remote_main),
    origin_main_live_remote_verification='Not performed by this audit; parent sole Git writer owns remote verification.',
    tracked_modifications=initial_status.splitlines(), final_status_porcelain=final_status, head_unchanged=git('rev-parse', 'HEAD').decode().strip() == head,
    tracked_files_count=len(tracked), tracked_files_bytes=sum(row['bytes'] for row in tracked), tracked_sha256_and_git_blob_verified=not mismatches,
    tracked_blob_mismatches=mismatches, tracked_blobs_not_in_origin_main_history=unrecoverable,
    tracked_files_different_from_current_main_count=len(different_from_current_main),
    ignored_count=len(ignored), ignored_regular_files_bytes=sum(row['bytes'] for row in ignored if row['type'] == 'file'),
    ignored_classifications=dict(collections.Counter(row['classification'] for row in ignored)), ignored_nonreproducible=nonreproducible,
    nonignored_untracked_count=len(untracked), untracked_nonignored=untracked_paths,
    full_filesystem_inventory_uncovered_files=inventory['uncovered_files'], missing_commits_from_origin_main=missing_commits, cherry_equivalence=cherry,
    bundle_assessment_manifest=f'{NAME}-bundle-assessment.json', incoming_symlink_references=incoming,
    matching_processes_visible_namespace=processes, process_check_limitation=activity['process_check_limitation'],
    coordinator_confirmed_no_active_assigned_tasks=True, coordinator_task_ownership_confirmation=COORDINATOR_CONFIRMATION,
    safe_to_remove_under_requested_gate=False, decision='BLOCKED: original HEAD is not an ancestor of origin/main. All tracked bytes are independently recoverable from retained origin/main history, but exact original commit/tree history must be retained before any alternate cleanup plan. Host-wide inactivity also requires parent confirmation.',
    excluded_paths=[str(MAIN), str(ROOT / 'empire-layout-engineering-20261019'), str(ROOT / 'empire-push-check-20261019'), '/workspace/shared/empire-art-rebuild'],
    tracked_manifest=tracked_name, ignored_manifest=ignored_name, untracked_manifest=untracked_name)
dump('summary', summary)
print(json.dumps(summary, indent=2), flush=True)
