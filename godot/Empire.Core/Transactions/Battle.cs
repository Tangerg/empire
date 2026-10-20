using System.Text.Json.Nodes;
using Empire.Core.Determinism;
namespace Empire.Core.Transactions;
public sealed record BattleReceipt(JsonObject Before, IReadOnlyList<JsonObject> Events, string StateHash);
/// <summary>One battle writer; failed commands cannot leak state or partial events.</summary>
public sealed class Battle
{
    private JsonObject _state;
    private readonly IReadOnlyDictionary<string, BattleActionHandler> _handlers;
    private bool _dispatching;
    public Battle(JsonObject initialState, BattleActionRegistry registry)
    {
        _state = (JsonObject)initialState.DeepClone();
        _handlers = registry.Freeze();
    }
    public JsonObject Snapshot() => (JsonObject)_state.DeepClone();
    public string StateHash => StateDigest.Hash(_state);
    public BattleReceipt Dispatch(JsonObject action)
    {
        if (_dispatching) throw new DomainInvariantException("Battle dispatch cannot be reentrant");
        var kind = action["kind"]?.GetValue<string>() ?? throw new DomainInvariantException("Action requires a kind");
        if (!_handlers.TryGetValue(kind, out var handler)) throw new IllegalActionException($"没有处理行动类型「{kind}」的规则策略");
        var before = Snapshot();
        var draft = Snapshot();
        var events = new List<JsonObject>();
        _dispatching = true;
        try
        {
            handler(draft, (JsonObject)action.DeepClone(), value => events.Add((JsonObject)value.DeepClone()));
            var committed = (JsonObject)draft.DeepClone();
            var hash = StateDigest.Hash(committed);
            _state = committed;
            return new BattleReceipt(before, events.AsReadOnly(), hash);
        }
        finally { _dispatching = false; }
    }
}
