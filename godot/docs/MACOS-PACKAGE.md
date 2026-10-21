# macOS cross-export: UNTESTED ON MAC

This is a Linux-cross-exported Godot 4.6.3 .NET development package, not a
Mac-validated release. It contains a Universal 2 app executable for Intel x86_64
and Apple Silicon arm64, plus the matching self-contained .NET 8 runtime for each
architecture. A separate Godot editor or .NET SDK should not be needed to run the
package, but actual startup on either kind of Mac has NOT been tested.

## Security and compatibility limits

- Godot's built-in ad-hoc signing is used. There is no Developer ID certificate,
  Apple identity signature, notarization, or notarization ticket. A local static
  inspection checks signature structures, not Apple's signature validation.
- Gatekeeper can block this downloaded package. Do not disable Gatekeeper, remove
  quarantine, or bypass a security warning for this build. A normally distributed
  release requires an explicitly authorized signing/notarization workflow and
  actual Mac validation, neither of which has been done.
- Info.plist declares deployment floors of 10.15 for Intel and 11.0 for Apple
  Silicon. These are deployment metadata, NOT a tested compatibility promise or
  a statement that old macOS versions remain supported by Microsoft. Use a
  currently supported macOS/.NET combination and verify it on the target Mac.
- No Mac GPU rendering, Retina text, mouse/keyboard input, gameplay, save/load,
  signature verification, Gatekeeper behavior, or operating-system version has
  been tested. Linux verification does not establish any of those results.
- The app uses the Compatibility renderer. The original recovered game had no
  audio runtime; silence is expected.

Keep the app bundle intact. Existing saves are not included in the package and
must not be overwritten by build cleanup. The campaign autosave and separate
manual save use Godot's platform application-data location, not this archive.

## Reproduce the cross-export on the verified Linux host

From the source checkout with the existing workspace-local .NET 8 SDK, Godot
4.6.3 .NET editor, and matching official `macos.zip` and ICU export data:

```sh
bash tools/godot/export-macos.sh /absolute/new-output-directory
```

The source checkout must include `distribution/notices`; the helper refuses
missing notices and places them in `THIRD-PARTY-NOTICES` beside the signed app,
without changing anything inside it.

The static guard has small mutation tests:

```sh
python3 tools/godot/verify-macos-package-tests.py
```

The first restore/publish needs the official Microsoft NuGet runtime packages for
`osx-x64` and `osx-arm64`. ETC2/ASTC texture import is enabled because Godot
requires it for Universal and
Apple Silicon exports. The helper does not install an SDK, change system
settings, use Apple credentials, publish externally, or run a Mac executable. It
refuses existing output paths, builds/imports/exports with bounded commands,
exports an app bundle first, archives only after Godot exits, checks the ZIP and
both runtime architectures without extraction, and records a
SHA-256 digest. It does not count static inspection as a macOS smoke test. The
initial direct-ZIP
export was rejected by the static guard because its executable was truncated;
this workflow does not use that path or modify the engine binary.

## Official references

- [Godot 4.6 macOS export](https://docs.godotengine.org/en/4.6/tutorials/export/exporting_for_macos.html)
- [Godot macOS export options](https://docs.godotengine.org/en/4.6/classes/class_editorexportplatformmacos.html)
- [.NET self-contained deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/)
- [.NET runtime identifiers](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog)
- [Current .NET macOS support](https://learn.microsoft.com/en-us/dotnet/core/install/macos)
- [.NET 8 OS support policy](https://github.com/dotnet/core/blob/main/release-notes/8.0/supported-os.md)
