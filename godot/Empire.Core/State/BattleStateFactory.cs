using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Core.Geometry;

namespace Empire.Core.State;

public sealed record BattleStateRules(ReadOnlyContent Content, GridRegistry Grids);

/// <summary>
/// Constructs the plain-data, pre-lifecycle state from a parsed LevelData document.
/// This does not start the first actor turn, evaluate objectives, or execute triggers.
/// </summary>
public static class BattleStateFactory
{
    /// <summary>Spawns through the same unit constructor used by initial levels.</summary>
    public static JsonObject SpawnUnit(JsonArray contentPacks, GridRegistry grids, JsonObject state,
        string type, int owner, Coord at, double? hp = null, bool done = false, JsonObject? source = null) =>
        SpawnUnit(new BattleStateRules(new ReadOnlyContent(contentPacks), grids), state, type, owner, at, hp, done, source);

    public static JsonObject SpawnUnit(BattleStateRules dependencies, JsonObject state,
        string type, int owner, Coord at, double? hp = null, bool done = false, JsonObject? source = null)
    {
        var seed = new JsonObject { ["x"] = at.X, ["y"] = at.Y, ["unit"] = type, ["owner"] = owner };
        if (source is not null)
            foreach (var pair in source) seed[pair.Key] = pair.Value?.DeepClone();
        if (hp is { } explicitHp) seed["hp"] = explicitHp;
        var id = StateJson.Integer(state, "nextUnitId");
        var grid = dependencies.Grids.Get(StateJson.Text(state["rules"]!["grid"], "grid"));
        state["nextUnitId"] = checked(id + 1);
        var unit = CreateUnit(dependencies.Content.Source, seed, id, grid.RestingFacing);
        unit["done"] = done;
        StateJson.Array(state["units"], "units").Add(unit);
        return unit;
    }

    public static JsonObject Create(JsonObject level, JsonArray contentPacks, uint? seed = null) =>
        Create(new BattleStateRules(new ReadOnlyContent(contentPacks), GridRegistry.Core()), level, seed);

    public static JsonObject Create(BattleStateRules dependencies, JsonObject level, uint? seed = null)
    {
        ArgumentNullException.ThrowIfNull(level);
        var content = dependencies.Content.Source;
        var map = StateMap.Create(content, level);
        var rules = StateDefaults.Rules(level);
        var restingFacing = dependencies.Grids.Get(StateJson.Text(rules, "grid")).RestingFacing;
        var players = CreatePlayers(content, level, map);
        var units = new JsonArray();
        var nextUnitId = 1;
        foreach (var source in StateJson.Objects(StateJson.Required(level, "units"), "units"))
            units.Add(CreateUnit(content, source, nextUnitId++, restingFacing));
        var commanders = CreateCommanders(level, units);
        var structures = CreateStructures(content, level);
        var composites = CreateComposites(level, structures);
        var scenario = CreateScenario(level);
        var deployment = CreateDeployment(level, players, units, (JsonObject)scenario["zones"]!);
        var currentPlayer = deployment is not null
            ? ((JsonArray)deployment["order"]!)[0]!.DeepClone()
            : players.Count > 0 ? players[0]!["id"]!.DeepClone() : JsonValue.Create(1);

        return new JsonObject
        {
            ["levelId"] = StateJson.Required(level, "id").DeepClone(),
            ["levelName"] = StateJson.Required(level, "name").DeepClone(),
            ["map"] = map,
            ["units"] = units,
            ["structures"] = structures,
            ["composites"] = composites,
            ["embarkedUnits"] = new JsonArray(),
            ["markers"] = new JsonArray(),
            ["commanders"] = commanders,
            ["players"] = players,
            ["rules"] = rules,
            ["turn"] = 1,
            ["currentPlayer"] = currentPlayer,
            ["phase"] = deployment is null ? "playing" : "deployment",
            ["winnerTeam"] = null,
            ["endReason"] = "",
            ["nextUnitId"] = nextUnitId,
            ["nextMarkerId"] = 1,
            ["deployment"] = deployment,
            ["turnOrder"] = new JsonObject
            {
                ["policy"] = rules["turnOrder"]?.DeepClone(),
                ["activeUnit"] = null,
                ["data"] = new JsonObject(),
            },
            ["actorTurns"] = 0,
            ["pendingCasts"] = new JsonArray(),
            ["random"] = new JsonObject
            {
                ["seed"] = seed ?? StateDefaults.LevelSeed(StateJson.Text(level, "id")),
                ["counters"] = new JsonObject(),
            },
            ["scenario"] = scenario,
        };
    }

