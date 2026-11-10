#!/usr/bin/env python3
"""Additional read-only admission for staged results and dependency identity.

The environment baseline is created explicitly once. It is not retrospective
proof of dependencies that were not fingerprinted by the initial runner.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import shutil
import sys


def sha(data):
    return hashlib.sha256(data).hexdigest()


def validate_records(run):
    plan = json.loads((run / "plan.json").read_text())
    count = 0
    missing = False
    for index, stage in enumerate(plan["stages"], 1):
        directory = run / f"{index:02d}-{stage['name']}"
        path = directory / "result.json"
        if not path.exists():
            missing = True
            continue
        if missing:
            raise ValueError("Completion records are not a contiguous prefix")
        result = json.loads(path.read_text())
        if result.get("terminal") is not True or result.get("exitCode") != 0:
            raise ValueError(f"Stage {index} is not a successful terminal result")
        if result.get("command") != stage["command"]:
            raise ValueError(f"Stage {index} command differs from the original plan")
        if result.get("source") != plan["source"]:
            raise ValueError(f"Stage {index} source differs from the original plan")
        if sha((directory / result["log"]).read_bytes()) != result["logSha256"]:
            raise ValueError(f"Stage {index} log changed")
        count += 1
    return count


def dependency_snapshot(run):
    plan = json.loads((run / "plan.json").read_text())
    toolchain = Path(plan["environment"]["toolchain"])
    files = {}
    for directory in (toolchain / "dotnet", toolchain / "godot"):
        for path in sorted(directory.rglob("*")):
            if path.is_file():
                files[str(path)] = sha(path.read_bytes())
    for program in ("python3", "timeout", "bash", "node"):
        path = Path(shutil.which(program)).resolve()
        files[f"resolved:{program}:{path}"] = sha(path.read_bytes())
    keys = ("PATH", "TMPDIR", "EMPIRE_TOOLCHAIN_ROOT", "EMPIRE_CHECK_TIMEOUT_SECONDS",
            "EMPIRE_REPLAY_TIMEOUT_SECONDS", "EMPIRE_CAMPAIGN_TIMEOUT_SECONDS")
    environment = {key: sha(value.encode()) for key, value in os.environ.items()
                   if key in keys or key.startswith(("DOTNET_", "CORECLR_", "COMPlus_", "MSBUILD", "MSBuild", "MONO_", "LD_"))}
    return {"fileSha256": files, "inheritedRuntimeEnvironmentValueSha256": environment}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run", type=Path, required=True)
    parser.add_argument("--initialize-environment", action="store_true")
    parser.add_argument("--records-only", action="store_true",
                        help="Check archived command/terminal/source/log records without asserting current dependency identity.")
    args = parser.parse_args()
    run = args.run.resolve()
    try:
        count = validate_records(run)
        if args.records_only:
            if args.initialize_environment:
                raise ValueError("Record-only review cannot initialize an environment baseline")
            print(f"PASS: {count} completed records have exact planned commands, successful terminals and original log/source identity. Records only; runtime dependencies were not rechecked.")
            return 0
        snapshot = dependency_snapshot(run)
        baseline = run / "expanded-dependency-baseline.json"
        if args.initialize_environment:
            if baseline.exists():
                raise ValueError("Expanded dependency baseline already exists; it cannot be overwritten")
            value = {"firstRecordedAtUtc": datetime.now(timezone.utc).isoformat(),
                     "completedStagesAtFirstSnapshot": count,
                     "scope": "All persistent .NET and Godot installation files, actual resolved Python/timeout/bash/node binaries, and hashed inherited runtime overrides. Earlier unrecorded dependency equality is not asserted.",
                     "snapshot": snapshot}
            baseline.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n")
        if not baseline.exists() or json.loads(baseline.read_text())["snapshot"] != snapshot:
            raise ValueError("Expanded dependency identity changed or has no initialized baseline")
        print(f"PASS: {count} completed records have exact planned commands, successful terminals and original log/source identity; expanded dependencies unchanged.")
        return 0
    except Exception as error:
        print(f"Staged admission blocked: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
