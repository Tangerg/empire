using Godot;
using Directory = System.IO.Directory;
using Path = System.IO.Path;

namespace Empire.Application;

internal static class PackageChecks
{
    internal static bool StartRequestedCheck(SceneTree tree)
    {
        const string prefix = "--package-check=";
        var arguments = OS.GetCmdlineUserArgs().Where(value => value.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        if (arguments.Length == 0) return false;
        var scene = arguments.Length == 1 ? arguments[0][prefix.Length..] switch
        {
            "release" => "ReleaseDiagnostics",
            _ => null,
        } : null;
        if (scene is null)
        {
            GD.PushError("Use one --package-check=release argument.");
            tree.Quit(2);
            return true;
        }
        try { RequireFreshTemporaryData(); }
        catch (Exception error)
        {
            GD.PushError("Refusing packaged diagnostics: " + error.Message);
            tree.Quit(2);
            return true;
        }
        // Official release templates disallow command-line scene overrides.
        // Defer the explicit diagnostic transition until the main scene is mounted.
        Callable.From(() =>
        {
            var error = tree.ChangeSceneToFile($"res://Scenes/{scene}.tscn");
            if (error == Error.Ok) return;
            GD.PushError($"Cannot start packaged diagnostic {scene}: {error}");
            tree.Quit(1);
        }).CallDeferred();
        return true;
    }

    internal static void RequireFreshTemporaryData()
    {
        var configured = System.Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrWhiteSpace(configured) || !Path.IsPathFullyQualified(configured))
            throw new InvalidOperationException("Use the smoke script's fresh temporary XDG_DATA_HOME.");
        var temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(configured).TrimEnd(Path.DirectorySeparatorChar);
        var relative = Path.GetRelativePath(temporary, root);
        var parts = relative.Split(Path.DirectorySeparatorChar);
        if (Path.IsPathFullyQualified(relative) || parts.Length != 2
            || !parts[0].StartsWith("empire-package-smoke.", StringComparison.Ordinal) || parts[1] != "data")
            throw new InvalidOperationException("Diagnostics only accept a generated empire-package-smoke.*/data namespace.");
        var userData = Path.GetFullPath(ProjectSettings.GlobalizePath("user://")).TrimEnd(Path.DirectorySeparatorChar);
        if (!userData.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Godot did not select the isolated user-data namespace.");
        for (var directory = new System.IO.DirectoryInfo(userData); directory is not null && directory.FullName != temporary; directory = directory.Parent)
            if (directory.Exists && (directory.Attributes & System.IO.FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Symlinked user-data ancestors are not allowed.");
        if (Directory.Exists(userData) && Directory.EnumerateFileSystemEntries(userData).Any(entry => Path.GetFileName(entry) != "logs"))
            throw new InvalidOperationException("Diagnostics will not open pre-existing user data.");
    }
}
