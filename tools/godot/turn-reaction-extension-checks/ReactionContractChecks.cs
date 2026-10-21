using System.Text.Json.Nodes;
using Empire.Core.Combat;
using Empire.Core.Composition;
using Empire.Core.State;
using Empire.Core.Tactics;
using Keys = Empire.Core.Composition.BattleCapabilities;

static class ReactionContractChecks
{
    internal static void Run(JsonArray packs, JsonArray fixtures, Action<bool, string> check, Action<JsonNode?, JsonNode?, string> equal)
    {
        var support = ReactionRegistry.Core().Get("support");
        check(support.TriggeredRankProgress == 20 && support.SupportAttackMultiplier == 0.6, "default support semantics are declared");
        foreach (var renamed in new[] { false, true })
        {
            var id = renamed ? "test.support" : "support";
            var behavior = support with
            {
                Id = id,
                TriggeredRankProgress = 37,
                SupportAttackMultiplier = 0.25
            };
            var plugin = new EnginePlugin("test.support-policy", 1, context => context.Replace(Keys.Reactions,
                context.Require(Keys.Reactions).Clone().Replace(behavior)), overrides: [Keys.Reactions], requiresCapabilities: [Keys.Reactions]);
            var engine = BattleEngineFactory.Create(new ReadOnlyContent(packs), [plugin]);
            var source = fixtures.OfType<JsonObject>().Single(item => StateJson.Text(item, "name") == "support interceptor retains source bonus");
            var state = source["before"]!.DeepClone().AsObject();
            var interceptor = state["units"]![2]!.AsObject();
            interceptor["reaction"] = id;
            var events = new JsonArray();
            var before = StateJson.Number(interceptor, "rankProgress", 0);
            engine.Combat.ExecutePlan(state, engine.Combat.Forecast(state, state["units"]![0]!.AsObject(), new(3, 3)), value => events.Add(value));
            var reward = events.OfType<JsonObject>().FirstOrDefault(value => StateJson.Text(value, "type") == "rankProgressChanged" && StateJson.Integer(value, "unit") == StateJson.Integer(interceptor, "id"));
            check(reward is not null && StateJson.Number(reward, "amount", 0) == 37, id + " registry-owned triggered rank progress");
            check(StateJson.Number(interceptor, "rankProgress", 0) >= before + 37, id + " actual interception rank write");

            var level = DispositionContractChecks.Level("square8");
            level["units"]![1]!["x"] = 3;
            level["units"]!.AsArray().Add(new JsonObject { ["unit"] = "soldier", ["owner"] = 1, ["x"] = 2, ["y"] = 4, ["reaction"] = id });
            var battle = engine.CreateBattle(level, 321);
            state = battle.Snapshot();
            var attacker = state["units"]![0]!.AsObject();
            var defender = state["units"]![1]!.AsObject();
            var plan = engine.Combat.Forecast(state, attacker, TacticalQueries.Position(defender));
            check(plan.SupportAttack is not null, id + " registered support attack selection");
            check(plan.SupportAttack!.Damage.Modifiers.Any(modifier => modifier.Id == "reaction.support-attack" && modifier.Value == 0.25), id + " registered support attack modifier");
            var disabled = behavior with
            {
                SupportAttackMultiplier = null,
                TriggeredRankProgress = 0
            };
            var disabledEngine = BattleEngineFactory.Create(engine.Catalog, [new EnginePlugin("test.disable-support", 1,
                context => context.Replace(Keys.Reactions, context.Require(Keys.Reactions).Clone().Replace(disabled)),
                overrides: [Keys.Reactions], requiresCapabilities: [Keys.Reactions])]);
            check(disabledEngine.Combat.Forecast(state, attacker, TacticalQueries.Position(defender)).SupportAttack is null, id + " replacement disables support without an id special case");
            var save = engine.SaveBattle(state, "2026-10-21T00:00:00.000Z");
            equal(engine.LoadBattle(save), state, id + " registered support save admission");
            var recorder = engine.RecordBattle(state);
            var action = new JsonObject
            {
                ["kind"] = "command",
                ["unit"] = 1,
                ["path"] = new JsonArray(new JsonObject { ["x"] = 2, ["y"] = 3 }),
                ["command"] = new JsonObject { ["ability"] = "attack", ["target"] = new JsonObject { ["x"] = 3, ["y"] = 3 }, ["weapon"] = "soldier_sword" },
            };
            var result = battle.Dispatch(action);
            recorder.Record(action);
            check(result.Events.Any(value => StateJson.Text(value, "type") == "supportAttack"), id + " registry support reaches dispatch");
            equal(engine.LoadBattle(engine.SaveBattle(battle.Snapshot(), "2026-10-21T00:00:00.000Z")), battle.Snapshot(), id + " support result save round trip");
            var replay = engine.ReplayBattle(level, recorder.Replay(battle.Snapshot()));
            check(replay.DivergedAt is null, id + " support command replay");
            equal(replay.State, battle.Snapshot(), id + " support command replay state");
        }
    }
}
