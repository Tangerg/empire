Empire desktop distribution: third-party notices
================================================

This directory accompanies the Godot 4.6.3 .NET desktop build and its
self-contained .NET 8.0.32 runtime. Keep it with the distribution.

Godot Engine and GodotSharp
--------------------------
GODOT-LICENSE.txt contains the complete upstream MIT license.
GODOT-COPYRIGHT.txt preserves the complete upstream copyright and third-party
license table, including its font notices and full license bodies.
GODOT-AUTHORS.md is the upstream AUTHORS.md referred to by the Godot license.
GODOTSHARP-NOTICE.txt preserves the shipped managed API package attribution.
Godot source: https://github.com/godotengine/godot/tree/4.6.3-stable

The complete vendor copyright table is retained rather than trying to remove
entries by guessing which optional engine components are linked. Its inclusion
does not claim that every listed optional, editor, or other-platform component
is present in this desktop build.

.NET runtime
------------
DOTNET-LICENSE.txt and DOTNET-THIRD-PARTY-NOTICES.txt are complete, unchanged
copies from Microsoft.NETCore.App.Runtime 8.0.32. The Linux x64, macOS x64,
and macOS arm64 runtime packs contain byte-identical copies of these notices.
DOTNET-PACKAGE-NOTICE.txt preserves additional runtime-package attribution.
.NET source: https://github.com/dotnet/runtime

Scope and maintenance
---------------------
manifest.json maps the notice files to their upstream versions and sources,
and records their byte lengths and SHA-256 hashes. This is technical notice
preservation, not legal advice, a legal audit, or a license for Empire itself.

The inspected project/PCK has no separately bundled TTF, TTC, OTF, or WOFF font
asset. Engine-provided fonts are covered by the full Godot copyright table.
Fonts supplied by the user's operating system are not redistributed here.
If a font or other third-party component is added later, preserve that asset's
own license and copyright notice as part of the same change.

Repository art hashes and generation records establish technical lineage, not
a blanket redistribution-rights determination. Reference screenshots are not
licensed for redistribution by this notice bundle. Existing project artwork,
story content, and original project source are not relicensed by these files.

When changing Godot, GodotSharp, .NET, or bundled assets, refresh the notices
and manifest from the actual replacement components. Do not truncate the
vendor notices or replace them with summaries.
