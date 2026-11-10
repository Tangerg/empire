#!/usr/bin/env python3
"""Run the unchanged native gate's commands with durable completion boundaries.

This is recovery evidence, not a single-process invocation of verify-native.sh.
Every original stage runs in order; a completed stage is reusable only with its
exit status, log hash, source identity and runtime artifact identity intact.
"""
import argparse
from datetime import datetime, timezone
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys


def digest(data):
    return hashlib.sha256(data).hexdigest()


def stamp():
    return datetime.now(timezone.utc).isoformat()


def write_json(path, value):
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n")
    temporary.replace(path)


def git(repo, *args):
    return subprocess.check_output(["git", "-C", str(repo), *args], text=True).strip()


def source_identity(repo):
    if git(repo, "status", "--porcelain", "--untracked-files=all", "--", "godot", "tools"):
        raise RuntimeError("Godot/tools tracked or untracked source differs from the tested commit")
    return {"commit": git(repo, "rev-parse", "HEAD"),
            "godotTree": git(repo, "rev-parse", "HEAD:godot"),
            "toolsTree": git(repo, "rev-parse", "HEAD:tools"),
            "gateSha256": digest((repo / "tools/godot/verify-native.sh").read_bytes()),
            "runnerSha256": digest(Path(__file__).read_bytes())}


def environment_identity(repo):
    toolchain = Path(os.environ.get("EMPIRE_TOOLCHAIN_ROOT", str(repo.parent / "empire-tools"))).resolve()
    files = [toolchain / "dotnet/dotnet",
             toolchain / "dotnet/sdk/8.0.427/dotnet.dll",
             toolchain / "dotnet/sdk/8.0.427/Roslyn/bincore/csc.dll",
             toolchain / "godot/Godot_v4.6.3-stable_mono_linux_x86_64/Godot_v4.6.3-stable_mono_linux.x86_64",
             Path(sys.executable).resolve()]
    files += [Path(shutil.which(name)).resolve() for name in ("bash", "node")]
    return {"toolchain": str(toolchain),
            "inputs": {key: os.environ.get(key) for key in ("EMPIRE_TOOLCHAIN_ROOT", "EMPIRE_CHECK_TIMEOUT_SECONDS",
                                                            "EMPIRE_REPLAY_TIMEOUT_SECONDS", "EMPIRE_CAMPAIGN_TIMEOUT_SECONDS", "TMPDIR")},
            "toolSha256": {str(path): digest(path.read_bytes()) for path in files}}


def artifacts(repo, output):
    paths = ["godot/.godot/imported", "godot/.godot/mono/temp/bin/Debug",
             "godot/Empire.Core/bin/Debug/net8.0", "godot/Empire.Core.Tests/bin/Debug/net8.0"]
    files = {}
    for relative in paths:
        base = repo / relative
        if base.exists():
            for path in sorted(base.rglob("*")):
                if path.is_file():
                    files[str(path.relative_to(repo))] = digest(path.read_bytes())
    for relative in ("godot/.godot/uid_cache.bin", "godot/.godot/global_script_class_cache.cfg"):
        path = repo / relative
        if path.is_file():
            files[relative] = digest(path.read_bytes())
    fingerprint = digest(json.dumps(files, sort_keys=True).encode())
    manifest = output / "artifacts" / (fingerprint + ".json")
    manifest.parent.mkdir(exist_ok=True)
    if not manifest.exists():
        write_json(manifest, files)
    elif digest(json.dumps(json.loads(manifest.read_text()), sort_keys=True).encode()) != fingerprint:
        raise RuntimeError("A saved artifact manifest changed")
    return {"fileCount": len(files), "sha256": fingerprint, "manifest": str(manifest.relative_to(output))}


def plan_for(repo):
    text = (repo / "tools/godot/verify-native.sh").read_text()
    preflight = text.split("python3 tools/godot/native-process-tests.py", 1)[1].split("# Bound each native process", 1)[0]
    preflight = "python3 tools/godot/native-process-tests.py" + preflight
    stages = [(f"preflight-{index + 1:02d}", line) for index, line in enumerate(preflight.splitlines()) if line.strip()]
    # Keep the exported timeout and dotnet wrapper exactly as authored.
    base_setup = "set -euo pipefail\nsource tools/godot/env.sh\n"
    setup = {"preflight": base_setup,
             "native": base_setup + "export EMPIRE_CHECK_TIMEOUT_SECONDS=" + text.split("export EMPIRE_CHECK_TIMEOUT_SECONDS=", 1)[1].split("# No installation", 1)[0]}
    for line in text.splitlines():
        if line.startswith("dotnet build "):
            stages.append(("build-project" if "godot/Empire.csproj" in line else "build-core-tests", line))
    replay = re.search(r'EMPIRE_CHECK_TIMEOUT_SECONDS="\$\{EMPIRE_REPLAY_TIMEOUT_SECONDS:-1200\}" \\\n  dotnet [^\n]+', text)
    if replay is None:
        raise RuntimeError("Original replay command shape changed")
    stages.append(("core-replay", replay[0]))
    for name in ("objective-outcome-checks", "catalog-boundary-checks"):
        command = f"bash tools/godot/{name}/run.sh"
        if command not in text:
            raise RuntimeError(f"Missing original command: {command}")
        stages.append((name, command))
    editor = re.search(r'import_log="\$\(mktemp [^\n]+\n[^\n]+native-process.sh[^\n]+--import', text)
    if editor is None:
        raise RuntimeError("Original editor import command shape changed")
    stages.append(("editor-import", editor[0]))
    scene_function = re.search(r"run_scene\(\) \{\n.*?\n\}", text, re.S)
    narrative = re.search(r'^export EMPIRE_NARRATIVE_EXPECTATIONS=.*$', text, re.M)
    loop = re.search(r"for scene in ([^;]+); do\n  run_scene \"\$scene\"\ndone", text)
    if not all((scene_function, narrative, loop)):
        raise RuntimeError("Original scene setup or loop shape changed")
    scene_setup = scene_function[0] + "\n" + narrative[0] + "\n"
    for scene in loop[1].split():
        stages.append((scene, scene_setup + f"scene={scene}\nrun_scene \"$scene\""))
    after_loop = text[loop.end():]
    for line in after_loop.splitlines():
        if line.startswith("run_scene ") or " run_scene CampaignPlayedFlowChecks" in line:
            match = re.search(r"run_scene (\w+)", line)
            stages.append((match[1], scene_setup + line))
    if len(stages) != 45 or stages[-1][0] != "CampaignPlayedFlowChecks":
        raise RuntimeError(f"Original gate stage inventory changed: {len(stages)} stages")
    return setup, stages


