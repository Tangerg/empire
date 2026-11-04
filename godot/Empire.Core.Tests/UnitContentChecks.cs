using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Abilities;
using Empire.Core.Composition;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.References;
using Empire.Core.State;
using Keys = Empire.Core.Composition.BattleCapabilities;

internal static class UnitContentChecks
{
    public static void Run(string oracleRoot)
    {
        var data = Path.GetFullPath(Path.Combine(oracleRoot, "../../data/content-packs.json"));
        var packs = EcmaJson.Parse(File.ReadAllText(data))!.AsArray();
        var engine = BattleEngineFactory.Create(new ReadOnlyContent(packs));
        var seed = Seed();
        Require(engine.UnitContentIssues(seed).Count == 0,
            "Unit content admission must accept cross-type unlocked careers and mastery without requiring a battlefield.");
        var opening = engine.CreateState(Level(seed));
        var unit = opening["units"]!.AsArray().OfType<JsonObject>().Single(value => value["key"]!.GetValue<string>() == "actor");
        Require(unit["resources"]!["test.open-resource"]!["current"]!.GetValue<double>() == 3
            && unit["career"]!["unlocked"]!.AsArray().Any(value => value!.GetValue<string>() == "ranger")
            && unit["career"]!["mastery"]!["ranger"]!.GetValue<double>() == 7,
            "The shared checks must preserve open resource names and previously acquired cross-type career progress.");

        Invalid(engine, value => value["unit"] = "absent", ["未知兵种 \"absent\"", "职业 militia 与兵种 absent 不匹配"]);
        Invalid(engine, value => value["career"] = "absent", ["单位 soldier 使用未知职业 \"absent\""]);
        Invalid(engine, value => value["career"] = "ranger", ["职业 ranger 与兵种 soldier 不匹配"]);
        Invalid(engine, value => value["unlockedCareers"] = new JsonArray("absent"), ["单位 soldier 解锁了未知职业 \"absent\""]);
        Invalid(engine, value => value["careerMastery"] = new JsonObject { ["absent"] = 0 }, ["单位 soldier 记录了未知职业熟练度 \"absent\""]);
        Invalid(engine, value => value["careerMastery"] = new JsonObject { ["militia"] = -1 }, ["单位 soldier 的职业熟练度必须 >= 0"]);
        Invalid(engine, value => value["learnedAbilities"] = new JsonArray("absent", "absent"), ["单位 actor 引用了未注册的能力「absent」"]);

        var noCareer = new JsonObject { ["unit"] = "soldier" };
        Require(engine.UnitContentIssues(noCareer).Count == 0, "Optional career and ability fields do not require fabricated defaults.");
        var malformed = Seed();
        malformed["unit"] = new JsonArray();
        ThrowsInvariant(() => engine.UnitContentIssues(malformed), "Malformed caller input must remain a programming invariant.");
        ComposedChecks(packs, engine);
    }

    private static void ComposedChecks(JsonArray packs, BattleEngine original)
    {
        var customPacks = packs.DeepClone().AsArray();
        var pack = customPacks.OfType<JsonObject>().Single(value => value["id"]!.GetValue<string>() == "empire.ancient-empires");
        var type = pack["units"]!.AsArray().OfType<JsonObject>().Single(value => value["id"]!.GetValue<string>() == "soldier").DeepClone().AsObject();
        type["id"] = "test.unit";
        pack["units"]!.AsArray().Add(type);
        var career = pack["careers"]!.AsArray().OfType<JsonObject>().Single(value => value["id"]!.GetValue<string>() == "militia").DeepClone().AsObject();
        career["id"] = "test.career";
        career["unitType"] = "test.unit";
        pack["careers"]!.AsArray().Add(career);
        var content = new ReadOnlyContent(customPacks);
        var levelCalls = 0;
        var defect = new DomainInvariantException("test unit reference checker defect");
        var plugin = new EnginePlugin("test.unit-admission", 1, context =>
        {
            context.Replace(Keys.Abilities, context.Require(Keys.Abilities).Clone()
                .Register(new("test.ability", AbilityTargetKind.Self)));
            var references = context.Require(Keys.ReferenceChecks).Clone();
            references.Replace(references.Get("abilities") with
            {
                Subject = "组合能力"
            });
            references.Register(new("test.unit-contract", "单位标记", _ => ["allowed"])
            {
                InUnit = (unit, rules) =>
                {
                    Require(ReferenceEquals(rules.Content, content), "Unit checks must use the actual composed content owner.");
                    var tag = unit["contentTag"]?.GetValue<string>();
                    if (tag == "throw")
                        throw defect;
                    return tag is null ? [] : [new("单位标记", tag)];
                },
                InLevel = (_, _) => { levelCalls++; return []; },
            });
            references.Register(new("test.state-only", "状态标记", _ => throw new DomainInvariantException("unrelated state check invoked"))
            {
                InState = (_, _) => [],
            });
            context.Replace(Keys.ReferenceChecks, references);
        }, overrides: [Keys.Abilities, Keys.ReferenceChecks], requiresCapabilities: [Keys.Abilities, Keys.ReferenceChecks]);
        var engine = BattleEngineFactory.Create(content, [plugin]);
        var seed = Seed();
        seed["unit"] = "test.unit";
        seed["career"] = "test.career";
        seed["learnedAbilities"] = new JsonArray("test.ability");
        seed["contentTag"] = "allowed";
        var before = EcmaJson.Stringify(seed);
        Require(engine.UnitContentIssues(seed).Count == 0 && levelCalls == 0,
            "Standalone unit admission must honor composed content and abilities without invoking whole-level checks.");
        engine.CreateState(Level(seed));
        Require(levelCalls == 1 && EcmaJson.Stringify(seed) == before,
            "Level admission must use the same unit checks and neither admission path may mutate its seed.");
        Require(original.UnitContentIssues(seed).Count >= 3,
            "Custom content and ability definitions must not leak into a separately composed engine.");

        seed["contentTag"] = "absent";
        Same(["单位标记 引用了未注册的单位标记「absent」"], engine.UnitContentIssues(seed),
            "A composed unit-reference extension must participate in standalone admission.");
        SameLevelIssues(engine, seed, engine.UnitContentIssues(seed));
        seed["contentTag"] = "allowed";
        seed["learnedAbilities"] = new JsonArray("absent");
        Same(["单位 actor 引用了未注册的组合能力「absent」"], engine.UnitContentIssues(seed),
            "Ability diagnostics must use the composed reference checker, not a second hard-coded list.");
        SameLevelIssues(engine, seed, engine.UnitContentIssues(seed));

        seed["contentTag"] = "throw";
        before = EcmaJson.Stringify(seed);
        try
        {
            engine.UnitContentIssues(seed);
            throw new Exception("The composed checker defect was swallowed.");
        }
        catch (DomainInvariantException error)
        {
            Require(ReferenceEquals(error, defect), "Checker defects must propagate unchanged, not become content diagnostics.");
        }
        Require(EcmaJson.Stringify(seed) == before, "A failed check must not mutate the inspected seed.");
    }

