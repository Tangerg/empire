using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.State;

internal static class StateContractChecks
{
    public static void Run(string root)
    {
        var fixture = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "state-contracts.json")))!;
        foreach (var item in fixture["levels"]!.AsArray())
        {
            var actual = LevelNormalizer.Normalize(item!["input"]);
            if (StateDigest.Hash(actual) != StateDigest.Hash(item["normalized"]!))
                throw new Exception("Level normalization: " + ReplayChecks.Difference(item["normalized"], actual, "$"));
        }
        var numericSchema = fixture["levels"]![0]!["input"]!.DeepClone().AsObject();
        numericSchema["schema"] = 2.0;
        LevelNormalizer.Normalize(numericSchema);
        var packs = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "../../data/content-packs.json")))!.AsArray();
        var grids = new GridRegistry().Register(new Square4Grid()).Register(new Square8Grid()).Register(new OddRowHexGrid()).Seal();
        foreach (var item in fixture["spawns"]!.AsArray())
        {
            var state = item!["before"]!.DeepClone().AsObject();
            var unit = BattleStateFactory.SpawnUnit(packs, grids, state, item["type"]!.GetValue<string>(),
                item["owner"]!.GetValue<int>(), new Coord(item["at"]!["x"]!.GetValue<int>(), item["at"]!["y"]!.GetValue<int>()),
                item["hp"]!.GetValue<double>(), item["done"]!.GetValue<bool>(), item["source"]!.AsObject());
            if (StateDigest.Hash(unit) != StateDigest.Hash(item["unit"]!)) throw new Exception("Spawn unit differs");
            if (StateDigest.Hash(state) != StateDigest.Hash(item["after"]!)) throw new Exception("Spawn state differs");
        }
    }
}
