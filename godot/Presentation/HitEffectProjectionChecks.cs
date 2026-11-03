#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Combat;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Tactics;
using Empire.Localization;
using Empire.Presentation.Tactical;

namespace Empire.Presentation;

internal static class HitEffectProjectionChecks
{
    private const string AttackWeapon = "c01.satchel-charge";
    private const string CounterWeapon = "c01.forge-burst";
    private const string SupportWeapon = "c01.inquisitor-flame";

    private sealed record Fixture(BattleEngine Engine, GrayFlagSession Session, SessionPresentationText Presentation,
        TacticalStamp Stamp, TacticalSelection Selection, TacticalHit Target, CombatPlan Plan,
        int Primary, int Concealed, int Supporter);

    public static void Check()
    {
        CheckGroupedEffects(fog: false);
        CheckGroupedEffects(fog: true);
        CheckNoEffects();
    }

    private static void CheckGroupedEffects(bool fog)
    {
        var fixture = CreateFixture(fog, withEffects: true);
        var hud = ProjectWithoutMutation(fixture);
        var forecast = hud.Sections.Single(section => section.Key == "forecast");
        var details = hud.Sections.Single(section => section.Key == "forecast-details");
        Require(forecast.Rows.Where(row => row.Key.StartsWith("hit-effect-", StringComparison.Ordinal))
                .Select(row => row.Key).SequenceEqual(new[] { "hit-effect-attack", "hit-effect-counter", "hit-effect-support" }),
            "Many planned effects must produce one compact summary per actual attack, counter and support phase.");
        Require(forecast.Rows.Single(row => row.Key == "forecast-scope").Label
                == fixture.Presentation.Messages.Format("ui.hud.forecast_scope")
            && details.Rows.Single(row => row.Key == "hit-effect-condition").Label
                == fixture.Presentation.Messages.Format("ui.hud.hit_effect.condition"),
            "The direct-damage forecast scope and conditional aftermath explanation must remain explicit.");

        var visibleTargets = fixture.Plan.UnitHits.Select(hit => hit.Target)
            .Where(id => !fog || id != fixture.Concealed).ToArray();
        // Both collision effects have emphasis. The earlier one wins, not the larger amount;
        // ordinary effects retain their original place in the complete target-by-target detail.
        CheckPhase(fixture, forecast, details, "attack", fixture.Plan.Attacker, visibleTargets, AttackEffects(), prominent: 1);
        CheckPhase(fixture, forecast, details, "counter", fixture.Primary, [fixture.Plan.Attacker], CounterEffects(), prominent: 0);
        CheckPhase(fixture, forecast, details, "support", fixture.Supporter, [fixture.Primary], SupportEffects(), prominent: 1);
        Require(details.Rows.Where(row => row.Key.StartsWith("hit-effect-detail:", StringComparison.Ordinal))
                .Select(row => row.Key.Split(':')[1])
                .SequenceEqual(Enumerable.Repeat("attack", visibleTargets.Length * 4)
                    .Concat(Enumerable.Repeat("counter", 2)).Concat(Enumerable.Repeat("support", 2))),
            "Expanded details preserve phase order without dropping or regrouping effect entries.");

        if (fog)
        {
            var hiddenName = Name(fixture, fixture.Concealed);
            var disclosed = hud.Sections.SelectMany(section => section.Rows)
                .Where(row => row.Key.StartsWith("hit-effect-", StringComparison.Ordinal)).ToArray();
            Require(disclosed.All(row => !row.Label.Contains(hiddenName, StringComparison.Ordinal)
                    && !row.Hint.Contains(hiddenName, StringComparison.Ordinal)),
                "Fog-hidden collateral must not leak its identity through effect summaries, tooltips or expanded details.");
            Require(visibleTargets.Length == 2 && fixture.Plan.UnitHits.Single(hit => hit.Target == fixture.Concealed).Effects.Count == 4,
                "The hidden-target guard must exercise real planned effects, not an absent or already-filtered combat hit.");
        }
    }

