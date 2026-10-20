#!/usr/bin/env bash
set -euo pipefail
source_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$source_dir/../../.." && pwd)"
source "$root/tools/godot/env.sh"
scratch="$(mktemp -d "${TMPDIR:-/tmp}/empire-scenery-mutants.XXXXXXXX")"
cd "$root"
EMPIRE_SCENERY_ORACLE="$scratch/oracle.json" "$root/node_modules/.bin/vite-node" "$source_dir/generate-oracle.mjs"
python3 - "$scratch" <<'PY'
import copy,json,pathlib,sys
p=pathlib.Path(sys.argv[1]); original=json.loads((p/'oracle.json').read_text())
for name in ['frame-contact','frame-shadow','cover-art']:
    value=copy.deepcopy(original)
    frame=next(f for f in value['fields'] if not f['authored'])
    if name=='frame-contact': frame['foreground']=frame['foreground'].replace('translate(-22.12,82.00)', 'translate(-22.12,83.00)',1)
    if name=='frame-shadow': frame['foreground']=frame['foreground'].replace('is-frame-bottom','is-frame-top',1)
    if name=='cover-art': value['covers'][2]['pieces'][0]['markup']=value['covers'][2]['pieces'][0]['markup'].replace('bprop-cover-1.png','bprop-cover-2.png')
    assert value!=original
    (p/f'{name}.json').write_text(json.dumps(value))
PY
for mutant in frame-contact frame-shadow cover-art; do
  if "$GODOT_NET" --headless --path "$root/godot" res://Scenes/SceneryChecks.tscn -- --scenery-oracle="$scratch/$mutant.json" > "$scratch/$mutant.log" 2>&1; then
    echo "FAIL: scenery mutant survived: $mutant"; exit 1
  fi
  case "$mutant" in
    frame-contact) grep -q 'Frame Y' "$scratch/$mutant.log" ;;
    frame-shadow) grep -q 'Frame shadow selector' "$scratch/$mutant.log" ;;
    cover-art) grep -q 'Cover artwork' "$scratch/$mutant.log" ;;
  esac
  echo "Rejected source-oracle mutant: $mutant"
done
