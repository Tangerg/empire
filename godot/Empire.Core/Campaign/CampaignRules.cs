using System.Text.Json.Nodes;
using static Empire.Core.State.StateJson;

namespace Empire.Core.Campaign;

public abstract class CampaignRegistry<T> where T : class
{
    private readonly Dictionary<string, T> entries = new(StringComparer.Ordinal);
    private readonly string subject;
    protected CampaignRegistry(string subject) => this.subject = subject;
    protected abstract string KeyOf(T entry);
    public bool IsSealed { get; private set; }
    public int Count => entries.Count;
    public CampaignRegistry<T> Register(T entry)
    {
        RequireMutable();
        if (!entries.TryAdd(KeyOf(entry), entry)) throw new DomainInvariantException($"{subject} already registered: \"{KeyOf(entry)}\"");
        return this;
    }
    public CampaignRegistry<T> Replace(T entry) { RequireMutable(); entries[KeyOf(entry)] = entry; return this; }
    public T Get(string id) => entries.GetValueOrDefault(id) ?? throw new DomainInvariantException($"unknown {subject} \"{id}\"");
    public T? TryGet(string id) => entries.GetValueOrDefault(id);
    public bool Has(string id) => entries.ContainsKey(id);
    public IReadOnlyList<T> All() => entries.Values.ToArray();
    public IReadOnlyList<string> Keys() => entries.Keys.ToArray();
    public CampaignRegistry<T> Seal() { IsSealed = true; return this; }
    protected R CopyInto<R>(R result) where R : CampaignRegistry<T>
    { foreach (var entry in entries.Values) result.Register(entry); return result; }
    private void RequireMutable() { if (IsSealed) throw new DomainInvariantException($"{subject} registry is sealed after composition"); }
}

public sealed record CampaignConditionHandler(string Kind, Func<JsonObject, JsonObject, CampaignConditionRegistry, bool> Evaluate);
public sealed class CampaignConditionRegistry : CampaignRegistry<CampaignConditionHandler>
{
    public CampaignConditionRegistry() : base("campaign condition handler") { }
    protected override string KeyOf(CampaignConditionHandler value) => value.Kind;
    public bool Evaluate(JsonObject state, JsonObject condition) => Get(Text(condition, "type")).Evaluate(state, condition, this);
    public CampaignConditionRegistry Clone() => CopyInto(new CampaignConditionRegistry());
    public static CampaignConditionRegistry Core()
    {
        var registry = new CampaignConditionRegistry();
        registry.Register(new("flag", (state, value, _) => Contains(state["flags"], Text(value, "flag")) == !IsFalse(value["present"])));
        registry.Register(new("variable", (state, value, _) => Compare(state["variables"]![Text(value, "key")], Text(value, "op"), value["value"])));
        registry.Register(new("resource", (state, value, _) => Compare(state["resources"]![Text(value, "resource")] ?? JsonValue.Create(0), Text(value, "op"), value["value"])));
        registry.Register(new("relation", (state, value, _) => Compare(state["relations"]![Text(value, "faction")] ?? JsonValue.Create(0), Text(value, "op"), value["value"])));
        registry.Register(new("roster", (state, value, _) => state["roster"]![Text(value, "unit")] is JsonObject unit &&
            (value["disposition"] is null || Text(unit, "disposition") == Text(value, "disposition"))));
        registry.Register(new("all", (state, value, rules) => Objects(value["conditions"], "conditions").All(child => rules.Evaluate(state, child))));
        registry.Register(new("any", (state, value, rules) => Objects(value["conditions"], "conditions").Any(child => rules.Evaluate(state, child))));
        registry.Register(new("not", (state, value, rules) => !rules.Evaluate(state, Object(value["condition"], "condition"))));
        return registry;
    }
    internal static bool Contains(JsonNode? array, string value) => Array(array, "values").Any(item => Text(item, "value") == value);
    private static bool Compare(JsonNode? left, string operation, JsonNode? right)
    {
        if (operation == "eq") return JsonNode.DeepEquals(left, right);
        if (operation == "neq") return !JsonNode.DeepEquals(left, right);
        if (left?.GetValueKind() != System.Text.Json.JsonValueKind.Number || right?.GetValueKind() != System.Text.Json.JsonValueKind.Number) return false;
        return operation == "gte" ? Number(left, "left") >= Number(right, "right") : Number(left, "left") <= Number(right, "right");
    }
}

