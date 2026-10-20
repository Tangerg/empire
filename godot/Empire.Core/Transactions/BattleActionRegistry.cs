using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
namespace Empire.Core.Transactions;
public delegate void BattleActionHandler(JsonObject draft, JsonObject action, Action<JsonObject> emit);
internal sealed record BattleActionRegistration(BattleActionHandler Handler, bool HandsOffTurn);
public sealed class BattleActionRegistry
{
    private readonly Dictionary<string, BattleActionRegistration> _handlers = new(StringComparer.Ordinal);
    public void Register(string kind, BattleActionHandler handler, bool handsOffTurn = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(handler);
        if (!_handlers.TryAdd(kind, new(handler, handsOffTurn))) throw new DomainInvariantException($"Action handler '{kind}' is already registered");
    }
    public void Replace(string kind, BattleActionHandler handler, bool handsOffTurn = false)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (!_handlers.ContainsKey(kind)) throw new DomainInvariantException($"Action handler '{kind}' is not registered");
        _handlers[kind] = new(handler, handsOffTurn);
    }
    public bool HandsOffTurn(JsonObject action) =>
        action["kind"] is JsonValue kind && kind.TryGetValue<string>(out var key) &&
        _handlers.TryGetValue(key, out var registration) && registration.HandsOffTurn;

    internal IReadOnlyDictionary<string, BattleActionRegistration> Freeze() =>
        new ReadOnlyDictionary<string, BattleActionRegistration>(new Dictionary<string, BattleActionRegistration>(_handlers, StringComparer.Ordinal));
}