    private static JsonArray CreatePlayers(StateContent content, JsonObject level, JsonObject map)
    {
        var result = new JsonArray();
        var players = StateJson.Objects(StateJson.Required(level, "players"), "players");
        foreach (var source in players.OrderBy(player => StateJson.Number(player["id"], "player id")))
        {
            var id = StateJson.Integer(source, "id");
            var authoredObjectives = source["objectives"] as JsonArray;
            var declarations = authoredObjectives is { Count: > 0 }
                ? authoredObjectives
                : level["victory"] is { } victory ? StateJson.Array(victory, "victory") : StateDefaults.Victory();
            var objectives = StateObjectives.AssignIds(declarations, $"player-{id}");
            var ownsHq = false;
            var owners = (JsonArray)map["owners"]!;
            var tiles = (JsonArray)map["tiles"]!;
            for (var index = 0; index < owners.Count; index++)
            {
                if (StateJson.Integer(owners[index], "tile owner") != id) continue;
                if (StateJson.IsTrue(content.Get("terrains", StateJson.Text(tiles[index], "terrain"))["hq"]))
                {
                    ownsHq = true;
                    break;
                }
            }
            var ai = source["ai"] is { } authoredAi ? StateJson.Object(authoredAi, "player ai") : null;
            result.Add(new JsonObject
            {
                ["id"] = id,
                ["name"] = StateJson.Required(source, "name").DeepClone(),
                ["team"] = source["team"]?.DeepClone() ?? JsonValue.Create(id),
                ["color"] = StateJson.Required(source, "color").DeepClone(),
                ["controller"] = StateJson.Required(source, "controller").DeepClone(),
                ["resources"] = StateJson.CopyObject(source["resources"], "player resources"),
                ["alive"] = true,
                ["startedWithHQ"] = ownsHq,
                ["objectives"] = objectives,
                ["objectiveStates"] = StateObjectives.RuntimeStates(objectives),
                ["ai"] = new JsonObject { ["aggression"] = ai?["aggression"]?.DeepClone() ?? JsonValue.Create(0.5) },
            });
        }
        return result;
    }

    private static JsonObject CreateUnit(StateContent content, JsonObject source, int id, string restingFacing)
    {
        var type = StateJson.Text(source, "unit");
        var definition = content.Get("units", type);
        var maximumHp = StateJson.Number(definition["maxHp"], "maxHp");
        var moraleDefinition = definition["morale"] is { } morale ? StateJson.Object(morale, "morale") : null;
        var maximumMorale = moraleDefinition is null ? 100 : StateJson.Number(moraleDefinition, "maximum", 100);
        var weaponState = new JsonObject();
        foreach (var entry in StateJson.Array(definition["weapons"], "weapons"))
        {
            var weaponId = StateJson.Text(entry, "weapon id");
            var weapon = content.Get("weapons", weaponId);
            weaponState[weaponId] = new JsonObject
            {
                ["cooldownRemaining"] = 0,
                ["resources"] = StateJson.CopyObject(weapon["resources"], "weapon resources"),
            };
        }
        var directiveSource = source["directive"] is { } authoredDirective
            ? StateJson.Object(authoredDirective, "directive") : new JsonObject();
        var directive = new JsonObject
        {
            ["mode"] = directiveSource["mode"]?.DeepClone() ?? JsonValue.Create("assault"),
            ["waypoints"] = StateJson.CopyArray(directiveSource["waypoints"], "directive waypoints"),
            ["cursor"] = Math.Max(0, StateJson.Round(StateJson.Number(directiveSource, "cursor", 0))),
        };
        StateJson.CopyIfPresent(directiveSource, directive, "zone");
        var result = new JsonObject
        {
            ["id"] = id,
            ["type"] = type,
            ["owner"] = StateJson.Required(source, "owner").DeepClone(),
            ["x"] = StateJson.Required(source, "x").DeepClone(),
            ["y"] = StateJson.Required(source, "y").DeepClone(),
            ["hp"] = PlacedGauge(StateJson.Number(source, "hp", maximumHp), maximumHp),
            ["done"] = false,
            ["capture"] = 0,
            ["statuses"] = new JsonArray(),
            ["weaponState"] = weaponState,
            ["commanderId"] = source["commander"]?.DeepClone(),
            ["rank"] = source["rank"]?.DeepClone() ?? JsonValue.Create(0),
            ["rankProgress"] = Math.Max(0, StateJson.Round(StateJson.Number(source, "rankProgress", 0))),
            ["resources"] = StateJson.MergeObjects(definition["resources"], source["resources"], "unit resources"),
            ["reaction"] = source["reaction"]?.DeepClone() ?? StateJson.Required(definition, "defaultReaction").DeepClone(),
            ["reactionUsedRound"] = -1,
            ["facing"] = source["facing"]?.DeepClone() ?? JsonValue.Create(restingFacing),
            ["morale"] = new JsonObject
            {
                ["current"] = PlacedGauge(StateJson.Number(source, "morale", maximumMorale), maximumMorale),
                ["maximum"] = maximumMorale,
                ["resilience"] = Math.Max(0, Math.Min(0.9,
                    moraleDefinition is null ? 0 : StateJson.Number(moraleDefinition, "resilience", 0))),
            },
            ["formation"] = source["formation"]?.DeepClone(),
            ["directive"] = directive,
            ["career"] = CreateCareer(content, source),
            ["learnedAbilities"] = StateJson.UniqueStrings(source["learnedAbilities"], "learnedAbilities"),
        };
        StateJson.CopyIfPresent(source, result, "key");
        return result;
    }

