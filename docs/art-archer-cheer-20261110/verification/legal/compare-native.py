import hashlib
import json
import sys
from pathlib import Path
expected, actual = map(Path, sys.argv[1:])
def comparable(path):
    value = json.loads(path.read_text())
    del value['campaign']['savedAt']
    del value['battle']['savedAt']
    return value
if comparable(expected) != comparable(actual):
    raise SystemExit('FAIL complete native save differs')
print(json.dumps({'passed': True, 'comparison': 'Complete official documents excluding only campaign.savedAt and battle.savedAt', 'sha256': {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in (expected, actual)}}, indent=2))