    private static void Invalid(BattleEngine engine, Action<JsonObject> change, IReadOnlyList<string> expected)
    {
        var seed = Seed();
        change(seed);
        var before = EcmaJson.Stringify(seed);
        Same(expected, engine.UnitContentIssues(seed), "Standalone unit diagnostics changed.");
        SameLevelIssues(engine, seed, expected);
        Require(EcmaJson.Stringify(seed) == before, "Rejected unit admission mutated the caller's seed.");
    }

    private static void SameLevelIssues(BattleEngine engine, JsonObject seed, IReadOnlyList<string> expected)
    {
        try
        {
            engine.CreateState(Level(seed));
            throw new Exception("The canonical level validator accepted an invalid unit.");
        }
        catch (BattleLevelException error)
        {
            Same(expected, error.Issues, "Level and standalone unit content diagnostics must agree exactly.");
        }
    }

    private static JsonObject Seed() => new()
    {
        ["key"] = "actor",
        ["unit"] = "soldier",
        ["career"] = "militia",
        ["unlockedCareers"] = new JsonArray("ranger"),
        ["careerMastery"] = new JsonObject { ["ranger"] = 7 },
        ["learnedAbilities"] = new JsonArray("wait"),
        ["resources"] = new JsonObject { ["test.open-resource"] = new JsonObject { ["current"] = 3, ["capacity"] = null } },
    };

    private static JsonObject Level(JsonObject seed)
    {
        var actor = seed.DeepClone().AsObject();
        actor["owner"] = 1;
        actor["x"] = 0;
        actor["y"] = 0;
        JsonObject Player(int id) => new()
        {
            ["id"] = id,
            ["team"] = id,
            ["controller"] = "human",
            ["resources"] = new JsonObject(),
            ["color"] = id == 1 ? "#336699" : "#993333",
            ["objectives"] = new JsonArray(new JsonObject { ["id"] = "rout", ["type"] = "routEnemies" }),
        };
        return new JsonObject
        {
            ["schema"] = 3,
            ["id"] = "test.unit-content",
            ["width"] = 3,
            ["height"] = 1,
            ["terrain"] = new JsonArray("..."),
            ["players"] = new JsonArray(Player(1), Player(2)),
            ["units"] = new JsonArray(actor, new JsonObject { ["key"] = "opponent", ["unit"] = "soldier", ["owner"] = 2, ["x"] = 2, ["y"] = 0 }),
            ["rules"] = new JsonObject { ["fog"] = false },
        };
    }

    private static void ThrowsInvariant(Action action, string message)
    {
        try
        {
            action();
        }
        catch (DomainInvariantException) { return; }
        throw new Exception(message);
    }
    private static void Same(IReadOnlyList<string> expected, IReadOnlyList<string> actual, string message) =>
        Require(expected.SequenceEqual(actual, StringComparer.Ordinal), message + " Actual: " + string.Join("; ", actual));
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
