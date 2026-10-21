#!/usr/bin/env python3
"""Inspect a cross-exported ZIP without pretending to execute or trust it on macOS."""
import hashlib
import json
from pathlib import Path, PurePosixPath
import plistlib
import stat
import struct
import sys
import zipfile

CPU_NAMES = {0x01000007: "x86_64", 0x0100000C: "arm64"}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def macho_slices(data):
    if data[:4] == b"\xca\xfe\xba\xbe":
        count = struct.unpack_from(">I", data, 4)[0]
        require(count == 2, "Universal executable must have exactly two slices")
        slices = []
        for index in range(count):
            cpu, _, offset, size, _ = struct.unpack_from(">IIIII", data, 8 + index * 20)
            require(offset + size <= len(data), "Mach-O slice is outside file")
            slices.append((cpu, data[offset:offset + size]))
        return slices
    require(data[:4] == b"\xcf\xfa\xed\xfe", "Expected 64-bit little-endian Mach-O")
    return [(struct.unpack_from("<I", data, 4)[0], data)]


def inspect_macho(data):
    result = []
    for cpu, section in macho_slices(data):
        require(section[:4] == b"\xcf\xfa\xed\xfe", "Invalid Mach-O slice header")
        require(struct.unpack_from("<I", section, 4)[0] == cpu, "CPU header mismatch")
        require(cpu in CPU_NAMES, "Unexpected CPU architecture")
        commands = struct.unpack_from("<I", section, 16)[0]
        cursor, has_signature, ad_hoc = 32, False, False
        for _ in range(commands):
            command, length = struct.unpack_from("<II", section, cursor)
            require(length >= 8 and cursor + length <= len(section), "Invalid load command")
            if command == 0x1D:  # LC_CODE_SIGNATURE, presence is not signature verification.
                offset, size = struct.unpack_from("<II", section, cursor + 8)
                require(offset + size <= len(section), "Signature is outside Mach-O")
                signature = section[offset:offset + size]
                require(len(signature) >= 12, "Missing signature superblob")
                magic, _, count = struct.unpack_from(">III", signature)
                require(magic == 0xFADE0CC0, "Invalid signature superblob")
                for index in range(count):
                    _, blob_offset = struct.unpack_from(">II", signature, 12 + index * 8)
                    blob_magic = struct.unpack_from(">I", signature, blob_offset)[0]
                    if blob_magic == 0xFADE0C02:
                        flags = struct.unpack_from(">I", signature, blob_offset + 12)[0]
                        has_signature = True
                        ad_hoc = bool(flags & 2)
            cursor += length
        require(has_signature, "Missing Mach-O CodeDirectory")
        result.append({"architecture": CPU_NAMES[cpu], "code_directory_present": True,
                       "ad_hoc_flag": ad_hoc})
    require(len({row["architecture"] for row in result}) == len(result), "Duplicate CPU slices")
    return result


def inspect_archive(path):
    with zipfile.ZipFile(path) as archive:
        names = archive.namelist()
        require(len(names) == len(set(names)), "Duplicate archive paths")
        for item in archive.infolist():
            name = PurePosixPath(item.filename)
            require(not name.is_absolute() and ".." not in name.parts, "Unsafe archive path")
            require(stat.S_IFMT(item.external_attr >> 16) != stat.S_IFLNK, "Unexpected symlink")
        require(archive.testzip() is None, "ZIP CRC failure")
        apps = {name.split("/")[0] for name in names if ".app/" in name}
        require(len(apps) == 1, "Expected one macOS app bundle")
        app = next(iter(apps))
        contents = app + "/Contents/"
        info = plistlib.loads(archive.read(contents + "Info.plist"))
        require(info["CFBundleIdentifier"] == "org.empire.grayflag.migration", "Unexpected bundle ID")
        executable = contents + "MacOS/" + info["CFBundleExecutable"]
        require(archive.getinfo(executable).external_attr >> 16 & 0o111, "Executable mode missing")
        executable_info = inspect_macho(archive.read(executable))
        require({row["architecture"] for row in executable_info} == {"x86_64", "arm64"}, "Not Universal 2")
        require(all(row["ad_hoc_flag"] for row in executable_info), "Main executable not ad-hoc signed")
        require(contents + "_CodeSignature/CodeResources" in names, "App signature resources missing")
        pcks = [name for name in names if name.startswith(contents + "Resources/") and name.endswith(".pck")]
        require(len(pcks) == 1 and archive.getinfo(pcks[0]).file_size > 0, "Game resource PCK missing")
        runtimes = {}
        for arch in ("x86_64", "arm64"):
            prefix = contents + "Resources/data_Empire_macos_" + arch + "/"
            for required in ("Empire.dll", "Empire.Core.dll", "GodotSharp.dll", "libcoreclr.dylib", "libhostfxr.dylib", "libhostpolicy.dylib", "Empire.runtimeconfig.json"):
                require(prefix + required in names, "Missing " + prefix + required)
            config = json.loads(archive.read(prefix + "Empire.runtimeconfig.json"))["runtimeOptions"]
            require(config.get("includedFrameworks"), "Runtime is not self-contained")
            libraries = [name for name in names if name.startswith(prefix) and name.endswith(".dylib")]
            require(libraries, "Missing native runtime libraries")
            for library in libraries:
                inspected = inspect_macho(archive.read(library))
                require([row["architecture"] for row in inspected] == [arch], "Wrong native runtime architecture: " + library)
            runtimes[arch] = {"native_libraries": len(libraries), "included_frameworks": config["includedFrameworks"]}
        require("README-UNTESTED.md" in names, "Untested notice missing")
    with path.open("rb") as source:
        digest = hashlib.file_digest(source, "sha256").hexdigest()
    return {"archive": str(path), "bytes": path.stat().st_size,
            "sha256": digest,
            "app_bundle": app, "executable": executable_info, "runtimes": runtimes,
            "static_zip_structure": "passed", "macos_execution": "NOT TESTED",
            "macos_codesign_verification": "NOT RUN", "notarization": "NOT PERFORMED",
            "gatekeeper_acceptance": "NOT TESTED", "plist_deployment_floors": info.get("LSMinimumSystemVersionByArchitecture", info.get("LSMinimumSystemVersion"))}


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: python3 tools/godot/verify-macos-package.py /path/to/archive.zip")
    print(json.dumps(inspect_archive(Path(sys.argv[1])), indent=2))
