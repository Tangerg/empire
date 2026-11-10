"""Compare complete archived native saves, excluding only official save times."""
import copy
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent
PAIRS = (
    ('native/native-southern-output.json', 'southern-bows/ui-settled.json', 'dc0736f6'),
    ('native/native-dual-output.json', 'enemy-west/after.json', '7325ab46'),
)


def comparison_copy(path):
    result = copy.deepcopy(json.loads(path.read_text()))
    for owner in ('campaign', 'battle'):
        del result[owner]['savedAt']
    return result


results = []
for native, expected, state_hash in PAIRS:
    native_path, expected_path = ROOT / native, ROOT / expected
    if comparison_copy(native_path) != comparison_copy(expected_path):
        raise ValueError(f'Native save differs beyond the two official timestamps: {native}')
    results.append({
        'native': native,
        'expected': expected,
        'completeDocumentEqual': True,
        'excludedFields': ['campaign.savedAt', 'battle.savedAt'],
        'expectedStateHashFromVerifiedReplay': state_hash,
        'nativeByteSha256': hashlib.sha256(native_path.read_bytes()).hexdigest(),
        'expectedByteSha256': hashlib.sha256(expected_path.read_bytes()).hexdigest(),
    })
print(json.dumps({'passed': True, 'comparisons': results}, indent=2))