    private static double PlacedGauge(double value, double maximum) =>
        Math.Max(1, Math.Min(maximum, StateJson.Round(value)));

    private static JsonObject CreateCareer(StateContent content, JsonObject source)
    {
        var type = StateJson.Text(source, "unit");
        var requestedId = source["career"] is { } authored ? StateJson.Text(authored, "career") : null;
        var requested = !string.IsNullOrEmpty(requestedId)
            ? content.TryGet("careers", requestedId)
            : content.All("careers").Where(career => StateJson.Text(career, "unitType") == type)
                .OrderBy(career => StateJson.Number(career["tier"], "career tier"))
                .ThenBy(career => StateJson.Text(career, "id"), StringComparer.Ordinal).FirstOrDefault();
        if (requested is not null && StateJson.Text(requested, "unitType") != type)
            throw new DomainInvariantException($"career {StateJson.Text(requested, "id")} does not use unit type \"{type}\"");
        var current = requested is null ? null : StateJson.Text(requested, "id");
        var unlocked = StateJson.UniqueStrings(source["unlockedCareers"], "unlockedCareers");
        var mastery = new JsonObject();
        if (!string.IsNullOrEmpty(current))
        {
            if (!unlocked.Any(entry => StateJson.Text(entry, "career id") == current)) unlocked.Add(current);
            mastery[current] = Math.Max(0, StateJson.Round(StateJson.Number(source, "rankProgress", 0)));
        }
        if (source["careerMastery"] is { } authoredMastery)
            foreach (var pair in StateJson.Object(authoredMastery, "careerMastery"))
                mastery[pair.Key] = pair.Value?.DeepClone();
        return new JsonObject { ["current"] = current, ["unlocked"] = unlocked, ["mastery"] = mastery };
    }

    private static JsonArray CreateCommanders(JsonObject level, JsonArray units)
    {
        var result = new JsonArray();
        foreach (var source in StateJson.Objects(level["commanders"], "commanders"))
        {
            var id = StateJson.Text(source, "id");
            var unitKey = StateJson.Text(source, "unitKey");
            var leader = units.OfType<JsonObject>().FirstOrDefault(unit =>
                unit["key"] is JsonValue key && key.TryGetValue<string>(out var text) && text == unitKey);
            if (leader is null)
                throw new DomainInvariantException($"commander {id} references unknown unit key \"{unitKey}\"");
            if (leader["commanderId"] is null) leader["commanderId"] = id;
            var aura = source["aura"] is { } authoredAura ? StateJson.Object(authoredAura, "aura") : new JsonObject();
            result.Add(new JsonObject
            {
                ["id"] = id,
                ["unitId"] = leader["id"]!.DeepClone(),
                ["owner"] = leader["owner"]!.DeepClone(),
                ["radius"] = Math.Max(0, StateJson.Number(source["radius"], "commander radius")),
                ["aura"] = new JsonObject
                {
                    ["attackMultiplier"] = aura["attackMultiplier"]?.DeepClone() ?? JsonValue.Create(1),
                    ["defenseDelta"] = aura["defenseDelta"]?.DeepClone() ?? JsonValue.Create(0),
                    ["movementDelta"] = aura["movementDelta"]?.DeepClone() ?? JsonValue.Create(0),
                },
                ["turnGrants"] = StateJson.CopyArray(source["turnGrants"], "turnGrants"),
                ["tactics"] = StateJson.CopyArray(source["tactics"], "tactics"),
                ["usedTactics"] = new JsonArray(),
            });
        }
        return result;
    }