def run(repo, output):
    output.mkdir(parents=True, exist_ok=True)
    with (output / "runner.lock").open("a") as lock:
        try:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError as error:
            raise RuntimeError("A recovery runner or its stage still holds the execution lock") from error
        setup, stages = plan_for(repo)
        identity = source_identity(repo)
        plan = {"source": identity, "environment": environment_identity(repo), "setup": setup,
                "stages": [{"name": name, "command": command} for name, command in stages],
                "scope": "All original gate commands in original order, with persistent per-stage results. Not a single-process aggregate pass."}
        plan_path = output / "plan.json"
        if plan_path.exists():
            if json.loads(plan_path.read_text()) != plan:
                raise RuntimeError("Plan/source changed; start a distinct recovery run")
        else:
            write_json(plan_path, plan)
        completed = []
        missing = False
        for index, (name, command) in enumerate(stages):
            stage_dir = output / f"{index + 1:02d}-{name}"
            result_path = stage_dir / "result.json"
            if not result_path.exists():
                missing = True
                continue
            if missing:
                raise RuntimeError("Completion records are not a contiguous prefix")
            result = json.loads(result_path.read_text())
            if result["exitCode"] != 0 or result["source"] != identity:
                raise RuntimeError(f"Failed or stale completion at {name}; preserve and investigate it")
            if digest((stage_dir / result["log"]).read_bytes()) != result["logSha256"]:
                raise RuntimeError(f"Log changed after completion: {name}")
            if completed and completed[-1]["artifactsAfter"] != result["artifactsBefore"]:
                raise RuntimeError(f"Artifact continuity broken before {name}")
            for key in ("artifactsBefore", "artifactsAfter"):
                record = result[key]
                saved = json.loads((output / record["manifest"]).read_text())
                if digest(json.dumps(saved, sort_keys=True).encode()) != record["sha256"]:
                    raise RuntimeError(f"Artifact manifest changed: {name}")
            completed.append(result)
        state = artifacts(repo, output)
        if completed and completed[-1]["artifactsAfter"]["sha256"] != state["sha256"]:
            raise RuntimeError("Runtime artifacts changed since the last completed stage; do not reuse this prefix")
        print(f"Verified {len(completed)}/{len(stages)} durable stages", flush=True)
        for index in range(len(completed), len(stages)):
            name, command = stages[index]
            stage_dir = output / f"{index + 1:02d}-{name}"
            stage_dir.mkdir(exist_ok=True)
            attempt = 1 + len(list(stage_dir.glob("attempt-*.json")))
            prefix = f"attempt-{attempt:02d}"
            log_path = stage_dir / (prefix + ".log")
            before = artifacts(repo, output)
            if completed and completed[-1]["artifactsAfter"] != before:
                raise RuntimeError("Runtime artifacts changed between stages")
            if source_identity(repo) != identity:
                raise RuntimeError("Source changed during the run")
            started = stamp()
            pending = {"startedAtUtc": started, "source": identity, "command": command,
                       "artifactsBefore": before, "log": log_path.name, "terminal": False}
            write_json(stage_dir / (prefix + ".json"), pending)
            print(f"START {index + 1:02d}/{len(stages)} {name} {started}", flush=True)
            with log_path.open("wb") as log:
                stage_setup = setup["preflight" if index < 8 else "native"]
                process = subprocess.run(["bash", "-c", stage_setup + command + "\n"], cwd=repo,
                                         stdout=log, stderr=subprocess.STDOUT, pass_fds=(lock.fileno(),))
            after = artifacts(repo, output)
            result = {**pending, "finishedAtUtc": stamp(), "exitCode": process.returncode,
                      "terminal": True, "logSha256": digest(log_path.read_bytes()), "artifactsAfter": after}
            write_json(stage_dir / (prefix + ".json"), result)
            if source_identity(repo) != identity:
                raise RuntimeError("Source changed while a stage was running")
            write_json(stage_dir / "result.json", result)
            print(f"END {index + 1:02d}/{len(stages)} {name} exit={process.returncode}", flush=True)
            if process.returncode != 0:
                return process.returncode
            completed.append(result)
        write_json(output / "complete.json", {"source": identity, "stagesPassed": len(completed),
                   "finishedAtUtc": stamp(), "allOriginalStagesPassed": True,
                   "singleProcessAggregatePass": False,
                   "planSha256": digest(plan_path.read_bytes()), "finalArtifacts": artifacts(repo, output)})
        print("STAGED_NATIVE_RECOVERY_COMPLETE: all 45 original stages exited 0; not a single-process aggregate pass.", flush=True)
        return 0


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args()
    try:
        return run(args.repo.resolve(), args.output_dir.resolve())
    except Exception as error:
        print(f"Recovery blocked: {error}", file=sys.stderr, flush=True)
        return 1


if __name__ == "__main__":
    sys.exit(main())
