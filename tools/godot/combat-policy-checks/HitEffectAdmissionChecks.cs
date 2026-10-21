using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Combat;
using Empire.Core.Composition;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.State;
using Keys = Empire.Core.Composition.BattleCapabilities;

static class HitEffectAdmissionChecks
{
    internal static int Run(JsonArray sourcePacks)
    {
        var checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("Mismatch in native hit-effect admission: " + name);
            checks++;
        }
        void Refuses(Action action, string reason)
        {
            try { action(); throw new Exception("Mismatch in native hit-effect admission: missing refusal " + reason); }
            catch (DomainInvariantException error) { Check(error.Message.Contains(reason, StringComparison.Ordinal), "owner-specific refusal: " + error.Message); }
        }
        foreach (var kind in new[] { "forcedMove", "addStatus", "test.mark" })
        {
            var packs = (JsonArray)sourcePacks.DeepClone();
            JsonObject Weapon(JsonArray values) => values.OfType<JsonObject>().SelectMany(pack => pack["weapons"]?.AsArray().OfType<JsonObject>() ?? [])
                .Single(value => StateJson.Text(value, "id") == "soldier_sword");
            var payload = new JsonObject { ["type"] = kind, ["profile"] = "armor_down", ["rounds"] = 2, ["status"] = "opaque-not-a-reference" };
            Weapon(packs)["hitEffects"] = new JsonArray(payload.DeepClone());
            foreach (var weapon in packs.OfType<JsonObject>().SelectMany(pack => pack["weapons"]?.AsArray().OfType<JsonObject>() ?? []))
                if (weapon["hitEffects"] is JsonArray effects)
                    for (var index = 0; index < effects.Count; index++)
                        if (StateJson.Text(effects[index]!.AsObject(), "type") == kind) effects[index] = payload.DeepClone();
            var catalog = new ReadOnlyContent(packs);
            var validated = 0;
            var handler = new WeaponHitEffectHandler(kind,
                (context, effect) => context.Rules.Effects.AddStatus(context.Target, StateJson.Text(effect, "profile"),
                    StateJson.Number(effect, "rounds", 0), StateJson.Integer(context.Attacker, "id"), context.Emit),
                effect => "Mark " + StateJson.Text(effect, "profile"))
            {
                Validate = (content, effect, owner) =>
                {
                    validated++;
                    Check(ReferenceEquals(content, catalog), kind + " validation receives composed content");
                    if (effect["profile"] is not JsonValue profile || !profile.TryGetValue<string>(out var id))
                        throw new DomainInvariantException(owner + " requires mark profile");
                    if (content.Definition("statuses", id) is null)
                        throw new DomainInvariantException(owner + " references missing mark profile " + id);
                    if (StateJson.Number(effect, "rounds", 0) < 1)
                        throw new DomainInvariantException(owner + " requires positive mark rounds");
                },
            };
            var plugin = new EnginePlugin("test.handler-admission." + kind, 1, context =>
            {
                var effects = context.Require(Keys.HitEffects).Clone();
                if (effects.Has(kind)) effects.Replace(handler); else effects.Register(handler);
                context.Replace(Keys.HitEffects, effects);
            }, overrides: [Keys.HitEffects], requiresCapabilities: [Keys.HitEffects]);
            var engine = BattleEngineFactory.Create(catalog, [plugin]);
            Check(validated > 0, kind + " handler validates content at composition");
            Check(engine.Combat.HitEffects.Describe(payload) == "Mark armor_down", kind + " handler owns description");
            foreach (var malformed in new[]
            {
                new JsonObject { ["type"] = kind, ["rounds"] = 2 },
                new JsonObject { ["type"] = kind, ["profile"] = "missing", ["rounds"] = 2 },
            })
            {
                var brokenPacks = (JsonArray)packs.DeepClone();
                Weapon(brokenPacks)["hitEffects"] = new JsonArray(malformed.DeepClone());
                var brokenCatalog = new ReadOnlyContent(brokenPacks);
                var brokenHandler = handler with
                {
                    Validate = (content, effect, owner) =>
                    {
                        Check(ReferenceEquals(content, brokenCatalog), kind + " invalid content validation identity");
                        if (effect["profile"] is null) throw new DomainInvariantException(owner + " requires mark profile");
                        if (content.Definition("statuses", StateJson.Text(effect, "profile")) is null)
                            throw new DomainInvariantException(owner + " references missing mark profile");
                    },
                };
                Refuses(() => BattleEngineFactory.Create(brokenCatalog, [new EnginePlugin("test.invalid-handler", 1, context =>
                    context.Replace(Keys.HitEffects, context.Require(Keys.HitEffects).Clone().Replace(brokenHandler)),
                    overrides: [Keys.HitEffects], requiresCapabilities: [Keys.HitEffects])]), "mark profile");
            }
            var level = Level();
            var battle = engine.CreateBattle(level, 789);
            var initial = battle.Snapshot();
            var direct = (JsonObject)initial.DeepClone(); var events = new JsonArray();
            var invalid = new JsonObject { ["type"] = kind, ["profile"] = "missing", ["rounds"] = 2 };
            Refuses(() => engine.Combat.ApplyHitEffects(direct, direct["units"]![0]!.AsObject(), direct["units"]![1]!.AsObject(), [invalid], value => events.Add(value)), "missing mark profile");
            Check(EcmaJson.DeepEquals(direct, initial) && events.Count == 0, kind + " direct apply validates before execution");
            var recorder = engine.RecordBattle(initial);
            var action = new JsonObject
            {
                ["kind"] = "command", ["unit"] = 1,
                ["path"] = new JsonArray(new JsonObject { ["x"] = 2, ["y"] = 3 }),
                ["command"] = new JsonObject { ["ability"] = "attack", ["weapon"] = "soldier_sword", ["target"] = new JsonObject { ["x"] = 3, ["y"] = 3 } },
            };
            var result = battle.Dispatch(action);
            recorder.Record(action);
            Check(result.Events.Any(value => StateJson.Text(value, "type") == "statusApplied" && StateJson.Text(value, "status") == "armor_down"), kind + " admitted replacement reaches actual attack");
            var saved = engine.SaveBattle(battle.Snapshot(), "2026-10-21T00:00:00.000Z");
            var restored = BattleEngineFactory.Create(catalog, [plugin]);
            Check(EcmaJson.DeepEquals(restored.LoadBattle(saved), battle.Snapshot()), kind + " custom handler save round trip");
            var replay = restored.ReplayBattle(level, recorder.Replay(battle.Snapshot()));
            Check(replay.DivergedAt is null && EcmaJson.DeepEquals(replay.State, battle.Snapshot()), kind + " custom handler replay");
        }
        var defaults = new GrayFlagEngine(sourcePacks);
        foreach (var invalid in new[]
        {
            new JsonObject { ["type"] = "forcedMove", ["mode"] = "push", ["distance"] = -1 },
            new JsonObject { ["type"] = "forcedMove", ["mode"] = "sideways", ["distance"] = 1 },
            new JsonObject { ["type"] = "addStatus", ["status"] = "armor_down", ["duration"] = 0 },
            new JsonObject { ["type"] = "removeStatus", ["status"] = "absent" },
        })
        {
            var state = defaults.CreateState(Level()); var before = (JsonObject)state.DeepClone(); var events = new JsonArray();
            Refuses(() => defaults.Combat.ApplyHitEffects(state, state["units"]![0]!.AsObject(), state["units"]![1]!.AsObject(), [invalid], value => events.Add(value)), StateJson.Text(invalid, "type") == "addStatus" ? "status duration" : "weapon hit effect");
            Check(EcmaJson.DeepEquals(state, before) && events.Count == 0, "default handler refuses malformed payload before writes");
        }
        foreach (var duration in new[] { 0, -1, 1.5, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var state = defaults.CreateState(Level()); var before = (JsonObject)state.DeepClone(); var events = new JsonArray();
            var invalid = new JsonObject { ["type"] = "addStatus", ["status"] = "armor_down", ["duration"] = duration };
            Refuses(() => defaults.Combat.ApplyHitEffects(state, state["units"]![0]!.AsObject(), state["units"]![1]!.AsObject(), [invalid], value => events.Add(value)), "status duration");
            Check(EcmaJson.DeepEquals(state, before) && events.Count == 0, "non-domain duration direct apply has no write");
        }
        foreach (var duration in new[] { 0, -1, 1.5 })
        {
            var invalid = new JsonObject { ["type"] = "addStatus", ["status"] = "armor_down", ["duration"] = duration };
            var packs = (JsonArray)sourcePacks.DeepClone();
            packs.OfType<JsonObject>().SelectMany(pack => pack["weapons"]?.AsArray().OfType<JsonObject>() ?? [])
                .Single(value => StateJson.Text(value, "id") == "soldier_sword")["hitEffects"] = new JsonArray(invalid.DeepClone());
            Refuses(() => new GrayFlagEngine(packs), "status duration");
            var engine = BattleEngineFactory.Create(defaults.Catalog, [new EnginePlugin("test.invalid-status-duration", 1,
                context => context.Replace(Keys.ActionHandlers, context.Require(Keys.ActionHandlers).Clone().Register("test.invalid-duration", (context, _) =>
                    context.Rules.Combat.ApplyHitEffects(context.State, context.State["units"]![0]!.AsObject(), context.State["units"]![1]!.AsObject(),
                        [new JsonObject { ["type"] = "addStatus", ["status"] = "armor_down", ["duration"] = 2 }, invalid], context.Emit))),
                overrides: [Keys.ActionHandlers], requiresCapabilities: [Keys.ActionHandlers])]);
            var battle = engine.CreateBattle(Level()); var before = battle.Snapshot(); var hash = battle.StateHash;
            Refuses(() => battle.Dispatch(new JsonObject { ["kind"] = "test.invalid-duration" }), "status duration");
            Check(EcmaJson.DeepEquals(before, battle.Snapshot()) && battle.StateHash == hash, "invalid-duration dispatch rolls back preceding status and emitted event counters");
        }
        return checks;
    }

    private static JsonObject Level() => new()
    {
        ["schema"] = 2, ["id"] = "native.hit-effects", ["name"] = "Hit effect contracts", ["width"] = 7, ["height"] = 7,
        ["terrain"] = new JsonArray(Enumerable.Repeat(".......", 7).Select(row => (JsonNode?)JsonValue.Create(row)).ToArray()),
        ["players"] = new JsonArray(
            new JsonObject { ["id"] = 1, ["name"] = "One", ["team"] = 1, ["controller"] = "human", ["color"] = "#ffffff" },
            new JsonObject { ["id"] = 2, ["name"] = "Two", ["team"] = 2, ["controller"] = "human", ["color"] = "#000000" }),
        ["units"] = new JsonArray(
            new JsonObject { ["unit"] = "soldier", ["owner"] = 1, ["x"] = 2, ["y"] = 3 },
            new JsonObject { ["unit"] = "soldier", ["owner"] = 2, ["x"] = 3, ["y"] = 3 }),
        ["victory"] = new JsonArray(new JsonObject { ["id"] = "route", ["type"] = "routEnemies", ["label"] = "Route" }),
        ["rules"] = new JsonObject { ["fog"] = false, ["zoneOfControl"] = false },
    };
}