public sealed record CampaignEffectHandler(string Kind, Action<JsonObject, JsonObject> Apply);
public sealed class CampaignEffectRegistry : CampaignRegistry<CampaignEffectHandler>
{
    public CampaignEffectRegistry() : base("campaign effect handler") { }
    protected override string KeyOf(CampaignEffectHandler value) => value.Kind;
    public void Apply(JsonObject state, JsonObject effect) => Get(Text(effect, "type")).Apply(state, effect);
    public CampaignEffectRegistry Clone() => CopyInto(new CampaignEffectRegistry());
    public static CampaignEffectRegistry Core()
    {
        var registry = new CampaignEffectRegistry();
        registry.Register(new("setVariable", (state, effect) => state["variables"]![Text(effect, "key")] = effect["value"]?.DeepClone()));
        registry.Register(new("addVariable", (state, effect) =>
        {
            var key = Text(effect, "key"); var current = state["variables"]![key];
            if (current is not null && current.GetValueKind() != System.Text.Json.JsonValueKind.Number)
                throw new CampaignInvariantException($"campaign variable \"{key}\" is not numeric");
            state["variables"]![key] = (current is null ? 0 : Number(current, key)) + Number(effect["amount"], "amount");
        }));
        registry.Register(new("setFlag", (state, effect) =>
        { var flag = Text(effect, "flag"); if (!CampaignConditionRegistry.Contains(state["flags"], flag)) Array(state["flags"], "flags").Add(flag); }));
        registry.Register(new("clearFlag", (state, effect) => state["flags"] = Without(state["flags"], Text(effect, "flag"))));
        registry.Register(new("addResource", (state, effect) =>
        { var id = Text(effect, "resource"); state["resources"]![id] = Number(Object(state["resources"], "resources"), id, 0) + Number(effect["amount"], "amount"); }));
        registry.Register(new("changeRelation", (state, effect) =>
        { var id = Text(effect, "faction"); state["relations"]![id] = Number(Object(state["relations"], "relations"), id, 0) + Number(effect["amount"], "amount"); }));
        registry.Register(new("setFeature", (state, effect) =>
        {
            var id = Text(effect, "feature");
            if (!IsTrue(effect["enabled"])) state["features"] = Without(state["features"], id);
            else state["features"] = new JsonArray(Array(state["features"], "features").Select(value => Text(value, "feature")).Append(id).Distinct(StringComparer.Ordinal).Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
        }));
        registry.Register(new("setRosterDisposition", (state, effect) =>
        {
            var id = Text(effect, "unit");
            if (state["roster"]![id] is not JsonObject unit) throw new CampaignInvariantException($"unknown campaign roster unit \"{id}\"");
            unit["disposition"] = effect["disposition"]!.DeepClone();
        }));
        return registry;
    }
    private static JsonArray Without(JsonNode? values, string exclude) => new(Array(values, "values")
        .Where(value => Text(value, "value") != exclude).Select(value => value!.DeepClone()).ToArray());
}

public sealed record CampaignRules(CampaignConditionRegistry Conditions, CampaignEffectRegistry Effects, CampaignNodeRegistry Nodes)
{
    public static CampaignRules Create(CampaignConditionRegistry? conditions = null, CampaignEffectRegistry? effects = null, CampaignNodeRegistry? nodes = null) =>
        new(conditions ?? CampaignConditionRegistry.Core(), effects ?? CampaignEffectRegistry.Core(), nodes ?? CampaignNodeRegistry.Core());
    internal CampaignRules FrozenClone()
    {
        var conditions = Conditions.Clone(); conditions.Seal();
        var effects = Effects.Clone(); effects.Seal();
        var nodes = Nodes.Clone(); nodes.Seal();
        return new(conditions, effects, nodes);
    }
}
