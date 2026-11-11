#!/usr/bin/env python3
"""Read-only comparison of this aggregate gate against the accepted gravekeeper idle aggregate and hit stage-coverage reference.

Exit 0: complete and equivalent; 1: completed but mismatched; 2: gate still pending.
No Godot process, rebuild, or test is launched. Assertion totals are not scenario counts.
"""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parent
# Frozen implementation and native-capture build identities; later documentation commits do not replace them.
SOURCE_COMMIT = '27de886c111600c64a868f9d57fc6868a6db45e4'
GODOT_TREE = '02b18b80aff45880bb58e503574b858c921449aa'
TOOLS_TREE = 'afff6c24fcf143fdff3a5a4facf2f373d1ad3290'
EXPECTED_HASHES = {
    'Empire.Core.dll': 'ab6a38e4b863a6a09acce6505aac93a4fe72975f0c9e41329d1b8645a7c56faa',
    'Empire.dll': '9c26ba406a55703a0d5a0b07141cfc094ece56b0842157ec47a03d465f0ec9c0',
    'GodotSharp.dll': '2142e62894176d7340f50581f8b2fe84e36ae64e1a85f1b2bd7737a7e8a5c8ba',
}
IMMEDIATE_SOURCE = '869078976dd21f5d82932dc4151bdd03fa973c1a'
IMMEDIATE_LOG_SHA256 = '70bf3ca37b793e52fb2f42b5f87b5010a73d316448e0c6982cf44fc648ebe410'
GATE_SHA256 = '4ca078f295e2b3159c0d533afe94f5a5bae146d91a325c4872658166e7e2ab83'
EXPECTED_UID_COUNT = 215
TERMINAL = ('Current native contracts, replay/save determinism, and application flow passed. '
            'Manual GUI and export acceptance are separate.')
ANSI = re.compile(r'\x1b\[[0-9;]*m')
TRACE = re.compile(r'^\s*AI_TRACE (c01-\d{2}): (\d+) steps, action/event/receipt SHA256 ([0-9a-f]{64})$', re.M)
PROGRESS = re.compile(r'^(?:CARRIED_PLAY_PROGRESS|CARRIED_ROSTER).*$', re.M)
PLAYED = re.compile(r'^(?:PLAYED_AI_DEFEAT_OK|PLAYED_TERMINAL|CARRIED_VICTORY_OK|CARRIED_OPENING_OK).*$', re.M)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def normalize_marker(line):
    line = re.sub(r'\b\d+ assertions\b', '<assertions>', line)
    return re.sub(r'\b\d+ unique script UIDs\b', '<uids>', line)


def warnings(text):
    return Counter(re.sub(r'/tmp/empire-native-check-data\.[^/\s]+', '<save-root>', line)
                   for line in text.splitlines() if line.startswith('WARNING:'))


