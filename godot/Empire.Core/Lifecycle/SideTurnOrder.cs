using System.Text.Json.Nodes;
using Empire.Core.State;
using static Empire.Core.State.StateJson;
using static Empire.Core.Lifecycle.TurnOrderSystem;

namespace Empire.Core.Lifecycle;

public sealed class SideTurnOrder : ITurnOrderPolicy
{
    public string Id => "side";
    public JsonObject InitialState(JsonObject state, ReadOnlyContent content) => new() { ["policy"] = Id, ["activeUnit"] = null, ["data"] = new JsonObject() };
    public TurnHandoff Begin(JsonObject state, TurnOrderContext context) => new(Players(state).FirstOrDefault(p => IsTrue(p["alive"])) is { } alive
        ? Integer(alive, "id") : Players(state).FirstOrDefault() is { } first ? Integer(first, "id") : 1, null, false);
    public TurnHandoff Advance(JsonObject state, TurnOrderContext context)
    {
        var players = Players(state).ToArray();
        var current = Integer(state, "currentPlayer");
        var cursor = System.Array.FindIndex(players, p => Integer(p, "id") == current);
        var roundAdvanced = false;
        for (var step = 0; step < players.Length; step++)
        {
            if (++cursor >= players.Length)
            {
                cursor = 0;
                roundAdvanced = true;
            }
            if (IsTrue(players[cursor]["alive"]))
                return new TurnHandoff(Integer(players[cursor], "id"), null, roundAdvanced);
        }
        return new TurnHandoff(current, null, roundAdvanced, true);
    }

    public bool CanAct(JsonObject state, JsonObject unit) => Integer(unit, "owner") == Integer(state, "currentPlayer") && !IsTrue(unit["done"]);
    public IReadOnlyList<JsonObject> Actors(JsonObject state) => Units(state).Where(unit => CanAct(state, unit)).ToArray();
    public IReadOnlyList<int> Preview(JsonObject state, ReadOnlyContent content, int count) => Actors(state).Select(unit => Integer(unit, "id")).ToArray();
}
