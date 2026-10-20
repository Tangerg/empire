using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using Empire.Core.Registries;
using Empire.Core.Actions;

namespace Empire.Core.Transactions;

public delegate void BattleActionHandler(JsonObject draft, JsonObject action, Action<JsonObject> emit);
public delegate void RuleActionHandler(ActionExecutionContext context, JsonObject action);
public sealed record BattleActionRegistration(string Kind, RuleActionHandler Handler, bool HandsOffTurn = false, bool DuringDeployment = false)
{
    public BattleActionRegistration(string kind, BattleActionHandler handler, bool handsOffTurn = false, bool duringDeployment = false)
        : this(kind, Adapt(handler), handsOffTurn, duringDeployment) { }

    private static RuleActionHandler Adapt(BattleActionHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return (context, action) => handler(context.State, action, context.Emit);
    }
}

public sealed class BattleActionRegistry : KeyedRegistry<BattleActionRegistration, BattleActionRegistry>
{
    public BattleActionRegistry() : base("action handler") { }
    protected override string KeyOf(BattleActionRegistration entry) => entry.Kind;
    protected override void ValidateEntry(BattleActionRegistration entry) => ArgumentNullException.ThrowIfNull(entry.Handler);

    public BattleActionRegistry Register(string kind, BattleActionHandler handler, bool handsOffTurn = false, bool duringDeployment = false) =>
        Register(new BattleActionRegistration(kind, handler, handsOffTurn, duringDeployment));

    public BattleActionRegistry Replace(string kind, BattleActionHandler handler, bool handsOffTurn = false, bool duringDeployment = false) =>
        Replace(new BattleActionRegistration(kind, handler, handsOffTurn, duringDeployment));

    public BattleActionRegistry Register(string kind, RuleActionHandler handler, bool handsOffTurn = false, bool duringDeployment = false) =>
        Register(new BattleActionRegistration(kind, handler, handsOffTurn, duringDeployment));

    public BattleActionRegistry Replace(string kind, RuleActionHandler handler, bool handsOffTurn = false, bool duringDeployment = false) =>
        Replace(new BattleActionRegistration(kind, handler, handsOffTurn, duringDeployment));

    public BattleActionRegistry Clone() => CopyInto(new BattleActionRegistry());

    public bool HandsOffTurn(JsonObject action) =>
        action["kind"] is JsonValue kind && kind.TryGetValue<string>(out var key) &&
        TryGet(key)?.HandsOffTurn == true;

    public bool DuringDeployment(JsonObject action) =>
        action["kind"] is JsonValue kind && kind.TryGetValue<string>(out var key) &&
        TryGet(key)?.DuringDeployment == true;

    internal IReadOnlyDictionary<string, BattleActionRegistration> Freeze() =>
        new ReadOnlyDictionary<string, BattleActionRegistration>(All().ToDictionary(entry => entry.Kind, StringComparer.Ordinal));
}
