using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Determinism;
using Empire.Core.Engine;

var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
JsonNode Read(string file) => JsonNode.Parse(File.ReadAllText(Path.Combine(root, file)))!;
var packs = Read("godot/data/content-packs.json").AsArray();
var engine = new GrayFlagEngine(packs);
var initial = Read("godot/tests/oracle/grayflag-0.58.json")["initialState"]!.AsObject();
var passed = 0;
JsonObject Selector(string? zone = null) => zone is null ? new JsonObject() : new JsonObject { ["zone"] = zone };
JsonObject Directive(string mode = "guard") => new() { ["type"] = "setUnitDirective", ["selector"] = Selector(),
    ["directive"] = new JsonObject { ["mode"] = mode, ["waypoints"] = new JsonArray(new JsonObject { ["x"] = 0, ["y"] = 0 }) } };
JsonObject SpawnSource(string type = "soldier") => new() { ["unit"] = type, ["owner"] = 1, ["x"] = 0, ["y"] = 0 };
JsonObject Spawn(JsonObject? source = null) => new() { ["type"] = "spawnUnits", ["units"] = new JsonArray(source ?? SpawnSource()) };
JsonObject WithEffect(GrayFlagEngine rules, JsonObject effect)
{
    var save = rules.SaveBattle(initial, "2026-01-01T00:00:00.000Z");
    save["state"]!["scenario"]!["triggers"]!.AsArray().Add(new JsonObject
    {
        ["id"] = "reference-probe", ["timing"] = "afterAction", ["once"] = true,
        ["condition"] = new JsonObject { ["type"] = "turnAtLeast", ["turn"] = 999 },
        ["effects"] = new JsonArray(effect),
    });
    return save;
}
void Check(string name, JsonObject effect, bool accepted, Action<JsonObject>? edit = null, GrayFlagEngine? rules = null)
{
    rules ??= engine;
    var save = WithEffect(rules, effect); edit?.Invoke(save);
    var before = save.ToJsonString();
    try
    {
        var loaded = rules.LoadBattle(save);
        if (!accepted) throw new Exception($"{name}: expected rejection");
        if (StateDigest.Hash(loaded) != StateDigest.Hash(save["state"]!)) throw new Exception($"{name}: altered accepted state");
    }
    catch (StoredDocumentException error)
    {
        if (accepted) throw new Exception($"{name}: rejected valid reference", error);
        var live = save["state"]!.DeepClone().AsObject(); var liveBefore = live.ToJsonString();
        try { rules.SaveBattle(live); throw new Exception($"{name}: save accepted rejected state"); }
        catch (DomainInvariantException) { }
        if (live.ToJsonString() != liveBefore) throw new Exception($"{name}: rejected save mutated live state");
    }
    if (save.ToJsonString() != before) throw new Exception($"{name}: load mutated input document");
    passed++;
}
void BadDirective(string name, Action<JsonObject> change)
{
    var effect = Directive(); change(effect["directive"]!.AsObject()); Check(name, effect, false);
}
Check("unknown future directive", Directive("unregistered-mode"), false);
BadDirective("unknown future directive zone", d => d["zone"] = "missing-zone");
BadDirective("offboard waypoint", d => d["waypoints"]![0]!["x"] = -1);
BadDirective("fractional waypoint", d => d["waypoints"]![0]!["x"] = 0.5);
BadDirective("damaged waypoint list", d => d["waypoints"] = new JsonObject());
BadDirective("missing mode", d => d.Remove("mode"));
foreach (var mode in new[] { "assault", "guard", "patrol", "retreat" }) Check($"supported {mode}", Directive(mode), true);
var selected = Directive(); selected["selector"] = Selector("missing-zone"); Check("selector missing zone", selected, false);
foreach (var kind in new[] { "changeUnitOwner", "changeMorale" })
    Check($"{kind} selector missing zone", new JsonObject { ["type"] = kind, ["selector"] = Selector("missing-zone"), ["owner"] = 1, ["amount"] = 0 }, false);
