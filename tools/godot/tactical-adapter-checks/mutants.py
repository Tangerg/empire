"""Compile isolated adapter mutants; production sources are never changed."""
import pathlib
import subprocess
import sys
import tempfile

root = pathlib.Path(__file__).resolve().parents[3]
source = (root / "godot/Application/GrayFlagSession.Tactical.cs").read_text()
mutants = {
    "outside terrain admitted": ('return !terrain.GetProperty("tags").EnumerateArray().Any(tag => tag.ValueEquals("outside"));', 'return true;'),
    "stale snapshot": ('if (session.revision != stamp.Revision)', 'if (false)'),
    "recruit cards collapsed into legal options": ('session.engine.Recruitment.Cards(state, Coord(at))', 'session.engine.Recruitment.Options(state, Coord(at))'),
    "lost raw terrain defense": ('terrain["defense"]!.GetValue<double>()', '0'),
    "lost authored direction order": ('grid.Directions.Select(direction', 'grid.Directions.Reverse().Select(direction'),
    "current player substituted for viewer": ('Integer(RequireVisible(unit), "owner"), Viewer(state))', 'Integer(RequireVisible(unit), "owner"), Integer(state, "currentPlayer"))'),
    "hidden unit leaked": ('unit is null || !visible.Contains(Integer(unit, "id"))', 'unit is null'),
}
with tempfile.TemporaryDirectory(prefix="empire-adapter-mutants-") as directory:
    for name, (old, new) in mutants.items():
        assert source.count(old) == 1, name
        path = pathlib.Path(directory) / "Adapter.cs"
        path.write_text(source.replace(old, new))
        result = subprocess.run([
            "dotnet", "run", "--project", str(root / "tools/godot/tactical-adapter-checks"),
            "-p:AdapterSource=" + str(path), "--", sys.argv[1],
        ], cwd=root, capture_output=True, text=True)
        output = result.stdout + result.stderr
        if result.returncode == 0 or "error CS" in output or "error MSB" in output:
            raise RuntimeError(name + " did not fail behaviorally:\n" + output)
        print("Killed adapter mutant:", name)
