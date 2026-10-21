using System.Text.Json.Nodes;
using Empire.Core.State;
using static Empire.Core.State.StateJson;

namespace Empire.Core.Lifecycle;

public readonly record struct TurnHandoff(int Player, int? ActiveUnit, bool RoundAdvanced, bool Exhausted = false);
public sealed record TurnOrderContext(ReadOnlyContent Content, Action<JsonObject> Emit);
public sealed record TurnOrderRules(ReadOnlyContent Content, TurnOrderRegistry TurnOrders);

/// <summary>The single phase/entitlement gate over the battle's selected policy.</summary>
public sealed class TurnOrderSystem(TurnOrderRules rules)
{
    public TurnOrderRegistry Policies => rules.TurnOrders;
    public ITurnOrderPolicy Active(JsonObject state) => rules.TurnOrders.Get(Text(Object(state["turnOrder"], "turnOrder"), "policy"));
    public TurnHandoff Start(JsonObject state, Action<JsonObject> emit)
    {
        var policy = rules.TurnOrders.Get(Text(Object(state["rules"], "rules"), "turnOrder"));
        state["turnOrder"] = policy.InitialState(state, rules.Content);
        return policy.Begin(state, new(rules.Content, emit));
    }
    public TurnHandoff Advance(JsonObject state, Action<JsonObject> emit) => Active(state).Advance(state, new(rules.Content, emit));
    public bool MayAct(JsonObject state, JsonObject unit) => Text(state, "phase") == "playing" && CanAct(state, unit);
    public bool CanAct(JsonObject state, JsonObject unit) => Active(state).CanAct(state, unit);
    public IReadOnlyList<JsonObject> Actors(JsonObject state) => Active(state).Actors(state);
    public IReadOnlyList<int> Preview(JsonObject state, int count = 8) => Active(state).Preview(state, rules.Content, count);

    internal static IEnumerable<JsonObject> Players(JsonObject state) => Objects(state["players"], "players");
    internal static IEnumerable<JsonObject> Units(JsonObject state) => Objects(state["units"], "units");
    internal static JsonObject Player(JsonObject state, int id) => Players(state).FirstOrDefault(p => Integer(p, "id") == id)
        ?? throw new DomainInvariantException($"unknown player {id}");
}
