"""Reject corrupt pixel evidence, duplicate observations and shifted pose boundaries."""
import json
import shutil
import subprocess
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent
cases = ('rgb-hash', 'duplicate-process-frame', 'shifted-cheer-frame')
for case in cases:
    with tempfile.TemporaryDirectory(prefix='archer-cheer-evidence-') as temp:
        destination = Path(temp)
        for path in ROOT.iterdir():
            if path.is_file():
                if path.name in ('verify-native-evidence.py', 'native-cheer-proof.json', 'native-cheer-frames.jsonl'):
                    shutil.copy2(path, destination / path.name)
                else:
                    (destination / path.name).symlink_to(path)
        if case == 'rgb-hash':
            proof_path = destination / 'native-cheer-proof.json'
            proof = json.loads(proof_path.read_text())
            proof['frames'][0]['rgbSha256'] = '0' * 64
            proof_path.write_text(json.dumps(proof))
        else:
            trace_path = destination / 'native-cheer-frames.jsonl'
            traces = [json.loads(line) for line in trace_path.read_text().splitlines()]
            if case == 'duplicate-process-frame':
                traces[42]['processFrame'] = traces[41]['processFrame']
            else:
                next(a for a in traces[47]['actors'] if a['id'] == 3)['frame'] = 0
            trace_path.write_text(''.join(json.dumps(t) + '\n' for t in traces))
        result = subprocess.run(['python', str(destination / 'verify-native-evidence.py')], capture_output=True, text=True)
        assert result.returncode != 0 and 'AssertionError' in result.stderr, (case, result)
        print(f'PASS: {case} rejected with exit {result.returncode}.')
print('PASS: all three controls leave original evidence unchanged.')
