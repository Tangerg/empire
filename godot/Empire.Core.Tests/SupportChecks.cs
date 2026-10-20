using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
using Empire.Core.Orders;
using Empire.Core.Resources;

internal static class SupportChecks
{
    public static void Tactics(string root)
    {
        var packs = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "../../data/content-packs.json")))!.AsArray();
        var grids = new GridRegistry().Register(new Square4Grid()).Register(new Square8Grid()).Register(new OddRowHexGrid()).Seal();
        var orders = new SupportOrders(packs, grids);
        var cases = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "action-cases.json")))!.AsArray();
        var checkedCount = 0;
        foreach (var entry in cases)
        {
            var action = entry!["action"]!.AsObject();
            if (action["kind"]!.GetValue<string>() != "tactic") continue;
            var state = entry["before"]!.DeepClone().AsObject();
            var events = new JsonArray();
            void Emit(JsonObject value)
            {
                events.Add(value.DeepClone());
                var type = value["type"]!.GetValue<string>();
                var counts = state["scenario"]!["eventCounts"]!.AsObject();
                counts[type] = (counts[type]?.GetValue<int>() ?? 0) + 1;
            }
            var target = action["target"] is { } at
                ? new Coord(at["x"]!.GetValue<int>(), at["y"]!.GetValue<int>()) : (Coord?)null;
            orders.ExecuteTactic(state, action["commander"]!.GetValue<string>(), action["tactic"]!.GetValue<string>(), target, Emit);
            if (StateDigest.Hash(events) != StateDigest.Hash(entry["events"]!)) throw new Exception($"Tactic events differ at case {checkedCount}");
            if (StateDigest.Hash(state) != StateDigest.Hash(entry["after"]!)) throw new Exception($"Tactic state differs at case {checkedCount}");
            checkedCount++;
        }
        if (checkedCount != 6) throw new Exception($"Expected six tactic cases, saw {checkedCount}");
    }

    public static void Resources()
    {
        var player = JsonNode.Parse("{\"id\":1,\"resources\":{\"funds\":{\"current\":5,\"capacity\":10}}}")!.AsObject();
        var resources = new BattleResources();
        var subject = new BattleResources.Subject("player", player);
        if (resources.Credit(subject, "funds", 12) != 5 || resources.Balance(subject, "funds") != 10) throw new Exception("Credit must clamp at capacity");
        if (resources.Spend(subject, "funds", 2.5) != 3 || resources.Balance(subject, "funds") != 7) throw new Exception("Spend must use JavaScript rounding");
        var costs = JsonNode.Parse("[{\"resource\":\"funds\",\"amount\":4},{\"resource\":\"funds\",\"amount\":4}]")!.AsArray();
        if (resources.CanAfford(subject, costs)) throw new Exception("Costs must aggregate before affordability");
        try { resources.SpendAll(subject, costs); throw new Exception("Overspend accepted"); }
        catch (Empire.Core.DomainInvariantException) { }
        if (resources.Balance(subject, "funds") != 7) throw new Exception("Failed aggregate spend changed account");
    }
}
