#!/usr/bin/env python3
"""Summarize process sampling, without claiming displayed frames or visual acceptance."""
import json
from pathlib import Path
import sys


def summarize(directory):
    trace = json.loads((directory / 'trace.json').read_text())
    samples = trace['samples']
    process = [row for row in samples if row['stage'] == 'process']
    spans = []
    for row in samples:
        if row['stage'] not in ('start', 'process'):
            continue
        if not spans or spans[-1]['frame'] != row['frame']:
            spans.append({'frame': row['frame'], 'firstSampleMs': row['elapsedMs'],
                          'lastSampleMs': row['elapsedMs'], 'samples': 1})
        else:
            spans[-1]['lastSampleMs'] = row['elapsedMs']
            spans[-1]['samples'] += 1
    deltas = sorted(row['deltaMs'] for row in process)
    assert trace['presented'] == list(range(7)), 'C04 receipt event sequence mismatch'
    assert trace['beforeHash'] == 'cfc58c1a' and trace['afterHash'] == '4ff092cd'
    assert trace['eventsHash'] == '45e9e44a59c57619fb070c4a684b6091645eee8e335f4b6cac612a45bde182a0'
    assert process and deltas[0] > 0
    assert trace['captures'] == 0 or trace['displayDriver'] != 'headless'
    return {
        'run': directory.name, 'mode': trace['mode'], 'displayDriver': trace['displayDriver'],
        'candidate': trace['candidate'] == '1', 'target': trace['target'],
        'targetType': trace['targetType'], 'processSamples': len(process),
        'deltaMinMs': min(deltas), 'deltaMedianMs': deltas[len(deltas)//2], 'deltaMaxMs': max(deltas),
        'presentationElapsedMs': process[-1]['elapsedMs'], 'wallElapsedMs': process[-1]['wallMs'],
        'sampledFrameRuns': spans, 'postDrawSamples': sum(row['stage'] == 'post_draw' for row in samples),
        'captures': trace['captures'], 'receiptHashesAndOrderMatch': True,
    }


if __name__ == '__main__':
    print(json.dumps({'scope': 'New C04 timing-marker experiment. Sampled intervals are not screen exposure durations. Headless excludes rendering and capture cost.',
                      'runs': [summarize(Path(arg)) for arg in sys.argv[1:]]}, indent=2))