var neutral = Directive(); neutral["selector"] = new JsonObject { ["owner"] = 0, ["ids"] = new JsonArray(99999), ["keys"] = new JsonArray("not-arrived") };
Check("neutral and absent selector targets remain valid", neutral, true);
var knownZone = Directive(); knownZone["directive"]!["zone"] = "empty-zone";
Check("empty declared zone", knownZone, true, save => save["state"]!["scenario"]!["zones"]!["empty-zone"] = new JsonArray());
void BadSpawn(string name, Action<JsonObject> change)
{
    var source = SpawnSource(); change(source); Check(name, Spawn(source), false);
}
BadSpawn("future unit type absent", s => s["unit"] = "missing-unit");
BadSpawn("future owner absent", s => s["owner"] = 999);
BadSpawn("future neutral owner absent", s => s["owner"] = 0);
BadSpawn("future cell offboard", s => s["x"] = -1);
BadSpawn("future cell fractional", s => s["y"] = 0.5);
BadSpawn("future learned ability", s => s["learnedAbilities"] = new JsonArray("unknown-ability"));
BadSpawn("future reaction", s => s["reaction"] = "unknown-reaction");
BadSpawn("future directive", s => s["directive"] = new JsonObject { ["mode"] = "unknown-mode" });
BadSpawn("future directive zone", s => s["directive"] = new JsonObject { ["mode"] = "guard", ["zone"] = "missing-zone" });
BadSpawn("future formation", s => s["formation"] = "unknown-formation");
BadSpawn("future career", s => s["career"] = "unknown-career");
BadSpawn("future account adapter", s => s["resources"] = new JsonObject { ["unknown-resource"] = new JsonObject { ["current"] = 1, ["capacity"] = null } });
Check("valid future default unit", Spawn(), true);
var occupied = SpawnSource(); occupied["x"] = initial["units"]![0]!["x"]!.DeepClone(); occupied["y"] = initial["units"]![0]!["y"]!.DeepClone();
occupied["key"] = initial["units"]![0]!["key"]?.DeepClone(); Check("future occupancy and key collision not pre-judged", Spawn(occupied), true);
Check("missing former structure remains valid no-op target", new JsonObject { ["type"] = "damageStructure", ["id"] = "former-structure", ["amount"] = 1 }, true);
Check("removing absent engagement remains valid no-op", new JsonObject { ["type"] = "removeEngagementRule", ["id"] = "former-rule" }, true);
Check("unsupported effect still rejected", new JsonObject { ["type"] = "unknown-effect", ["id"] = "former-structure", ["amount"] = 1 }, false);

