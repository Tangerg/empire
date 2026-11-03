#nullable enable
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Empire.Presentation;

internal static class NativeNodeChecks
{
    internal static HashSet<ulong> OrphanIds()
    {
        // Godot 4.6's typed C# indexer truncates these native 64-bit IDs to int.
        // Read the underlying Variants instead: godotengine/godot#114242.
        using var ids = (Godot.Collections.Array)Node.GetOrphanNodeIds();
        return ids.Select(id => id.AsUInt64()).ToHashSet();
    }
}
