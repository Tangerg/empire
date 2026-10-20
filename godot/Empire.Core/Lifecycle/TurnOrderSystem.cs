using System.Text.Json.Nodes;
using Empire.Core.State;
using static Empire.Core.State.StateJson;

namespace Empire.Core.Lifecycle;

public readonly record struct TurnHandoff(int Player, int? ActiveUnit, bool RoundAdvanced, bool Exhausted = false);

/// <summary>The source side and initiative policies, operating on the battle's serializable clock.</summary>
public sealed class TurnOrderSystem
{
    private readonly StateContent content;
    public TurnOrderSystem(JsonArray contentPacks) : this(new StateContent(contentPacks)) { }
    internal TurnOrderSystem(StateContent content) => this.content = content;

    public JsonObject InitialState(JsonObject state)
    {
        var policy = Text(Object(state["rules"], "rules"), "turnOrder");
        var data = new JsonObject();
        if (policy == "initiative")
            foreach (var unit in Units(state)) data[ChargeKey(Integer(unit, "id"))] = Speed(unit) % 37;
        else if (policy != "side") throw Unknown(policy);
        return new JsonObject { ["policy"] = policy, ["activeUnit"] = null, ["data"] = data };
    }

    public TurnHandoff Begin(JsonObject state) => Policy(state) switch
    {
        "side" => new TurnHandoff(Players(state).FirstOrDefault(p => IsTrue(p["alive"])) is { } alive
            ? Integer(alive, "id") : Players(state).FirstOrDefault() is { } first ? Integer(first, "id") : 1, null, false),
        "initiative" => AdvanceInitiative(state, false),
        var policy => throw Unknown(policy),
    };

    public TurnHandoff Advance(JsonObject state)
    {
        var policy = Policy(state);
        if (policy == "initiative") return AdvanceInitiative(state, true);
        if (policy != "side") throw Unknown(policy);
        var players = Players(state).ToArray();
        var current = Integer(state, "currentPlayer");
        var cursor = System.Array.FindIndex(players, p => Integer(p, "id") == current);
        var roundAdvanced = false;
        for (var step = 0; step < players.Length; step++)
        {
            if (++cursor >= players.Length) { cursor = 0; roundAdvanced = true; }
            if (IsTrue(players[cursor]["alive"]))
                return new TurnHandoff(Integer(players[cursor], "id"), null, roundAdvanced);
        }
        return new TurnHandoff(current, null, roundAdvanced, true);
    }

    public bool MayAct(JsonObject state, JsonObject unit) =>
        Text(state, "phase") == "playing" && CanAct(state, unit);

    public bool CanAct(JsonObject state, JsonObject unit) => Policy(state) switch
    {
        "side" => Integer(unit, "owner") == Integer(state, "currentPlayer") && !IsTrue(unit["done"]),
        "initiative" => Order(state)["activeUnit"] is { } active && Integer(active, "activeUnit") == Integer(unit, "id") && !IsTrue(unit["done"]),
        var policy => throw Unknown(policy),
    };

    public IReadOnlyList<JsonObject> Actors(JsonObject state) => Units(state).Where(unit => CanAct(state, unit)).ToArray();

    public IReadOnlyList<int> Preview(JsonObject state, int count = 8)
    {
        if (Policy(state) == "side") return Actors(state).Select(unit => Integer(unit, "id")).ToArray();
        if (Policy(state) != "initiative") throw Unknown(Policy(state));
        var ticker = Ticker(state);
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
            ticker[first] = (ticker[first].Charge - 100, ticker[first].Speed);
        }
        return result;
    }

    private TurnHandoff AdvanceInitiative(JsonObject state, bool spendActive)
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
            data[key] = Number(data, key, 0) - 100;
        }
        var advanced = false;
        for (var guard = 0; guard <= 400; guard++)
        {
            var ticker = Ticker(state);
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

    private Dictionary<int, (double Charge, double Speed)> Ticker(JsonObject state)
    {
        var result = new Dictionary<int, (double, double)>();
        var data = Object(Order(state)["data"], "turn order data");
        foreach (var unit in Units(state))
        {
            if (!IsTrue(Player(state, Integer(unit, "owner"))["alive"])) continue;
            var id = Integer(unit, "id");
            result[id] = (Number(data, ChargeKey(id), 0), Speed(unit));
        }
        return result;
    }

    private static int[] Ready(Dictionary<int, (double Charge, double Speed)> ticker) => ticker
        .Where(pair => pair.Value.Charge >= 100).OrderByDescending(pair => pair.Value.Charge)
        .ThenByDescending(pair => pair.Value.Speed).ThenBy(pair => pair.Key).Select(pair => pair.Key).ToArray();
    private double Speed(JsonObject unit) => Math.Max(1, Number(content.Get("units", Text(unit, "type")), "movement", 0) * 10);
    private static string ChargeKey(int id) => $"charge.{id}";
    private static string Policy(JsonObject state) => Text(Order(state), "policy");
    private static JsonObject Order(JsonObject state) => Object(state["turnOrder"], "turnOrder");
    internal static IEnumerable<JsonObject> Players(JsonObject state) => Objects(state["players"], "players");
    internal static IEnumerable<JsonObject> Units(JsonObject state) => Objects(state["units"], "units");
    internal static JsonObject Player(JsonObject state, int id) => Players(state).FirstOrDefault(p => Integer(p, "id") == id)
        ?? throw new DomainInvariantException($"unknown player {id}");
    private static DomainInvariantException Unknown(string id) => new($"unknown turn order \"{id}\"");
}
