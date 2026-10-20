using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
namespace Empire.Core.Transactions;
public delegate void BattleActionHandler(JsonObject draft, JsonObject action, Action<JsonObject> emit);
public sealed class BattleActionRegistry
{
    private readonly Dictionary<string, BattleActionHandler> _handlers = new(StringComparer.Ordinal);
    public void Register(string kind, BattleActionHandler handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(handler);
        if (!_handlers.TryAdd(kind, handler)) throw new DomainInvariantException($"Action handler '{kind}' is already registered");
    }
    public void Replace(string kind, BattleActionHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (!_handlers.ContainsKey(kind)) throw new DomainInvariantException($"Action handler '{kind}' is not registered");
        _handlers[kind] = handler;
    }
    internal IReadOnlyDictionary<string, BattleActionHandler> Freeze() =>
        new ReadOnlyDictionary<string, BattleActionHandler>(new Dictionary<string, BattleActionHandler>(_handlers, StringComparer.Ordinal));
}
