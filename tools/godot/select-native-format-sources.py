#!/usr/bin/env python3
"""Select Git-visible native sources, excluding proven uncompiled documentation archives."""
import json
import os
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET


def git_files(root, *arguments):
    output = subprocess.check_output(["git", "ls-files", "-z", *arguments], cwd=root)
    return {os.fsdecode(name) for name in output.split(b"\0") if name}


def removes_documentation(project):
    # This is the repository's literal archive boundary, not an MSBuild glob interpreter.
    # Conditional or differently expressed exclusions cannot justify dropping sources.
    return any(
        not group.attrib
        and set(item.attrib) == {"Remove"}
        and "docs/**/*.cs" in item.attrib["Remove"].split(";")
        for group in ET.parse(project).getroot().findall("ItemGroup")
        for item in group.findall("Compile")
    )


def compiled_sources(root, projects):
    compiled = set()
    visited = set()
    pending = [(project, "") for project in sorted(
        {project.resolve() for project in projects}, reverse=True)]
    while pending:
        project, configuration = pending.pop()
        if (project, configuration) in visited:
            continue
        visited.add((project, configuration))
        # Evaluation only: no build targets or restore. Follow actual project references
        # so an archived probe reintroduced into an active project cannot remain hidden.
        command = [
            "timeout", "--signal=TERM", "--kill-after=15s",
            os.environ.get("EMPIRE_CHECK_TIMEOUT_SECONDS", "300"),
            str(Path(os.environ["DOTNET_ROOT"]) / "dotnet"), "msbuild", str(project),
            "-nologo", "-verbosity:quiet", "-getItem:Compile,ProjectReference",
            "-getProperty:Configurations,Configuration,TargetFrameworks",
            # Isolated tool harnesses use this same baseline property in their run.sh.
            f"-property:EmpireRoot={root}",
        ]
        if configuration:
            if any(character in configuration for character in ",;=%\r\n"):
                raise ValueError(f"Unsupported configuration in {project}: {configuration!r}")
            command.append(f"-property:Configuration={configuration}")
        result = json.loads(subprocess.check_output(command, cwd=root))
        properties = result["Properties"]
        for name in ("Configurations", "Configuration", "TargetFrameworks"):
            if not isinstance(properties[name], str):
                raise ValueError(f"Invalid evaluated {name} in {project}")
        if properties["TargetFrameworks"]:
            raise ValueError(f"Multi-target project needs archive-boundary review: {project}")
        configuration = properties["Configuration"]
        visited.add((project, configuration))
        configurations = {name.strip() for name in properties["Configurations"].split(";") if name.strip()}
        pending.extend((project, name) for name in sorted(configurations, reverse=True))
        items = result["Items"]
        for kind in ("Compile", "ProjectReference"):
            if not isinstance(items[kind], list):
                raise ValueError(f"Invalid evaluated {kind} list in {project}")
            for item in items[kind]:
                path = Path(item["FullPath"])
                if not path.is_absolute():
                    raise ValueError(f"Non-absolute evaluated {kind} path in {project}: {path}")
                if kind == "Compile":
                    compiled.add(path.resolve())
                else:
                    if any(item.get(name) for name in (
                            "SetConfiguration", "SetTargetFramework", "AdditionalProperties",
                            "GlobalPropertiesToRemove")):
                        raise ValueError(f"Project-reference property overrides need archive-boundary review: {project}")
                    if not path.is_file():
                        raise ValueError(f"Missing evaluated project reference in {project}: {path}")
                    pending.append((path.resolve(), configuration))
    return compiled


def select_sources(root):
    sources = {
        name for name in git_files(root, "--cached", "--others", "--exclude-standard",
                                   "--", "godot/*.cs", "tools/godot/*.cs")
        if (root / name).is_file()
    }
    documentation = Path("godot/docs")
    markers = {
        Path(name).parent for name in git_files(root, "--cached", "--", "godot")
        if Path(name).name == ".gdignore"
        and Path(name).is_relative_to(documentation)
        and (root / name).is_file()
    }
    archives = {
        name for name in sources
        if any(Path(name).is_relative_to(directory) for directory in markers)
    }
    project = root / "godot/Empire.csproj"
    if archives and removes_documentation(project):
        projects = {
            root / name for name in git_files(
                root, "--cached", "--others", "--exclude-standard", "--",
                "godot/*.csproj", "tools/godot/*.csproj")
            if (root / name).is_file()
            and not any(Path(name).is_relative_to(directory) for directory in markers)
        }
        compiled = compiled_sources(root, projects | {project})
        sources -= {name for name in archives if (root / name).resolve() not in compiled}
    if not sources:
        raise ValueError("No native source files found")
    return sorted(sources)


if __name__ == "__main__":
    try:
        sources = select_sources(Path(__file__).resolve().parents[2])
        sys.stdout.buffer.write(b"".join(os.fsencode(name) + b"\0" for name in sources))
    except (OSError, ValueError, KeyError, TypeError, ET.ParseError,
            subprocess.CalledProcessError) as error:
        print(f"Native format source selection failed: {error}", file=sys.stderr)
        if isinstance(error, subprocess.CalledProcessError) and error.output:
            print(os.fsdecode(error.output), file=sys.stderr)
        sys.exit(1)
