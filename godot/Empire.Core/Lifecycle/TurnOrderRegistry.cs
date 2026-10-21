using System.Text.Json.Nodes;
using Empire.Core.Registries;
using Empire.Core.State;

namespace Empire.Core.Lifecycle;

public interface ITurnOrderPolicy
{
    string Id
    {
        get;
    }
    JsonObject InitialState(JsonObject state, ReadOnlyContent content);
    TurnHandoff Begin(JsonObject state, TurnOrderContext context);
    TurnHandoff Advance(JsonObject state, TurnOrderContext context);
    bool CanAct(JsonObject state, JsonObject unit);
    IReadOnlyList<JsonObject> Actors(JsonObject state);
    IReadOnlyList<int> Preview(JsonObject state, ReadOnlyContent content, int count);
}

public sealed class TurnOrderRegistry() : KeyedRegistry<ITurnOrderPolicy, TurnOrderRegistry>("turn order")
{
    protected override string KeyOf(ITurnOrderPolicy entry) => entry.Id;
    public TurnOrderRegistry Clone() => CopyInto(new TurnOrderRegistry());
    public static TurnOrderRegistry Core() => new TurnOrderRegistry().Register(new SideTurnOrder()).Register(new InitiativeTurnOrder());
}
