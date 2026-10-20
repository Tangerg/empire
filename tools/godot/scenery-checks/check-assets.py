"""Verify the added cover export is exactly the recovered original, never generated art."""
import hashlib
import json
from pathlib import Path
root = Path(__file__).resolve().parents[3]
source = root / 'packages/story-candidate-01/assets/final-fantasy-v1'
manifest = json.loads((root / 'godot/data/selected-assets.json').read_text())
for topic in ('C01-BPROP-COVER-1', 'C01-BPROP-COVER-2'):
    record = next(asset for asset in manifest['assets'] if asset.get('topicId') == topic)
    original = (source / record['png']).read_bytes()
    native = (root / 'godot/assets' / record['png']).read_bytes()
    assert native == original, f'{topic}: native art differs from original'
    assert hashlib.sha256(original).hexdigest() == record['sha256'], f'{topic}: SHA256 mismatch'
    print(f"Original cover verified: {topic} {record['sha256']}")