    private static void CheckPhase(Fixture fixture, TacticalHudSection forecast, TacticalHudSection details,
        string phase, int source, IReadOnlyList<int> targets, IReadOnlyList<JsonObject> effects, int prominent)
    {
        var text = fixture.Presentation.Messages;
        var descriptions = effects.Select(effect => fixture.Presentation.HitEffects.Describe(EcmaJson.ToElement(effect))).ToArray();
        var compact = text.Format("ui.hud.hit_effect." + phase, descriptions[prominent].Compact);
        var remaining = targets.Count * effects.Count - 1;
        var summary = forecast.Rows.Single(row => row.Key == "hit-effect-" + phase);
        Require(summary.Label == text.Format("ui.hud.hit_effect.more", compact, remaining),
            $"{phase}: the summary must select the first highest-priority effect and disclose all {remaining} additional visible entries.");
        Require(summary.Label.StartsWith(text.Format("ui.hud.hit_effect.more", "", remaining), StringComparison.Ordinal),
            $"{phase}: the additional-effect count must precede the description so trailing ellipsis cannot conceal it.");
        var expected = targets.SelectMany(target => descriptions.Select(description =>
            text.Format("ui.hud.hit_effect.detail", Name(fixture, source), Name(fixture, target),
                text.Format("ui.hud.hit_effect." + phase, description.Detail)))).ToArray();
        var actual = details.Rows.Where(row => row.Key.StartsWith("hit-effect-detail:" + phase + ":", StringComparison.Ordinal)).ToArray();
        Require(actual.Select(row => row.Label).SequenceEqual(expected)
            && actual.Select(row => row.Key).SequenceEqual(expected.Select((_, index) => $"hit-effect-detail:{phase}:{index}")),
            $"{phase}: every visible target must retain every effect in authored order, with the actual source and recipient named.");
        Require(summary.Hint == text.Format("ui.hud.hit_effect.condition") + "\n" + string.Join("\n", expected),
            $"{phase}: the compact row's tooltip must retain the same complete ordered conditional details.");
    }

    private static void CheckNoEffects()
    {
        var fixture = CreateFixture(fog: false, withEffects: false);
        var hud = ProjectWithoutMutation(fixture);
        var rows = hud.Sections.SelectMany(section => section.Rows).ToArray();
        Require(rows.All(row => !row.Key.StartsWith("hit-effect-", StringComparison.Ordinal))
            && rows.Any(row => row.Key == "forecast-scope"),
            "A genuine attack/counter/support plan without effects must not invent summaries, extra counts or an aftermath condition.");
    }

    private static TacticalHudProjection ProjectWithoutMutation(Fixture fixture)
    {
        var session = fixture.Session;
        var state = EcmaJson.Stringify(session.Snapshot());
        const string savedAt = "2026-11-03T00:00:00.000Z";
        var save = EcmaJson.Stringify(fixture.Engine.SaveBattle(session.Snapshot(), savedAt));
        var first = session.ProjectHud(fixture.Stamp, fixture.Selection, fixture.Target, fixture.Plan.Attacker, false);
        var repeated = session.ProjectHud(fixture.Stamp, fixture.Selection, fixture.Target, fixture.Plan.Attacker, false);
        Require(first.Sections.SelectMany(section => section.Rows).Select(row => (row.Key, row.Label, row.Hint))
                .SequenceEqual(repeated.Sections.SelectMany(section => section.Rows).Select(row => (row.Key, row.Label, row.Hint))),
            "Repeated HUD projection must preserve the same summaries and ordered details.");
        Require(EcmaJson.Stringify(session.Snapshot()) == state && EcmaJson.Stringify(fixture.Engine.SaveBattle(session.Snapshot(), savedAt)) == save
            && session.Revision == 0 && session.ActionLog.Count == 0 && session.Events.Count == 0
            && !session.CanUndo && session.GetSnapshot().Transition is null,
            "Effect disclosure must not execute effects, alter saved state or randomness, dispatch actions, or create presentation transitions.");
        return first;
    }