// Broader declaration reference checks for the remaining composed built-ins.
JsonObject Effect(string type) => new() { ["type"] = type, ["selector"] = Selector() };
foreach (var kind in new[] { "addStatus", "removeStatus" })
{
    var bad = Effect(kind); bad["status"] = "unknown-status"; bad["duration"] = 2; Check($"{kind} unknown status", bad, false);
    var good = Effect(kind); good["status"] = "poisoned"; good["duration"] = 2; Check($"{kind} known status", good, true);
}
foreach (var kind in new[] { "teleportUnits", "restoreWithdrawnUnits", "replaceTerrain", "addOverlay", "setElevation", "addElevation" })
{
    var bad = Effect(kind); bad["zone"] = "unknown-zone"; bad["terrain"] = "plain";
    bad["overlay"] = "flooded"; bad["value"] = 1; bad["amount"] = 1;
    Check($"{kind} unknown zone", bad, false);
    var good = bad.DeepClone().AsObject(); good["zone"] = "probe-zone";
    Check($"{kind} known empty zone", good, true, save => save["state"]!["scenario"]!["zones"]!["probe-zone"] = new JsonArray());
}
foreach (var (kind, field) in new[] { ("replaceTerrain", "terrain"), ("addOverlay", "overlay") })
{
    var bad = Effect(kind); bad[field] = "unknown-content";
    Check($"{kind} unknown content", bad, false);
}
foreach (var (kind, field) in new[] { ("setElevation", "value"), ("addElevation", "amount") })
{
    var bad = Effect(kind); bad[field] = 1.5; Check($"{kind} fractional value", bad, false);
}
var move = Effect("forceMove"); move["source"] = new JsonObject { ["x"] = 0, ["y"] = 0 }; move["distance"] = 1;
Check("forced move valid source", move.DeepClone().AsObject(), true);
move["source"]!["x"] = -1; Check("forced move offboard source", move.DeepClone().AsObject(), false);
move["source"]!["x"] = 0; move["distance"] = 1.5; Check("forced move fractional distance", move, false);
var cliff = Effect("setCliffs"); cliff["edges"] = new JsonArray(new JsonObject { ["from"] = new JsonObject { ["x"] = 0, ["y"] = 0 }, ["to"] = new JsonObject { ["x"] = 1, ["y"] = 0 } });
Check("cliff adjacent edge", cliff.DeepClone().AsObject(), true);
cliff["edges"]![0]!["to"]!["x"] = 2; Check("cliff nonadjacent edge", cliff, false);
var cover = Effect("setDirectionalCover"); cover["covers"] = new JsonArray(new JsonObject { ["at"] = new JsonObject { ["x"] = -1, ["y"] = 0 } });
Check("cover offboard cell", cover, false);
Check("repair missing structure rejected", new JsonObject { ["type"] = "repairStructure", ["id"] = "former-structure", ["amount"] = 1 }, false);
Check("move missing composite rejected", new JsonObject { ["type"] = "moveComposite", ["id"] = "unknown-composite", ["dx"] = 0, ["dy"] = 0 }, false);
foreach (var kind in new[] { "activateObjective", "cancelObjective", "completeObjective", "revealObjective" })
{
    Check($"{kind} unknown objective", new JsonObject { ["type"] = kind, ["player"] = 1, ["id"] = "unknown-objective" }, false);
    Check($"{kind} known nested objective", new JsonObject { ["type"] = kind, ["player"] = 1, ["id"] = "route" }, true);
}
Check("unknown resource adapter", new JsonObject { ["type"] = "changeUnitResource", ["selector"] = Selector(), ["resource"] = "unknown-resource", ["amount"] = 1 }, false);
Check("known resource adapter", new JsonObject { ["type"] = "changeUnitResource", ["selector"] = Selector(), ["resource"] = "momentum", ["amount"] = 1 }, true);
Check("team unknown player", new JsonObject { ["type"] = "setPlayerTeam", ["player"] = 999, ["team"] = 1 }, false);
void ConditionCheck(string name, JsonObject condition, bool accepted)
{
    Check(name, new JsonObject { ["type"] = "emitSignal", ["signal"] = "probe" }, accepted,
        save => save["state"]!["scenario"]!["triggers"]!.AsArray().Last()!["condition"] = condition);
}
ConditionCheck("nested unknown condition", new JsonObject { ["type"] = "not", ["condition"] = new JsonObject { ["type"] = "unknown-condition" } }, false);
ConditionCheck("nested condition missing zone", new JsonObject { ["type"] = "all", ["conditions"] = new JsonArray(new JsonObject { ["type"] = "unitInZone", ["zone"] = "unknown-zone", ["selector"] = Selector() }) }, false);
ConditionCheck("removed structure condition remains valid", new JsonObject { ["type"] = "structure", ["id"] = "former-structure", ["state"] = "destroyed" }, true);
ConditionCheck("fractional cycle rejected", new JsonObject { ["type"] = "turnCycle", ["every"] = 1.5 }, false);