    private static JsonArray CreateStructures(StateContent content, JsonObject level)
    {
        var result = new JsonArray();
        foreach (var source in StateJson.Objects(level["structures"], "structures"))
        {
            var type = StateJson.Text(source, "type");
            var definition = content.Get("structures", type);
            var maximumHp = StateJson.Number(definition["maxHp"], "structure maxHp");
            result.Add(new JsonObject
            {
                ["id"] = StateJson.Required(source, "id").DeepClone(),
                ["type"] = type,
                ["owner"] = source["owner"]?.DeepClone() ?? JsonValue.Create(0),
                ["x"] = StateJson.Required(source, "x").DeepClone(),
                ["y"] = StateJson.Required(source, "y").DeepClone(),
                ["hp"] = Math.Max(0, Math.Min(maximumHp, StateJson.Round(StateJson.Number(source, "hp", maximumHp)))),
                ["disabled"] = source["disabled"]?.DeepClone() ?? JsonValue.Create(false),
                ["statuses"] = new JsonArray(),
            });
        }
        return result;
    }

    private static JsonArray CreateComposites(JsonObject level, JsonArray structures)
    {
        var ids = structures.OfType<JsonObject>().Select(structure => StateJson.Text(structure, "id"))
            .ToHashSet(StringComparer.Ordinal);
        var result = new JsonArray();
        foreach (var source in StateJson.Objects(level["composites"], "composites"))
        {
            var id = StateJson.Text(source, "id");
            var parts = StateJson.UniqueStrings(StateJson.Required(source, "parts"), "composite parts");
            foreach (var part in parts)
            {
                var partId = StateJson.Text(part, "structure id");
                if (!ids.Contains(partId))
                    throw new DomainInvariantException($"composite {id} references unknown structure \"{partId}\"");
            }
            if (parts.Count == 0)
                throw new DomainInvariantException($"composite {id} must include at least one structure");
            result.Add(new JsonObject
            {
                ["id"] = id,
                ["parts"] = parts,
                ["minimumNeutralized"] = Math.Max(1, Math.Min(parts.Count,
                    StateJson.Round(StateJson.Number(source, "minimumNeutralized", parts.Count)))),
                ["tags"] = StateJson.UniqueStrings(source["tags"], "composite tags"),
            });
        }
        return result;
    }

    private static JsonObject CreateScenario(JsonObject level)
    {
        var source = level["scenario"] is { } authored ? StateJson.Object(authored, "scenario") : new JsonObject();
        var zones = new JsonObject();
        var zoneTags = new JsonObject();
        foreach (var zone in StateJson.Objects(source["zones"], "scenario zones"))
        {
            var id = StateJson.Text(zone, "id");
            zones[id] = StateJson.CopyArray(StateJson.Required(zone, "cells"), "zone cells");
            zoneTags[id] = StateJson.UniqueStrings(zone["tags"], "zone tags");
        }
        var overlays = new JsonArray();
        foreach (var overlay in StateJson.Objects(source["overlays"], "scenario overlays"))
        {
            var zone = StateJson.Text(overlay, "zone");
            overlays.Add(new JsonObject
            {
                ["id"] = StateJson.Required(overlay, "id").DeepClone(),
                ["type"] = StateJson.Required(overlay, "type").DeepClone(),
                ["cells"] = StateJson.CopyArray(zones[zone], "overlay cells"),
                ["remainingRounds"] = overlay["remainingRounds"]?.DeepClone(),
            });
        }
        var engagementRules = new JsonArray();
        foreach (var rule in StateJson.Objects(source["engagementRules"], "engagementRules"))
        {
            var copy = (JsonObject)rule.DeepClone();
            // `rule.players?.slice()` serializes neither an absent nor an explicit null list.
            if (rule["players"] is null) copy.Remove("players");
            else copy["players"] = StateJson.CopyArray(rule["players"], "engagement rule players");
            engagementRules.Add(copy);
        }
        return new JsonObject
        {
            ["variables"] = StateJson.CopyObject(source["variables"], "scenario variables"),
            ["zones"] = zones,
            ["overlays"] = overlays,
            ["triggers"] = StateJson.CopyArray(source["triggers"], "scenario triggers"),
            ["firedTriggerIds"] = new JsonArray(),
            ["triggerRuntime"] = new JsonObject(),
            ["eventCounts"] = new JsonObject(),
            ["zoneTags"] = zoneTags,
            ["engagementRules"] = engagementRules,
        };
    }

