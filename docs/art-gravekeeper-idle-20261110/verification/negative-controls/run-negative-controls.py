#!/usr/bin/env python3
"""Run isolated gravekeeper idle data mutations against explicitly frozen native assemblies.

Requires GODOT_NET pointing to the existing official Godot Mono executable.
No editor, import, build, production write, or assembly discovery is performed.
The --output directory must not exist and must be outside --repo.
"""

import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess


ASSEMBLIES = ("Empire.dll", "Empire.Core.dll", "GodotSharp.dll")
BINDINGS = Path("data/art-bindings.json")
MANIFEST = Path("data/selected-assets.json")
IDENTITY_ERROR = (
    "Only the generic keeper idle selects the exact four-pose, 2400 ms breathing "
    "appearance and uniform scale."
)
INTERVAL_ERROR = "The four authored anatomical phases occupy their exact half-open intervals."
CASES = (
    ("static-idle-binding", "Cast recovery selects the declared breathing appearance without replacing the canonical gravekeeper sheet.",
     "CheckGravekeeperCastPlayback"),
    ("one-ms-first-to-second", INTERVAL_ERROR, "CheckGravekeeperIdlePlayback"),
    ("repeat-exhale-frame", INTERVAL_ERROR, "CheckGravekeeperIdlePlayback"),
    ("presentation-scale", IDENTITY_ERROR, "CheckGravekeeperIdlePlayback"),
)