GrayFlagEngine FutureCatalog(Action<JsonObject, JsonObject, JsonObject> edit)
{
    var changed = packs.DeepClone().AsArray();
    var unit = changed.OfType<JsonObject>().SelectMany(p => p["units"]?.AsArray().OfType<JsonObject>() ?? []).First(u => u["id"]!.GetValue<string>() == "soldier").DeepClone().AsObject();
    unit["id"] = "future-unit"; unit["weapons"] = new JsonArray("future-weapon");
    var weapon = changed.OfType<JsonObject>().SelectMany(p => p["weapons"]?.AsArray().OfType<JsonObject>() ?? []).First(w => w["id"]!.GetValue<string>() == "soldier_sword").DeepClone().AsObject();
    weapon["id"] = "future-weapon";
    var career = changed.OfType<JsonObject>().SelectMany(p => p["careers"]?.AsArray().OfType<JsonObject>() ?? []).First(c => c["id"]!.GetValue<string>() == "militia").DeepClone().AsObject();
    career["id"] = "future-career"; career["unitType"] = "future-unit";
    edit(unit, weapon, career);
    changed.Add(new JsonObject { ["id"] = "reference-probe", ["version"] = 1,
        ["units"] = new JsonArray(unit), ["weapons"] = new JsonArray(weapon), ["careers"] = new JsonArray(career) });
    return new GrayFlagEngine(changed);
}
foreach (var (name, edit) in new (string, Action<JsonObject, JsonObject, JsonObject>)[]
{
    ("future type ability", (u,w,c) => u["abilities"]!.AsArray().Add("unknown-ability")),
    ("future default reaction", (u,w,c) => u["defaultReaction"] = "unknown-reaction"),
    ("future weapon area", (u,w,c) => w["area"] = "unknown-area"),
    ("future weapon effect", (u,w,c) => w["hitEffects"]!.AsArray().Add(new JsonObject { ["type"] = "unknown-effect" })),
    ("future weapon account", (u,w,c) => w["resources"]!["unknown-resource"] = new JsonObject { ["current"] = 1, ["capacity"] = null }),
    ("future weapon cost", (u,w,c) => w["resourceCosts"]!.AsArray().Add(new JsonObject { ["resource"] = "unknown-resource", ["amount"] = 1 })),
    ("future unit account", (u,w,c) => u["resources"]!["unknown-resource"] = new JsonObject { ["current"] = 1, ["capacity"] = null }),
    ("future mastered ability", (u,w,c) => c["masteryAbilities"]!.AsArray().Add("unknown-ability")),
})
{
    try { _ = FutureCatalog(edit); throw new Exception($"{name}: composition admitted unsupported future content"); }
    catch (DomainInvariantException) { passed++; }
}
Check("valid prospective content", Spawn(SpawnSource("future-unit")), true, rules: FutureCatalog((u,w,c) => { }));
foreach (var level in Read("godot/data/grayflag-campaign-levels.json").AsArray().OfType<JsonObject>())
{
    var live = engine.CreateState(level); var save = engine.SaveBattle(live); var copy = engine.LoadBattle(save);
    if (StateDigest.Hash(live) != StateDigest.Hash(copy)) throw new Exception($"campaign round trip changed {level["id"]}");
    passed++;
}
Console.WriteLine($"PASS {passed} scenario payload-reference admission and atomicity checks");
var levelGuards = 0;
void LevelGuard(string name, Action<JsonObject> mutate, bool accepted = false)
{
    var level = Read("godot/data/grayflag-level.json").AsObject(); mutate(level);
    var before = level.ToJsonString();
    try { _ = engine.CreateState(level); if (!accepted) throw new Exception($"{name}: admitted unsupported declaration"); }
    catch (StoredDocumentException) { if (accepted) throw; }
    if (level.ToJsonString() != before) throw new Exception($"{name}: admission mutated level");
    levelGuards++;
}
LevelGuard("unknown nested failOn condition", level => level["players"]![0]!["objectives"]![0]!["condition"] = new JsonObject { ["type"] = "not", ["condition"] = new JsonObject { ["type"] = "unknown-condition" } });
void FutureLevel(string name, Action<JsonObject> edit, bool accepted = false)
{
    LevelGuard(name, level =>
    {
        var source = SpawnSource(); edit(source);
        level["scenario"]!["triggers"]!.AsArray().Add(new JsonObject { ["id"] = "future-capability", ["timing"] = "afterAction", ["condition"] = new JsonObject { ["type"] = "turnAtLeast", ["turn"] = 999 }, ["effects"] = new JsonArray(Spawn(source)) });
    }, accepted);
}
FutureLevel("unknown future learned ability", source => source["learnedAbilities"] = new JsonArray("unknown-ability"));
FutureLevel("unknown future reaction", source => source["reaction"] = "unknown-reaction");
FutureLevel("unknown future directive", source => source["directive"] = new JsonObject { ["mode"] = "unknown-directive" });
FutureLevel("unknown future resource", source => source["resources"] = new JsonObject { ["unknown-resource"] = new JsonObject { ["current"] = 0, ["capacity"] = null } });
FutureLevel("supported future overrides", source => { source["reaction"] = "counter"; source["learnedAbilities"] = new JsonArray("heal"); source["directive"] = new JsonObject { ["mode"] = "guard" }; }, true);
LevelGuard("unknown initial resource", level => level["units"]![0]!["resources"] = new JsonObject { ["unknown-resource"] = new JsonObject { ["current"] = 0, ["capacity"] = null } });
LevelGuard("unknown player resource", level => level["players"]![0]!["resources"]!["unknown-resource"] = new JsonObject { ["current"] = 0, ["capacity"] = null });
LevelGuard("unknown base resource grant", level => level["rules"]!["baseResourceGrants"] = new JsonArray(new JsonObject { ["subject"] = "player", ["resource"] = "unknown-resource", ["amount"] = 1 }));
LevelGuard("unknown scenario resource adapter", level => level["scenario"]!["triggers"]!.AsArray().Add(new JsonObject { ["id"] = "resource-probe", ["timing"] = "afterAction", ["condition"] = new JsonObject { ["type"] = "turnAtLeast", ["turn"] = 999 }, ["effects"] = new JsonArray(new JsonObject { ["type"] = "changeUnitResource", ["selector"] = Selector(), ["resource"] = "unknown-resource", ["amount"] = 1 }) }));
Console.WriteLine($"PASS {levelGuards} explicit native execution-capability admission guards; authored inputs unchanged");
