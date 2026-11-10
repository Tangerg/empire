#!/usr/bin/env python3
"""Read-only comparison of this aggregate gate against the accepted fall aggregate and hit stage-coverage reference.

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
BASELINE = ROOT.parents[2] / 'art-archer-hit-20261110/verification/full-native-staged'
SOURCE_COMMIT = '6fa6f8aa12cf4586967955c6898b6dd1409d16ad'
GODOT_TREE = '5023aa4d61b633c0d59c2e15e0e4057505fcf557'
FALL_BASELINE = ROOT.parents[2] / 'art-archer-fall-20261110/verification/full-native'
TERMINAL = ('Current native contracts, replay/save determinism, and application flow passed. '
            'Manual GUI and export acceptance are separate.')
ANSI = re.compile(r'\x1b\[[0-9;]*m')
TRACE = re.compile(r'^\s*AI_TRACE (c01-\d{2}): (\d+) steps, action/event/receipt SHA256 ([0-9a-f]{64})$', re.M)
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
    parser.add_argument('--baseline', type=Path, default=BASELINE)
    parser.add_argument('--hashes', type=Path, default=ROOT / 'frozen-assemblies-sha256.txt')
    parser.add_argument('--frozen-dir', type=Path, help='Optional live frozen-DLL verification')
    parser.add_argument('--runtime-dir', type=Path, help='Optional live runtime-DLL verification')
    args = parser.parse_args(argv)
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
    fall_path = FALL_BASELINE / 'full-native.txt'
    fall = ANSI.sub('', fall_path.read_text())
    fall_status = json.loads((FALL_BASELINE / 'run-status.json').read_text())
    fall_valid = (digest(fall_path) == fall_status['fullLogSha256'] ==
                  '26af520e9ff722c79a182cfacf83e185c19aaf7cb2c56c9a7a949af36bd852e2'
                  and (FALL_BASELINE / 'full-native.exit').read_text().strip() == '0'
                  and TERMINAL in fall.splitlines()
                  and fall_status['executionMode'] == 'single-original-aggregate'
                  and fall_status['sourceCommit'] == '90952ac7a0d74f3f43b7dbeb0971a27df29b6dac'
                  and fall_status['processExitCode'] == 0
                  and fall_status['allOriginalStagesPassed'] == 45)
    log_path = args.log
    current = ANSI.sub('', log_path.read_text())
    exit_path = args.exit_path
    exit_code = exit_path.read_text().strip() if exit_path.exists() else None
    terminal = TERMINAL in current.splitlines()
    complete = exit_code is not None
    traces = TRACE.findall(current)
    baseline_traces = TRACE.findall(baseline)
    played = PLAYED.findall(current)
    baseline_played = PLAYED.findall(baseline)
    old_warnings, new_warnings = warnings(baseline), warnings(current)

    # Stage-specific success markers establish coverage. Silent format/import completion
    # is inferred only from a successful original set -e gate, never from partial output.
    sections = re.split(r'^=== (\d+)/45 (.*?) \(exit 0\) ===\s*$', baseline, flags=re.M)
    current_lines = [normalize_marker(line) for line in current.splitlines()]
    stages = []
    cursor = 0
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
        stages.append({'number': int(number), 'name': name, 'observed': observed,
                       'marker': marker, 'silentStageRequiresAggregateSuccess': marker is None})

    recorded_hashes = {}
    for line in args.hashes.read_text().splitlines():
        expected, relative = line.split(maxsplit=1)
        name = Path(relative).name
        if name in recorded_hashes:
            raise ValueError(f'Duplicate assembly hash: {name}')
        recorded_hashes[name] = expected
    expected_hashes = {
        'Empire.Core.dll': '411cd2da1a064c258e4ce896d26fbe915adbea57f74ad7f35536249ad801bf68',
        'Empire.dll': 'e45a3eb15b357073fec2004dc0474b7b85f595a09f32bb5cc28610a4ea6d5405',
        'GodotSharp.dll': '2142e62894176d7340f50581f8b2fe84e36ae64e1a85f1b2bd7737a7e8a5c8ba',
    }
    live_dll_checks = {}
    for label, directory in [('liveFrozenDlls', args.frozen_dir), ('liveRuntimeDlls', args.runtime_dir)]:
        if directory is not None:
            actual = {name: digest(directory / name) for name in expected_hashes}
            live_dll_checks[label] = {'directory': str(directory), 'sha256': actual,
                                      'matchesRecorded': actual == recorded_hashes}
    source_commit = args.source.read_text().strip()
    builds = re.findall(r'Build succeeded\.\s+(\d+) Warning\(s\)\s+(\d+) Error\(s\)', current)
    errors = [line for line in current.splitlines()
              if re.search(r'^(?:ERROR:|SCRIPT ERROR:)|ObjectDB instances leaked|resources still in use at exit', line)]
    checks = {
        'acceptedStageCoverageBaselineVerified': baseline_valid,
        'acceptedFallAggregateVerified': fall_valid,
        'fallAiTracesIdentical': len(traces) == 16 and traces == TRACE.findall(fall),
        'fallPlayedFlowMarkersIdentical': len(played) == 6 and played == PLAYED.findall(fall),
        'fallNormalizedWarningsIdentical': new_warnings == warnings(fall),
        'aggregateExitZeroAndTerminalMarker': exit_code == '0' and terminal,
        'sixteenOrderedAiTracesIdentical': len(traces) == len(baseline_traces) == 16 and traces == baseline_traces,
        'sixPlayedFlowMarkersIdentical': len(played) == len(baseline_played) == 6 and played == baseline_played,
        'normalizedWarningMultisetIdentical': new_warnings == old_warnings,
        'all45StagesCovered': len(stages) == 45 and all(stage['observed'] for stage in stages),
        'metadata213': 'Godot source metadata: 213 unique script UIDs;' in current,
        'buildsZeroWarningsAndErrors': len(builds) >= 2 and all(pair == ('0', '0') for pair in builds),
        'archivedAssemblyHashRecordMatchesTestedBuild': recorded_hashes == expected_hashes,
        'sourceCommitMatchesTestedBuild': source_commit == SOURCE_COMMIT,
        'noErrorOrLeakDiagnostics': not errors,
    }
    checks.update({label: item['matchesRecorded'] for label, item in live_dll_checks.items()})
    report = {
        'status': 'pending' if not complete else 'passed' if all(checks.values()) else 'failed',
        'currentLog': str(log_path), 'currentLogSha256': digest(log_path), 'exitCode': exit_code,
        'sourceCommit': source_commit, 'testedGodotTree': GODOT_TREE,
        'immediateBaselineLog': str(fall_path), 'immediateBaselineLogSha256': digest(fall_path),
        'immediateBaselineTestedCommit': fall_status['sourceCommit'],
        'immediateBaselineIsSingleProcessAggregate': True,
        'baselineLog': str(baseline_path), 'baselineMode': status['executionMode'],
        'baselineIsSingleProcessAggregate': False, 'checks': checks,
        'aiTraceCount': len(traces), 'independentLegalOrders': sum(int(row[1]) for row in traces),
        'aiTraces': traces, 'playedFlowMarkers': played,
        'warningCount': sum(new_warnings.values()), 'baselineWarningCount': sum(old_warnings.values()),
        'addedWarnings': dict(new_warnings - old_warnings), 'missingWarnings': dict(old_warnings - new_warnings),
        'buildSummaryCount': len(builds), 'recordedAssemblyHashes': recorded_hashes, 'optionalLiveDllChecks': live_dll_checks, 'stages': stages, 'errorDiagnostics': errors,
        'scope': 'Headless contracts and played opening only; no GUI/export/full-campaign acceptance claim.',
    }
    rendered = json.dumps(report, indent=2, ensure_ascii=False) + '\n'
    if args.output:
        args.output.write_text(rendered)
    print(rendered, end='')
    return 2 if not complete else 0 if all(checks.values()) else 1


if __name__ == '__main__':
    raise SystemExit(compare())