def sha(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def file_hashes(root, paths):
    return {str(path.relative_to(root)): sha(path) for path in sorted(paths)}


def inputs(root):
    """Hash every presentation data/resource input, not only mutated JSON."""
    paths = [root / "project.godot"]
    for folder in ("data", "assets", "Scenes", "Presentation", "Application", "tests", ".godot/imported"):
        paths.extend(path for path in (root / folder).rglob("*") if path.is_file())
    return file_hashes(root, paths)


def git(repo, *arguments):
    return subprocess.check_output(["git", "-C", str(repo), *arguments])


def mutate(name, originals):
    bindings = json.loads(originals[BINDINGS])
    manifest = json.loads(originals[MANIFEST])
    if name == "static-idle-binding":
        action = bindings["CANDIDATE_01_UNIT_ART"]["c01.gravekeeper"]["actionClips"]
        assert action["idle"] == "gravekeeper.breathe"
        action["idle"] = "static.idle"
        return BINDINGS, bindings
    frames = manifest["actorClips"]["gravekeeper.breathe"]["frames"]
    assert [frame["span"] for frame in frames] == [700, 500, 500, 700]
    assert [frame["frame"] for frame in frames] == [
        "breathe-rest", "breathe-inhale", "breathe-high", "breathe-exhale"]
    if name == "one-ms-first-to-second":
        frames[0]["span"] -= 1
        frames[1]["span"] += 1
        assert sum(frame["span"] for frame in frames) == 2400
    elif name == "repeat-exhale-frame":
        frames[3]["frame"] = "breathe-high"
    elif name == "presentation-scale":
        appearances = [asset for asset in manifest["assets"] if asset.get("id") == "gravekeeper-breathe-v1"]
        assert len(appearances) == 1 and appearances[0]["presentationScale"] == 0.0792
        appearances[0]["presentationScale"] = 0.0791
    else:
        raise ValueError(name)
    return MANIFEST, manifest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--assembly-dir", required=True, type=Path)
    parser.add_argument("--expected-commit", required=True,
                        help="Exact tested source commit supplied with the frozen DLL handoff")
    args = parser.parse_args()
    repo = args.repo.resolve(strict=True)
    commit = git(repo, "rev-parse", "HEAD").decode().strip()
    if commit != args.expected_commit:
        parser.error("HEAD must equal the explicitly supplied tested commit")
    frozen = args.assembly_dir.resolve(strict=True)
    output = args.output.resolve()
    if output.exists() or output == repo or repo in output.parents:
        parser.error("--output must be a new directory outside --repo")
    if output == frozen or frozen in output.parents or output in frozen.parents:
        parser.error("--output and --assembly-dir must be disjoint")
    godot = Path(os.environ.get("GODOT_NET", "")).resolve()
    if not godot.is_file() or not os.access(godot, os.X_OK):
        parser.error("GODOT_NET must name an existing executable")
    for name in ASSEMBLIES:
        if not (frozen / name).is_file():
            parser.error(f"Missing explicitly frozen assembly: {frozen / name}")
    source = repo / "godot"
    if not (source / ".godot").is_dir():
        parser.error("The source must already have an imported runtime cache")

    output.mkdir(parents=True)
    stage = output / "project"
    stage.mkdir()
    summary = {"status": "preparing", "repo": str(repo), "stage": str(stage),
               "assemblyDirectory": str(frozen), "cases": [],
               "scriptSha256": sha(Path(__file__)), "godotSha256": sha(godot),
               "strictGateSha256": sha(repo / "tools/godot/native-process.sh")}
    summary_path = output / "summary.json"

    def save_summary():
        summary_path.write_text(json.dumps(summary, indent=2) + "\n")

    source_before = inputs(source)
    frozen_before = file_hashes(frozen, [frozen / name for name in ASSEMBLIES])
    diff = git(repo, "diff", "HEAD", "--binary")
    (output / "source-working-tree.diff").write_bytes(diff)
    summary["source"] = {
        "commit": git(repo, "rev-parse", "HEAD").decode().strip(),
        "committedTree": git(repo, "rev-parse", "HEAD^{tree}").decode().strip(),
        "status": git(repo, "status", "--porcelain=v1").decode(),
        "workingDiffSha256": hashlib.sha256(diff).hexdigest(),
        "csharpFiles": file_hashes(source, [p for p in source.rglob("*.cs")
            if not any(part in {".godot", "obj", "bin"} for part in p.relative_to(source).parts)]),
    }
    summary["sourceInputsBefore"] = source_before
    summary["frozenAssembliesBefore"] = frozen_before
    for path in source.iterdir():
        destination = stage / path.name
        if path.name in {"data", ".godot"}:
            shutil.copytree(path, destination, symlinks=False)
        else:
            # Runtime only reads these resources; never chmod or write their targets.
            destination.symlink_to(path, target_is_directory=path.is_dir())
    summary["readOnlyResourceLinks"] = {str(path.relative_to(stage)): str(path.resolve())
        for path in stage.iterdir() if path.is_symlink()}
    runtime = stage / ".godot/mono/temp/bin/Debug"
    for name in ASSEMBLIES:
        shutil.copyfile(frozen / name, runtime / name)
    staged_before = file_hashes(runtime, [runtime / name for name in ASSEMBLIES])
    (output / "frozen-assemblies-sha256.txt").write_text(
        "".join(f"{digest}  {name}\n" for name, digest in frozen_before.items()))
    assert staged_before == frozen_before, "Staged DLLs must equal the explicit frozen DLLs"
    summary["stagedAssembliesBefore"] = staged_before
    summary["stageInputsBefore"] = inputs(stage)
    assert summary["stageInputsBefore"] == source_before
    originals = {relative: (stage / relative).read_bytes() for relative in (BINDINGS, MANIFEST)}

    def restore():
        for relative, data in originals.items():
            (stage / relative).write_bytes(data)
        assert all((stage / relative).read_bytes() == data for relative, data in originals.items())

    def run_case(name, expected=None, expected_method=None):
        env = os.environ.copy()
        for variable in ("HOME", "DOTNET_CLI_HOME", "XDG_DATA_HOME", "XDG_CONFIG_HOME", "XDG_CACHE_HOME", "XDG_RUNTIME_DIR"):
            directory = output / "runtime-environment" / name / variable.lower()
            directory.mkdir(parents=True)
            env[variable] = str(directory)
        log = output / f"{name}.log"
        wrapper = output / f"{name}-gate.log"
        command = ["bash", str(repo / "tools/godot/native-process.sh"), str(log), name,
                   str(godot), "--headless", "--path", str(stage), "res://Scenes/AnimationChecks.tscn"]
        record = {"name": name, "expectedError": expected, "expectedMethod": expected_method, "command": command,
                  "mutatedInputs": file_hashes(stage, [stage / BINDINGS, stage / MANIFEST])}
        summary["cases"].append(record)
        save_summary()
        with wrapper.open("w") as stream:
            result = subprocess.run(command, env=env, stdout=stream, stderr=subprocess.STDOUT, check=False)
        text = log.read_text()
        errors = [line for line in text.splitlines() if line.startswith(("ERROR:", "SCRIPT ERROR:"))]
        record.update(gateExit=result.returncode, errors=errors,
                      logSha256=sha(log), gateLogSha256=sha(wrapper))
        save_summary()
        if expected is not None:
            assert result.returncode == 1, f"{name}: expected strict diagnostic gate exit 1"
            assert errors and errors[0] == f"ERROR: System.InvalidOperationException: {expected}", f"{name}: wrong first failure: {errors}"
            first_error_block = text[text.index(errors[0]):].split("C# backtrace", 1)[0]
            assert f"AnimationChecks.{expected_method}()" in first_error_block, f"{name}: failure came from the wrong test"
            assert "Animation presentation checks passed:" not in text, f"{name}: mutant passed"
        else:
            assert result.returncode == 0 and not errors, f"{name}: restored positive failed"
            assert "Animation presentation checks passed:" in text, f"{name}: missing full-suite completion"
        record["passed"] = True
        save_summary()

    try:
        save_summary()
        summary["status"] = "running"
        for name, expected, expected_method in CASES:
            restore()
            relative, changed = mutate(name, originals)
            try:
                (stage / relative).write_text(json.dumps(changed, indent=2) + "\n")
                run_case(name, expected, expected_method)
            finally:
                restore()
            restored = file_hashes(stage, [stage / BINDINGS, stage / MANIFEST])
            expected_restored = {str(relative): hashlib.sha256(data).hexdigest()
                for relative, data in originals.items()}
            summary["cases"][-1]["restoredInputs"] = restored
            assert restored == expected_restored, f"{name}: restored input hashes differ"
            save_summary()
        run_case("restored-positive")
        summary["status"] = "passed"
    except BaseException as error:
        summary["status"] = "failed"
        summary["failure"] = f"{type(error).__name__}: {error}"
        raise
    finally:
        restore()
        summary["stageInputsAfter"] = inputs(stage)
        summary["sourceInputsAfter"] = inputs(source)
        summary["frozenAssembliesAfter"] = file_hashes(frozen, [frozen / name for name in ASSEMBLIES])
        summary["stagedAssembliesAfter"] = file_hashes(runtime, [runtime / name for name in ASSEMBLIES])
        summary["restoredInputsMatch"] = summary["stageInputsBefore"] == summary["stageInputsAfter"]
        summary["productionInputsUnchanged"] = source_before == summary["sourceInputsAfter"]
        summary["assembliesUnchanged"] = frozen_before == summary["frozenAssembliesAfter"] == summary["stagedAssembliesAfter"]
        summary["sourceCommitAfter"] = git(repo, "rev-parse", "HEAD").decode().strip()
        summary["sourceCommitUnchanged"] = summary["sourceCommitAfter"] == args.expected_commit
        intact = (summary["restoredInputsMatch"] and summary["productionInputsUnchanged"]
                  and summary["assembliesUnchanged"] and summary["sourceCommitUnchanged"])
        if not intact:
            summary["status"] = "failed"
            summary["failure"] = "Input/assembly integrity check failed"
        save_summary()
        assert intact, "Input/assembly integrity check failed"
    print(json.dumps({"status": summary["status"], "summary": str(summary_path)}, indent=2))


if __name__ == "__main__":
    main()
