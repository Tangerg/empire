#!/usr/bin/env bash
set -euo pipefail
if [[ $# != 1 || "$1" != /* || ! -x "$1" ]]; then
  echo 'Usage: bash tools/godot/smoke-linux-package.sh /absolute/package/Empire.x86_64' >&2
  exit 2
fi
binary="$1"
if [[ "$(uname -sm)" != 'Linux x86_64' ]]; then
  echo 'This package smoke test is only supported on Linux x86_64.' >&2
  exit 2
fi
temporary="$(realpath "${TMPDIR:-/tmp}")"
test -d "$temporary"
run_check() {
  local name="$1" expected_status="$2" expected="$3" setup="$4" data save status=0
  shift 4
  data="$(mktemp -d "$temporary/empire-package-smoke.XXXXXX")"
  mkdir "$data/home" "$data/data" "$data/config" "$data/cache" "$data/empty-path"
  save="$data/data/godot/app_userdata/Empire GrayFlag Migration/grayflag-campaign-autosave.json"
  if [[ "$setup" == pre-existing ]]; then
    mkdir -p "$(dirname "$save")"
    printf 'package refusal sentinel\n' > "$save"
  fi
  (
    cd "$data"
    env -i HOME="$data/home" XDG_DATA_HOME="$data/data" XDG_CONFIG_HOME="$data/config" \
      XDG_CACHE_HOME="$data/cache" TMPDIR="$temporary" LANG=C.UTF-8 PATH="$data/empty-path" \
      /usr/bin/timeout --signal=TERM --kill-after=15s 180 "$binary" --headless "$@"
  ) > "$data/native.log" 2>&1 || status=$?
  cat "$data/native.log"
  if [[ "$status" != "$expected_status" ]] || grep -Eiq '(leaked|segmentation fault|unhandled exception)' "$data/native.log" \
    || { [[ "$expected_status" == 0 ]] && grep -Eiq '(^ERROR:|SCRIPT ERROR:)' "$data/native.log"; }; then
    printf 'Package smoke failed: %s (exit %s); %s\n' "$name" "$status" "$data/native.log" >&2
    return 1
  fi
  if [[ "$setup" == pre-existing ]] && [[ "$(cat "$save")" != 'package refusal sentinel' ]]; then
    printf 'Package diagnostic changed pre-existing data: %s\n' "$save" >&2
    return 1
  fi
  if [[ -n "$expected" ]] && ! grep -Fq "$expected" "$data/native.log"; then
    printf 'Package smoke produced no success marker: %s; %s\n' "$name" "$data/native.log" >&2
    return 1
  fi
  printf 'PACKAGE_SMOKE_OK %s; fresh isolated user data and log: %s\n' "$name" "$data"
}
run_check unknown-mode 2 'Use one --package-check=' fresh -- --package-check=arbitrary-scene
run_check duplicate-mode 2 'Use one --package-check=' fresh -- --package-check=release --package-check=release
run_check existing-data 2 'Diagnostics will not open pre-existing user data.' pre-existing -- --package-check=release
run_check startup 0 '' fresh --quit-after 120
run_check release 0 'RELEASE_DIAGNOSTICS_OK ' fresh -- --package-check=release
