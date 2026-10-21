using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Abilities;
using Empire.Core.Combat;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Orders;
using Empire.Core.Resources;
using Empire.Core.State;
using Empire.Core.Tactics;
using Empire.Core.Transactions;

internal static class ResourceConsumerChecks
{
    public static void Run(JsonObject input, Action<string, JsonNode?, JsonNode?> check)
    {
        var originalPacks = input["consumerPacks"]!.AsArray();
        var baseline = new GrayFlagEngine(originalPacks);
        var serialization = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        JsonNode? Serialize(object? value) => JsonSerializer.SerializeToNode(value, serialization);
        foreach (var item in input["consumers"]!.AsArray().OfType<JsonObject>())
        {
            var name = StateJson.Text(item, "name");
            var operation = StateJson.Text(item, "operation");
            var packs = originalPacks.DeepClone().AsArray();
            foreach (var (family, key) in new[] { ("weapons", "weapon"), ("tactics", "tactic") })
            {
                var definition = item[key]!.AsObject();
                var id = StateJson.Text(definition, "id");
                var target = packs.OfType<JsonObject>().SelectMany(pack => StateJson.Objects(pack[family], family))
                    .Single(value => StateJson.Text(value, "id") == id);
                foreach (var (field, value) in definition) target[field] = value?.DeepClone();
            }
            var resources = Policies(item["config"]!.AsObject());
            var catalog = new ReadOnlyContent(packs);
            var combat = new BattleCombat(catalog, baseline.Grids, resources, baseline.Space, baseline.AreaShapes,
                DefaultCombatModifiers.Create(), DefaultWeaponHitEffects.Create(), baseline.Reactions,
                baseline.RankProgression, baseline.UnitDepartures);
            var support = new SupportOrders(catalog, baseline.Grids, resources, combat.Effects);
            var rules = baseline.AbilityRules with { Content = catalog, Combat = combat, Support = support };
            var commands = new AbilityCommands(rules, baseline.Commands);
            var state = item["initial"]!.DeepClone().AsObject();
            var actor = state["units"]![0]!.AsObject();
            JsonNode? menus = null;
            if (operation == "command")
            {
                menus = Serialize(commands.Options(state, actor, TacticalQueries.Position(actor)));
                foreach (var option in menus!.AsArray().OfType<JsonObject>())
                {
                    option.Remove("engagement");
                    if (option["weapon"] is null) option.Remove("weapon");
                }
            }
            else if (operation.StartsWith("tactic", StringComparison.Ordinal))
            {
                try { menus = Serialize(support.Tactics(state, "resource-commander")); }
                catch (DomainInvariantException fault) { throw new Exception(name + " menus: " + fault.Message, fault); }
            }
            JsonNode? result = null;
            JsonObject? error = null;
            var events = new JsonArray();
            Battle? battle = null;
            BattleReceipt? receipt = null;
            try
            {
                if (operation == "ready") result = JsonValue.Create(combat.Forecasts.IsWeaponReady(state, actor, item["weapon"]!.AsObject()));
                else if (operation == "consume") combat.Forecasts.ConsumeWeapon(state, actor, "soldier_sword", value => events.Add(value));
                else if (operation == "command")
                {
                    battle = new Battle(state, new BattleActionRegistry().Register("command", (draft, action, emit) =>
                    {
                        commands.Execute(draft, action, value =>
                        {
                            emit(value);
                            var counts = draft["scenario"]!["eventCounts"]!.AsObject();
                            var type = StateJson.Text(value, "type");
                            counts[type] = StateJson.Number(counts, type, 0) + 1;
                        });
                    }));
                    receipt = battle.Dispatch(EcmaJson.Parse("{\"kind\":\"command\",\"unit\":1,\"path\":[{\"x\":2,\"y\":3}],\"command\":{\"ability\":\"attack\",\"weapon\":\"soldier_sword\",\"target\":{\"x\":3,\"y\":3}}}")!.AsObject());
                    foreach (var value in receipt.Events) events.Add(value.DeepClone());
                }
                else if (operation == "tactic-execute") support.ExecuteTactic(state, "resource-commander", "rally", null, value => events.Add(value));
                else if (operation != "tactic-options") throw new Exception("Unknown consumer operation");
            }
            catch (Exception fault) when (fault is DomainInvariantException or IllegalActionException)
            {
                error = new JsonObject { ["kind"] = fault.GetType().Name.Replace("Exception", ""), ["message"] = fault.Message };
            }
            if (battle is not null) state = battle.Snapshot();
            check(name + " menus", menus, item["menus"]);
            check(name + " result", result, item["result"]);
            check(name + " error", error, item["error"]);
            check(name + " events", events, item["events"]);
            check(name + " state", state, item["after"]);
            check(name + " digest", JsonValue.Create(StateDigest.Hash(state)), item["hash"]);
            if (receipt is not null)
            {
                battle!.Restore(receipt);
                check(name + " undo", battle.Snapshot(), item["initial"]);
            }
        }
    }

    private static BattleResources Policies(JsonObject config)
    {
        var resources = new BattleResources();
        foreach (var (id, subjectKind) in new[] { ("charge", "squad"), ("favor", "player") })
            resources.Adapters.Register(new(id, id, subjectKind, id == "charge" && StateJson.IsTrue(config["integer"]), 0.5, subject =>
            {
                if (subject.Holder["resources"]?[id] is not JsonObject state) return null;
                return new ResourceAccount(() => new(
                    state["current"] is { } current ? StateJson.Number(current, "current") : null,
                    state["capacity"] is { } capacity ? StateJson.Number(capacity, "capacity") : null),
                    current => state["current"] = current);
            }));
        resources.Subjects.Register(new("squad", (context, _) => new("squad", context.Player!), subject => new JsonObject
        {
            ["kind"] = "squad", ["id"] = "squad-" + StateJson.Integer(subject.Holder, "id"), ["slot"] = "reserve",
        }));
        return resources;
    }
}