    private static Fixture CreateFixture(bool fog, bool withEffects)
    {
        // Deliberately synthetic: detached content lowers damage, limits sight, and supplies
        // effect combinations absent from the campaign. No shipped level or asset is changed.
        var packs = PresentationJson.Load<JsonArray>("res://data/content-packs.json");
        JsonObject Definition(string family, string id) => packs.OfType<JsonObject>()
            .SelectMany(pack => pack[family]?.AsArray().OfType<JsonObject>() ?? [])
            .Single(value => value["id"]!.GetValue<string>() == id);
        foreach (var (unit, weapon) in new[]
        {
            ("c01.engineer", AttackWeapon), ("c01.rune-artificer", CounterWeapon), ("c01.templar", SupportWeapon),
        })
        {
            Definition("units", unit)["weapons"] = new JsonArray(weapon);
            Definition("units", unit)["vision"] = 1;
            Definition("weapons", weapon)["power"] = 1;
        }
        Definition("weapons", AttackWeapon)["hitEffects"] = new JsonArray(withEffects ? AttackEffects().Cast<JsonNode>().ToArray() : []);
        Definition("weapons", CounterWeapon)["hitEffects"] = new JsonArray(withEffects ? CounterEffects().Cast<JsonNode>().ToArray() : []);
        Definition("weapons", SupportWeapon)["hitEffects"] = new JsonArray(withEffects ? SupportEffects().Cast<JsonNode>().ToArray() : []);
        var engine = new BattleEngine(packs);
        var supporter = Unit("supporter", "c01.templar", 1, 1, 2);
        supporter["reaction"] = "support";
        var level = new JsonObject
        {
            ["schema"] = 3,
            ["id"] = "test.hud.hit-effects",
            ["width"] = 6,
            ["height"] = 4,
            ["terrain"] = new JsonArray("......", "......", "......", "......"),
            ["units"] = new JsonArray(Unit("actor", "c01.engineer", 1, 1, 1),
                Unit("primary", "c01.rune-artificer", 2, 2, 1), supporter,
                Unit("collateral", "c01.banner-guard", 2, 2, 2), Unit("concealed", "rogue", 2, 3, 1)),
            ["players"] = new JsonArray(Player(1), Player(2)),
            ["rules"] = new JsonObject { ["fog"] = fog, ["zoneOfControl"] = false, ["moraleEnabled"] = false, ["turnLimit"] = null },
        };
        var presentation = SessionPresentationText.Compose(new(engine, GodotTextCatalog.LoadSimplifiedChinese(), new Dictionary<string, string>()),
            new JsonArray(level.DeepClone()), BattleTextJson(), Godot.FileAccess.GetFileAsString("res://data/grayflag-content-text.json"));
        var authored = EcmaJson.Stringify(level);
        var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        var state = session.Snapshot();
        int Id(string key) => state["units"]!.AsArray().OfType<JsonObject>()
            .Single(unit => unit["key"]!.GetValue<string>() == key)["id"]!.GetValue<int>();
        var actor = Id("actor");
        var primary = Id("primary");
        var collateral = Id("collateral");
        var concealed = Id("concealed");
        var queries = session.GetTacticalQueries(51);
        var origin = new BoardCell(1, 1);
        var target = TacticalHit.Unit(new(2, 1), primary);
        var command = queries.CommandsAt(actor, origin).Single(option => option.Weapon == AttackWeapon);
        Require(queries.MoveStops(actor).Contains(origin) && command.TargetSubjects.Contains(TacticalTarget.Unit(target.Cell, primary)),
            "The synthetic forecast must be reachable through canonical movement and explicit unit-target command queries.");
        var selection = new TacticalSelection
        {
            Kind = TacticalSelectionKind.Target,
            UnitId = actor,
            Destination = origin,
            Path = queries.PathTo(actor, origin) ?? throw new InvalidOperationException("The fixture actor must have a canonical origin path."),
            Ability = command.Ability,
            Weapon = command.Weapon,
            Hostile = command.Hostile,
            TargetSubjects = command.TargetSubjects,
        };
        var plan = engine.Combat.PreviewAttack(state, actor, new(1, 1), new(2, 1), AttackWeapon);
        Require(plan.PrimaryUnit is { Interceptor: null, Counter.Weapon: CounterWeapon }
            && plan.SupportAttack is { Weapon: SupportWeapon } support && support.Attacker == Id("supporter") && support.Target == primary
            && plan.UnitHits.Select(hit => hit.Target).ToHashSet().SetEquals(new[] { primary, collateral, concealed })
            && plan.UnitHits.Count == 3 && plan.UnitHits[0].Target == primary
            && plan.UnitHits.All(hit => !hit.Killed && hit.Effects.Count == (withEffects ? 4 : 0))
            && plan.CounterEffects.Count == (withEffects ? 2 : 0) && support.Effects.Count == (withEffects ? 2 : 0),
            "The fixture must produce three real area-hit recipients plus a surviving counter and support effect phase.");
        Require(queries.Unit(primary) is not null && queries.Unit(collateral) is not null
            && (queries.Unit(concealed) is null) == fog && EcmaJson.Stringify(level) == authored,
            "Only the far collateral recipient is fog-hidden; fixture construction must preserve authored input.");
        return new(engine, session, presentation, queries.Stamp, selection, target, plan, primary, concealed, Id("supporter"));
    }

