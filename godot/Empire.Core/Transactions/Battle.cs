using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.Actions;
namespace Empire.Core.Transactions;

/// <summary>A detached result and an unforgeable checkpoint issued by its battle.</summary>
public sealed class BattleReceipt
{
    private readonly JsonObject before;
    private readonly JsonObject[] events;
    internal object Owner { get; }
    internal BattleReceipt? Previous { get; }
    public JsonObject Before => (JsonObject)before.DeepClone();
    public IReadOnlyList<JsonObject> Events => Array.AsReadOnly(events.Select(value => (JsonObject)value.DeepClone()).ToArray());
    public string StateHash { get; }
    public bool HandsOffTurn { get; }

    internal BattleReceipt(object owner, BattleReceipt? previous, JsonObject before,
        IEnumerable<JsonObject> events, string stateHash, bool handsOffTurn)
    {
        Owner = owner;
        Previous = previous;
        this.before = (JsonObject)before.DeepClone();
        this.events = events.Select(value => (JsonObject)value.DeepClone()).ToArray();
        StateHash = stateHash;
        HandsOffTurn = handsOffTurn;
    }
}

/// <summary>One battle writer; failed commands cannot leak state or partial events.</summary>
public sealed class Battle
{
    private JsonObject _state;
    private readonly IReadOnlyDictionary<string, BattleActionRegistration> _handlers;
    private readonly object _checkpointOwner = new();
    private BattleReceipt? _checkpoint;
    private bool _dispatching;
    private readonly ActionExecutionPipeline? pipeline;
    public Battle(JsonObject initialState, BattleActionRegistry registry, ActionExecutionPipeline? pipeline = null)
    {
        _state = EcmaJson.Normalize(initialState)!.AsObject();
        _handlers = registry.Freeze();
        this.pipeline = pipeline;
    }
    public JsonObject Snapshot() => (JsonObject)_state.DeepClone();
    public string StateHash => StateDigest.Hash(_state);

    public void Restore(BattleReceipt receipt)
    {
        if (_dispatching) throw new DomainInvariantException("Battle restore cannot be reentrant");
        ArgumentNullException.ThrowIfNull(receipt);
        if (!ReferenceEquals(receipt.Owner, _checkpointOwner) || !ReferenceEquals(receipt, _checkpoint))
            throw new DomainInvariantException("Checkpoint is not the current restorable receipt of this battle");
        var restored = receipt.Before;
        _state = restored;
        _checkpoint = receipt.Previous;
    }

    public BattleReceipt Dispatch(JsonObject action)
    {
        if (_dispatching) throw new DomainInvariantException("Battle dispatch cannot be reentrant");
        action = EcmaJson.Normalize(action)!.AsObject();
        var kind = action["kind"]?.GetValue<string>() ?? throw new DomainInvariantException("Action requires a kind");
        if (!_handlers.TryGetValue(kind, out var registration) && pipeline is null) throw new IllegalActionException($"没有处理行动类型「{kind}」的规则策略");
        var before = Snapshot();
        var draft = Snapshot();
        var events = new List<JsonObject>();
        _dispatching = true;
        try
        {
            void Emit(JsonObject value) => events.Add((JsonObject)value.DeepClone());
            if (pipeline is not null) pipeline.Execute(registration, draft, (JsonObject)action.DeepClone(), Emit);
            else registration!.Handler(ActionExecutionContext.Bare(draft, Emit), (JsonObject)action.DeepClone());
            var committed = (JsonObject)draft.DeepClone();
            var hash = StateDigest.Hash(committed);
            var receipt = new BattleReceipt(_checkpointOwner, registration!.HandsOffTurn ? null : _checkpoint,
                before, events, hash, registration!.HandsOffTurn);
            _state = committed;
            _checkpoint = registration!.HandsOffTurn ? null : receipt;
            return receipt;
        }
        finally { _dispatching = false; }
    }
}
