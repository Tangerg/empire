using System.Text.Json.Nodes;
using Empire.Core.State;
using static Empire.Core.State.StateJson;
using static Empire.Core.Lifecycle.TurnOrderSystem;

namespace Empire.Core.Lifecycle;

public sealed class InitiativeTurnOrder : ITurnOrderPolicy
{
    public const int Threshold = 100;
    public string Id => "initiative";
    public JsonObject InitialState(JsonObject state, ReadOnlyContent content)
    {
        var data = new JsonObject();
        foreach (var unit in Units(state)) data[ChargeKey(Integer(unit, "id"))] = Speed(content, unit) % 37;
        return new JsonObject { ["policy"] = Id, ["activeUnit"] = null, ["data"] = data };
    }
    public TurnHandoff Begin(JsonObject state, TurnOrderContext context) => AdvanceInitiative(state, context.Content, false);
    public TurnHandoff Advance(JsonObject state, TurnOrderContext context) => AdvanceInitiative(state, context.Content, true);
    public bool CanAct(JsonObject state, JsonObject unit) => Order(state)["activeUnit"] is { } active && Integer(active, "activeUnit") == Integer(unit, "id") && !IsTrue(unit["done"]);
    public IReadOnlyList<JsonObject> Actors(JsonObject state) => Units(state).Where(unit => CanAct(state, unit)).ToArray();
    public IReadOnlyList<int> Preview(JsonObject state, ReadOnlyContent content, int count)
    {
        var ticker = Ticker(content, state);
        var result = new List<int>();
        for (var guard = 0; result.Count < count && guard < 10_000; guard++)
        {
            var ready = Ready(ticker);
            if (ready.Length == 0)
            {
                foreach (var id in ticker.Keys.ToArray()) ticker[id] = (ticker[id].Charge + ticker[id].Speed, ticker[id].Speed);
                continue;
            }
            var first = ready[0];
            result.Add(first);
            ticker[first] = (ticker[first].Charge - Threshold, ticker[first].Speed);
        }
        return result;
    }

    private TurnHandoff AdvanceInitiative(JsonObject state, ReadOnlyContent content, bool spendActive)
    {
        var order = Order(state);
        var data = Object(order["data"], "turn order data");
        var live = Units(state).Select(unit => ChargeKey(Integer(unit, "id"))).ToHashSet(StringComparer.Ordinal);
        foreach (var key in data.Select(pair => pair.Key).ToArray())
            if (key.StartsWith("charge.", StringComparison.Ordinal) && !live.Contains(key)) data.Remove(key);
        if (order["activeUnit"] is { } active && !Units(state).Any(u => Integer(u, "id") == Integer(active, "activeUnit")))
            order["activeUnit"] = null;
        if (spendActive && order["activeUnit"] is { } spent)
        {
            var key = ChargeKey(Integer(spent, "activeUnit"));
            data[key] = Number(data, key, 0) - Threshold;
        }
        var advanced = false;
        for (var guard = 0; guard <= Threshold * 4; guard++)
        {
            var ticker = Ticker(content, state);
            if (ticker.Count == 0) break;
            var ready = Ready(ticker);
            if (ready.Length > 0)
            {
                var actor = Units(state).FirstOrDefault(unit => Integer(unit, "id") == ready[0]);
                if (actor is not null)
                {
                    order["activeUnit"] = Integer(actor, "id");
                    return new TurnHandoff(Integer(actor, "owner"), Integer(actor, "id"), advanced);
                }
            }
            foreach (var pair in ticker) data[ChargeKey(pair.Key)] = pair.Value.Charge + pair.Value.Speed;
            advanced = true;
        }
        order["activeUnit"] = null;
        return new TurnHandoff(Integer(state, "currentPlayer"), null, advanced, true);
    }

    private static Dictionary<int, (double Charge, double Speed)> Ticker(ReadOnlyContent content, JsonObject state)
    {
        var result = new Dictionary<int, (double, double)>();
        var data = Object(Order(state)["data"], "turn order data");
        foreach (var unit in Units(state))
        {
            if (!IsTrue(Player(state, Integer(unit, "owner"))["alive"])) continue;
            var id = Integer(unit, "id");
            result[id] = (Number(data, ChargeKey(id), 0), Speed(content, unit));
        }
        return result;
    }

    private static int[] Ready(Dictionary<int, (double Charge, double Speed)> ticker) => ticker
        .Where(pair => pair.Value.Charge >= Threshold).OrderByDescending(pair => pair.Value.Charge)
        .ThenByDescending(pair => pair.Value.Speed).ThenBy(pair => pair.Key).Select(pair => pair.Key).ToArray();
    private static double Speed(ReadOnlyContent content, JsonObject unit) => Math.Max(1, (content.Definition("units", Text(unit, "type"))
        ?? throw new DomainInvariantException($"unknown unit \"{Text(unit, "type")}\"")).GetProperty("movement").GetDouble() * 10);
    private static string ChargeKey(int id) => $"charge.{id}";
    private static JsonObject Order(JsonObject state) => Object(state["turnOrder"], "turnOrder");
}