    private static JsonObject[] AttackEffects() =>
    [
        new() { ["type"] = "addStatus", ["status"] = "armor_down", ["duration"] = 2 },
        new() { ["type"] = "forcedMove", ["mode"] = "push", ["distance"] = 1, ["collisionDamage"] = 7 },
        new() { ["type"] = "forcedMove", ["mode"] = "pull", ["distance"] = 2, ["collisionDamage"] = 11 },
        new() { ["type"] = "removeStatus", ["status"] = "poisoned" },
    ];

    private static JsonObject[] CounterEffects() =>
    [
        new() { ["type"] = "removeStatus", ["status"] = "armor_down" },
        new() { ["type"] = "addStatus", ["status"] = "poisoned", ["duration"] = 3 },
    ];

    private static JsonObject[] SupportEffects() =>
    [
        new() { ["type"] = "addStatus", ["status"] = "silenced", ["duration"] = 1 },
        new() { ["type"] = "forcedMove", ["mode"] = "push", ["distance"] = 1, ["collisionDamage"] = 13 },
    ];

    private static JsonObject Unit(string key, string type, int owner, int x, int y) => new()
    {
        ["key"] = key,
        ["unit"] = type,
        ["owner"] = owner,
        ["x"] = x,
        ["y"] = y,
        ["facing"] = owner == 1 ? "east" : "west",
    };

    private static JsonObject Player(int id) => new()
    {
        ["id"] = id,
        ["team"] = id,
        ["color"] = id == 1 ? "#336699" : "#993333",
        ["controller"] = id == 1 ? "human" : "ai",
        ["resources"] = new JsonObject(),
        ["objectives"] = new JsonArray(new JsonObject { ["id"] = "rout", ["type"] = "routEnemies" }),
    };

    private static string BattleTextJson() => """
        {"schema":3,"levels":{"test.hud.hit-effects":{
          "title":{"literal":"Synthetic effect disclosure"},
          "description":{"literal":"Isolated projection contract, not a campaign battle."},
          "signals":{},"actors":{},"players":{
            "1":{"name":{"literal":"Allies"},"objectives":{"rout":{"generated":true}}},
            "2":{"name":{"literal":"Enemies"},"objectives":{"rout":{"generated":true}}}
          }
        }}}
        """;

    private static string Name(Fixture fixture, int id) =>
        fixture.Session.UnitName(EcmaJson.ToElement(TacticalQueries.RequireUnit(fixture.Session.Snapshot(), id)));

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