    private static JsonObject? CreateDeployment(JsonObject level, JsonArray players, JsonArray units, JsonObject zones)
    {
        if (level["deployment"] is null) return null;
        var source = StateJson.Object(level["deployment"], "deployment");
        var declarations = StateJson.Objects(StateJson.Required(source, "zones"), "deployment zones").ToList();
        var orderValues = source["order"] is { } authoredOrder
            ? StateJson.Array(authoredOrder, "deployment order").Select(entry => StateJson.Integer(entry, "deployment player"))
            : declarations.Select(entry => StateJson.Integer(entry, "player"));
        var order = orderValues.Distinct().ToList();
        if (order.Count == 0) throw new DomainInvariantException("deployment must include at least one player");
        var playerIds = players.OfType<JsonObject>().Select(player => StateJson.Integer(player, "id")).ToHashSet();
        foreach (var owner in order)
            if (!playerIds.Contains(owner)) throw new DomainInvariantException($"deployment references unknown player {owner}");
        var assignments = new JsonArray();
        var assigned = new HashSet<int>();
        foreach (var entry in declarations)
        {
            var zone = StateJson.Text(entry, "zone");
            if (zones[zone] is null)
                throw new DomainInvariantException($"deployment references unknown zone \"{zone}\"");
            var owner = StateJson.Integer(entry, "player");
            if (!playerIds.Contains(owner))
                throw new DomainInvariantException($"deployment references unknown player {owner}");
            var keys = entry["unitKeys"] is { } authoredKeys ? StateJson.Array(authoredKeys, "deployment unitKeys") : null;
            var unitIds = new JsonArray();
            foreach (var unit in units.OfType<JsonObject>())
            {
                if (StateJson.Integer(unit, "owner") != owner) continue;
                if (keys is not null)
                {
                    if (unit["key"] is not JsonValue keyValue || !keyValue.TryGetValue<string>(out var key) ||
                        string.IsNullOrEmpty(key) || !keys.Any(candidate => StateJson.Text(candidate, "unit key") == key))
                        continue;
                }
                unitIds.Add(StateJson.Integer(unit, "id"));
            }
            if (keys is not null && unitIds.Count != keys.Count)
                throw new DomainInvariantException($"deployment zone \"{zone}\" references an unknown or wrong-owner unit key");
            assignments.Add(new JsonObject { ["player"] = owner, ["zone"] = zone, ["unitIds"] = unitIds });
        }
        foreach (var assignment in assignments.OfType<JsonObject>())
            foreach (var id in (JsonArray)assignment["unitIds"]!)
            {
                var unitId = StateJson.Integer(id, "deployment unit id");
                if (!assigned.Add(unitId))
                    throw new DomainInvariantException($"unit {unitId} belongs to multiple deployment zones");
            }
        foreach (var owner in order)
            if (!assignments.OfType<JsonObject>().Any(entry => StateJson.Integer(entry, "player") == owner))
                throw new DomainInvariantException($"deployment player {owner} has no deployment zone");
        var orderArray = new JsonArray();
        foreach (var owner in order) orderArray.Add(owner);
        return new JsonObject { ["order"] = orderArray, ["currentIndex"] = 0, ["assignments"] = assignments };
    }
}