def compare(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, help='Optional JSON report destination')
    parser.add_argument('--log', type=Path, default=ROOT / 'full-native.txt')
    parser.add_argument('--exit', dest='exit_path', type=Path, default=ROOT / 'full-native.exit')
    parser.add_argument('--source', type=Path, default=ROOT / 'full-native-source-commit.txt')
    parser.add_argument('--source-status', type=Path, default=ROOT / 'full-native-source-status.txt')
    parser.add_argument('--reference-root', type=Path, help='Empire repository containing accepted evidence under docs; auto-detected when archived in that repository')
    parser.add_argument('--baseline', type=Path, help='Override staged 45-stage coverage provenance only; never the immediate idle baseline')
    parser.add_argument('--hashes', type=Path, default=ROOT / 'frozen-assemblies-sha256.txt')
    parser.add_argument('--frozen-dir', type=Path, help='Optional live frozen-DLL verification')
    parser.add_argument('--runtime-dir', type=Path, help='Optional live runtime-DLL verification')
    args = parser.parse_args(argv)
    reference_root = args.reference_root or next((p for p in ROOT.parents if (p / 'godot').is_dir() and (p / 'tools/godot').is_dir()), None)
    if reference_root is None:
        parser.error('--reference-root is required for evidence outside the repository')
    args.baseline = args.baseline or reference_root / 'docs/art-archer-hit-20261110/verification/full-native-staged'
    immediate_baseline = reference_root / 'docs/art-gravekeeper-idle-20261110/verification/full-native'
    baseline_path = args.baseline / 'full-native-staged.txt'
    baseline = ANSI.sub('', baseline_path.read_text())
    status = json.loads((args.baseline / 'full-native-staged-status.json').read_text())
    baseline_valid = (digest(baseline_path) == status['fullLogSha256']
                      and (args.baseline / 'runner.exit').read_text().strip() == '0'
                      and status['allOriginalStagesPassed']
                      and len(status['stages']) == 45
                      and all(stage['exitCode'] == 0 for stage in status['stages']))
    plan = json.loads((args.baseline / 'plan.json').read_text())
    completion = json.loads((args.baseline / 'complete.json').read_text())
    baseline_valid = (baseline_valid and digest(args.baseline / 'plan.json') == status['planSha256']
                      and completion['planSha256'] == status['planSha256']
                      and completion['source'] == plan['source']
                      and completion['allOriginalStagesPassed'] and completion['stagesPassed'] == 45
                      and plan['source']['commit'] == status['testedCommit']
                      and plan['source']['gateSha256'] == status['originalGateSha256'])
    for stage in status['stages']:
        directory = args.baseline / f"{stage['stage']:02d}-{stage['name']}"
        result = json.loads((directory / 'result.json').read_text())
        baseline_valid = (baseline_valid and result['terminal'] and result['exitCode'] == 0
                          and result['source'] == plan['source']
                          and digest(directory / result['log']) == result['logSha256'] == stage['logSha256'])
    immediate_path = immediate_baseline / 'full-native.txt'
    immediate = ANSI.sub('', immediate_path.read_text())
    immediate_status = json.loads((immediate_baseline / 'comparison-archive-only.json').read_text())
    immediate_valid = (digest(immediate_path) == immediate_status['currentLogSha256'] == IMMEDIATE_LOG_SHA256
                       and (immediate_baseline / 'full-native.exit').read_text().strip() == '0'
                       and (immediate_baseline / 'full-native-source-commit.txt').read_text().strip() == IMMEDIATE_SOURCE
                       and TERMINAL in immediate.splitlines()
                       and immediate_status['status'] == 'passed' and immediate_status['exitCode'] == '0'
                       and immediate_status['sourceCommit'] == IMMEDIATE_SOURCE
                       and immediate_status['checks']['all45StagesCovered']
                       and all(immediate_status['checks'].values()))
    log_path = args.log
    current = ANSI.sub('', log_path.read_text()) if log_path.exists() else ''
    exit_path = args.exit_path
    exit_code = exit_path.read_text().strip() if exit_path.exists() else None
    terminal = TERMINAL in current.splitlines()
    complete = exit_code is not None
    traces = TRACE.findall(current)
    played = PLAYED.findall(current)
    old_warnings, new_warnings = warnings(immediate), warnings(current)

    # Stage-specific success markers establish coverage. Silent format/import completion
    # is inferred only from a successful original set -e gate, never from partial output.
    sections = re.split(r'^=== (\d+)/45 (.*?) \(exit 0\) ===\s*$', baseline, flags=re.M)
    current_lines = [normalize_marker(line) for line in current.splitlines()]
    stages = []
    immediate_lines = [normalize_marker(line) for line in immediate.splitlines()]
    cursor = 0
    immediate_cursor = 0
    for index in range(1, len(sections), 3):
        number, name, body = sections[index:index + 3]
        candidates = [line for line in body.splitlines()
                      if 'passed' in line.lower() or line.startswith('PASS')
                      or re.match(r'^\w+_OK\b', line) or line == 'OK'
                      or line.startswith('Godot source metadata:') or line == 'Build succeeded.']
        marker = normalize_marker(candidates[-1]) if candidates else None
        if marker is None:
            observed = exit_code == '0' and terminal
        else:
            try:
                cursor = current_lines.index(marker, cursor) + 1
                observed = True
            except ValueError:
                observed = False
        if marker is None:
            immediate_observed = immediate_valid
        else:
            try:
                immediate_cursor = immediate_lines.index(marker, immediate_cursor) + 1
                immediate_observed = True
            except ValueError:
                immediate_observed = False
        stages.append({'number': int(number), 'name': name, 'observed': observed,
                       'immediateBaselineObserved': immediate_observed,
                       'marker': marker, 'silentStageRequiresAggregateSuccess': marker is None})

    recorded_hashes = {}
    if args.hashes.exists():
        for line in args.hashes.read_text().splitlines():
            expected, relative = line.split(maxsplit=1)
            name = Path(relative).name
            if name in recorded_hashes:
                raise ValueError(f'Duplicate assembly hash: {name}')
            if re.fullmatch(r'[0-9a-f]{64}', expected) is None:
                raise ValueError(f'Malformed assembly hash: {name}')
            recorded_hashes[name] = expected
    elif complete:
        raise FileNotFoundError(f'Completed gate is missing its frozen assembly hash record: {args.hashes}')
    live_dll_checks = {}
    for label, directory in [('liveFrozenDlls', args.frozen_dir), ('liveRuntimeDlls', args.runtime_dir)]:
        if directory is not None:
            if EXPECTED_HASHES is None:
                raise ValueError('Frozen build hashes are not pinned yet; live byte checks cannot run.')
            actual = {name: digest(directory / name) for name in EXPECTED_HASHES}
            live_dll_checks[label] = {'directory': str(directory), 'sha256': actual,
                                      'matchesRecorded': actual == recorded_hashes}
    source_commit = args.source.read_text().strip() if args.source.exists() else None
    clean_source = args.source_status.exists() and args.source_status.read_text() == ''
    builds = re.findall(r'Build succeeded\.\s+(\d+) Warning\(s\)\s+(\d+) Error\(s\)', current)
    errors = [line for line in current.splitlines()
              if re.search(r'^(?:ERROR:|SCRIPT ERROR:)|ObjectDB instances leaked|resources still in use at exit', line)]
    checks = {
        'acceptedStageCoverageBaselineVerified': baseline_valid,
        'acceptedIdleAggregateVerified': immediate_valid and len(stages) == 45
            and all(stage['immediateBaselineObserved'] for stage in stages),
        'aggregateExitZeroAndTerminalMarker': exit_code == '0' and terminal,
        'sixteenOrderedAiTracesIdentical': len(traces) == 16 and traces == TRACE.findall(immediate)
            and [row[0] for row in traces] == [f'c01-{i:02}' for i in range(1, 17)]
            and sum(int(row[1]) for row in traces) == 3425,
        'sixPlayedFlowMarkersIdentical': len(played) == 6 and played == PLAYED.findall(immediate),
        'fourteenProgressAndRosterMarkersIdentical': len(PROGRESS.findall(current)) == 14
            and PROGRESS.findall(current) == PROGRESS.findall(immediate),
        'normalizedWarningMultisetIdentical': new_warnings == old_warnings,
        'all45StagesCovered': len(stages) == 45 and all(stage['observed'] for stage in stages),
        'metadata215': re.findall(r'Godot source metadata: (\d+) unique script UIDs;', current) == [str(EXPECTED_UID_COUNT)],
        'originalGateScriptUnchanged': digest(reference_root / 'tools/godot/verify-native.sh') == GATE_SHA256,
        'testedBuildIdentitiesPinned': SOURCE_COMMIT is not None and GODOT_TREE is not None
            and TOOLS_TREE is not None and EXPECTED_HASHES is not None,
        'buildsZeroWarningsAndErrors': len(builds) >= 2 and all(pair == ('0', '0') for pair in builds),
        'archivedAssemblyHashRecordMatchesTestedBuild': EXPECTED_HASHES is not None and recorded_hashes == EXPECTED_HASHES,
        'sourceCommitMatchesTestedBuild': SOURCE_COMMIT is not None and source_commit == SOURCE_COMMIT,
        'sourceWasCleanAtGateLaunch': clean_source,
        'noErrorOrLeakDiagnostics': not errors,
    }
    checks.update({label: item['matchesRecorded'] for label, item in live_dll_checks.items()})
    report = {
        'status': 'pending' if not complete else 'passed' if all(checks.values()) else 'failed',
        'currentLog': str(log_path), 'currentLogSha256': digest(log_path) if log_path.exists() else None, 'exitCode': exit_code,
        'sourceCommit': source_commit, 'testedGodotTree': GODOT_TREE, 'testedToolsTree': TOOLS_TREE,
        'immediateBaselineLog': str(immediate_path), 'immediateBaselineLogSha256': digest(immediate_path),
        'immediateBaselineTestedCommit': IMMEDIATE_SOURCE,
        'immediateBaselineIsSingleProcessAggregate': True,
        'stageCoverageReferenceLog': str(baseline_path), 'stageCoverageReferenceMode': status['executionMode'],
        'stageCoverageReferenceIsSingleProcessAggregate': False, 'checks': checks,
        'aiTraceCount': len(traces), 'independentLegalOrders': sum(int(row[1]) for row in traces),
        'aiTraces': traces, 'playedFlowMarkers': played,
        'progressAndRosterMarkers': PROGRESS.findall(current),
        'warningCount': sum(new_warnings.values()), 'baselineWarningCount': sum(old_warnings.values()),
        'addedWarnings': dict(new_warnings - old_warnings), 'missingWarnings': dict(old_warnings - new_warnings),
        'buildSummaryCount': len(builds), 'recordedAssemblyHashes': recorded_hashes, 'optionalLiveDllChecks': live_dll_checks, 'stages': stages, 'errorDiagnostics': errors,
        'assertionTotalsAreScenarioCounts': False,
        'scope': 'Headless contracts and played opening only; no GUI/export/full-campaign acceptance claim.',
    }
    rendered = json.dumps(report, indent=2, ensure_ascii=False) + '\n'
    if args.output:
        args.output.write_text(rendered)
    print(rendered, end='')
    return 2 if not complete else 0 if all(checks.values()) else 1


if __name__ == '__main__':
    raise SystemExit(compare())
