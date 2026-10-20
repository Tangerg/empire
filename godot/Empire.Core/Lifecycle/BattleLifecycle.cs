using System.Text.Json.Nodes;
using Empire.Core.Resources;
using Empire.Core.State;
using static Empire.Core.State.StateJson;
using static Empire.Core.Lifecycle.TurnOrderSystem;

namespace Empire.Core.Lifecycle;

/// <summary>Owns phase, round and actor-turn transitions. The caller's emitter owns event counting.</summary>
public sealed class BattleLifecycle
{
    private readonly JsonObject state;
    private readonly StateContent content;
    private readonly Action<JsonObject> emit;
    private readonly ILifecycleEffects effects;
    private readonly BattleResources resources;
    public TurnOrderSystem Turns { get; }
    public ScenarioSystem Scenarios { get; }
    public VictorySystem Victory { get; }

    public BattleLifecycle(JsonObject state, JsonArray contentPacks, Action<JsonObject> emit,
        ILifecycleEffects? effects = null, BattleResources? resources = null, ScenarioSystem? scenarios = null,
        Func<JsonObject, int, JsonObject, string>? extraObjective = null,
        Func<JsonObject, string>? extraObjectiveDescription = null)
    {
        this.state = state;
        content = new StateContent(contentPacks);
        this.emit = emit;
        this.effects = effects ?? new StrictLifecycleEffects();
        this.resources = resources ?? new BattleResources();
        Turns = new TurnOrderSystem(content);
        Scenarios = scenarios ?? new ScenarioSystem(content);
        Victory = new VictorySystem(content, Scenarios, extraObjective, extraObjectiveDescription);
    }

    public void Start()
    {
        state["turnOrder"] = Turns.InitialState(state);
        Claim(Turns.Begin(state));
    }

    public void BeginPlaying()
    {
        state["deployment"] = null;
        state["phase"] = "playing";
        foreach (var unit in Units(state)) unit["done"] = false;
        Start();
        emit(new JsonObject { ["type"] = "battleStarted", ["player"] = Integer(state, "currentPlayer"), ["turn"] = Integer(state, "turn") });
    }

    public void AdvanceTurn()
    {
        emit(new JsonObject { ["type"] = "turnEnd", ["player"] = Integer(state, "currentPlayer") });
        Scenarios.Run(state, "turnEnd", emit);
        var handoff = Turns.Advance(state);
        if (handoff.Exhausted)
        {
            ConcludeIfDecided();
            if (Text(state, "phase") != "over") Conclude(null, "无人可行动");
            return;
        }
        if (handoff.RoundAdvanced)
        {
            state["turn"] = Integer(state, "turn") + 1;
            AdvanceOverlayRound();
            emit(new JsonObject { ["type"] = "roundStart", ["turn"] = Integer(state, "turn") });
        }
        Claim(handoff);
        BeginActorTurn(handoff.RoundAdvanced);
    }

    public void AfterAction()
    {
        if (Text(state, "phase") != "playing") return;
        Scenarios.Run(state, "afterAction", emit);
        ConcludeIfDecided();
    }

    public void ConcludeIfDecided()
    {
        var contenders = Players(state).Where(candidate => IsTrue(candidate["alive"])).Select(candidate => Integer(candidate, "id")).ToArray();
        var result = Victory.Evaluate(state, emit);
        foreach (var id in contenders)
            if (!IsTrue(Player(state, id)["alive"])) emit(new JsonObject { ["type"] = "defeat", ["player"] = id });
        if (result.Team is not null || result.Reason.Length > 0) Conclude(result.Team, result.Reason);
    }

    private void Claim(TurnHandoff handoff)
    {
        state["currentPlayer"] = handoff.Player;
        Object(state["turnOrder"], "turnOrder")["activeUnit"] = handoff.ActiveUnit;
        state["actorTurns"] = Integer(state, "actorTurns") + 1;
    }

    private void Conclude(int? team, string reason)
    {
        state["phase"] = "over";
        state["winnerTeam"] = team;
        state["endReason"] = reason;
        emit(new JsonObject { ["type"] = "gameOver", ["team"] = team, ["reason"] = reason });
    }

    private void BeginActorTurn(bool roundAdvanced)
    {
        effects.ResolveDueCasts(state, emit);
        var owner = Integer(state, "currentPlayer");
        var active = Object(state["turnOrder"], "turnOrder")["activeUnit"];
        var scope = Units(state).Where(unit => active is null ? Integer(unit, "owner") == owner : Integer(unit, "id") == Integer(active, "activeUnit")).ToArray();
        var started = new JsonObject { ["type"] = "turnStart", ["player"] = owner, ["turn"] = Integer(state, "turn") };
        if (active is not null) started["activeUnit"] = active.DeepClone();
        emit(started);
        ApplyOverlayStatuses(scope);
        ResolveStatuses(scope);
        if (active is null) OpenRoundFor(owner);
        else if (roundAdvanced)
            foreach (var player in Players(state).Where(player => IsTrue(player["alive"]))) OpenRoundFor(Integer(player, "id"));
        foreach (var unit in scope) RefreshActor(unit, owner);
        Scenarios.Run(state, "turnStart", emit);
    }

    private void OpenRoundFor(int owner)
    {
        var subject = new BattleResources.Subject("player", Player(state, owner));
        var commanders = Objects(state["commanders"], "commanders").Where(commander => Integer(commander, "owner") == owner &&
            Units(state).Any(unit => Integer(unit, "id") == Integer(commander, "unitId"))).ToArray();
        var commanderGrants = new JsonArray();
        foreach (var commander in commanders)
        {
            commander["usedTactics"] = new JsonArray();
            foreach (var grant in Array(commander["turnGrants"], "commander turn grants")) commanderGrants.Add(grant?.DeepClone());
        }
        Grant(subject, commanderGrants);
        var rules = Object(state["rules"], "rules");
        var income = (JsonArray)Array(rules["baseResourceGrants"], "base resource grants").DeepClone();
        var map = Object(state["map"], "map");
        var owners = Array(map["owners"], "owners");
        var tiles = Array(map["tiles"], "tiles");
        var overrides = Object(rules["siteResourceOverrides"], "siteResourceOverrides");
        for (var index = 0; index < owners.Count; index++)
        {
            if (Integer(owners[index], "owner") != owner) continue;
            var terrain = content.Get("terrains", Text(tiles[index], "terrain"));
            foreach (var grant in Objects(terrain["ownerTurnGrants"], "owner turn grants"))
            {
                var resource = Text(grant, "resource");
                income.Add(new JsonObject { ["resource"] = resource, ["amount"] = (overrides[resource] ?? grant["amount"])!.DeepClone() });
            }
        }
        Grant(subject, income);
    }

    private void Grant(BattleResources.Subject subject, JsonArray grants)
    {
        foreach (var grant in resources.Totals(grants))
        {
            if (!resources.HasAccount(subject, grant.Resource)) continue;
            var amount = resources.Credit(subject, grant.Resource, grant.Value);
            resources.Announce(subject, grant.Resource, amount, emit);
        }
    }

    private void RefreshActor(JsonObject unit, int owner)
    {
        foreach (var pair in Object(unit["weaponState"], "weapon state"))
        {
            var runtime = Object(pair.Value, "weapon runtime");
            runtime["cooldownRemaining"] = Math.Max(0, Number(runtime["cooldownRemaining"], "cooldown") - 1);
        }
        if (!Objects(state["pendingCasts"], "pendingCasts").Any(cast => Integer(cast, "caster") == Integer(unit, "id"))) unit["done"] = false;
        var rules = Object(state["rules"], "rules");
        if (!IsTrue(rules["healOnOwnedBuilding"])) return;
        var map = Object(state["map"], "map");
        var index = Integer(unit, "y") * Integer(map, "width") + Integer(unit, "x");
        if (Integer(Array(map["owners"], "owners")[index], "owner") != owner) return;
        var terrain = content.Get("terrains", Text(Array(map["tiles"], "tiles")[index], "terrain"));
        var rate = Math.Max(0, Number(terrain["heal"], "heal") + OverlaysAt(unit).Sum(overlay => Number(content.Get("terrainOverlays", Text(overlay, "type"))["healDelta"], "heal delta")));
        if (rate <= 0) return;
        var hp = Number(unit["hp"], "hp");
        var maximum = Number(content.Get("units", Text(unit, "type"))["maxHp"], "maxHp");
        var healed = Math.Min(Math.Max(0, Round(rate)), Math.Max(0, maximum - hp));
        unit["hp"] = hp + healed;
        if (healed > 0) emit(new JsonObject { ["type"] = "regen", ["unit"] = Integer(unit, "id"), ["amount"] = healed });
    }

    private void AdvanceOverlayRound()
    {
        var overlays = Array(Object(state["scenario"], "scenario")["overlays"], "overlays");
        foreach (var overlay in Objects(overlays, "overlays").ToArray())
        {
            if (overlay["remainingRounds"] is null) continue;
            var remaining = Number(overlay["remainingRounds"], "remainingRounds") - 1;
            overlay["remainingRounds"] = remaining;
            if (remaining > 0) continue;
            overlays.Remove(overlay);
            emit(new JsonObject { ["type"] = "overlayRemoved", ["overlay"] = Text(overlay, "id") });
        }
    }

    private IEnumerable<JsonObject> OverlaysAt(JsonObject unit) => Objects(Object(state["scenario"], "scenario")["overlays"], "overlays")
        .Where(overlay => Objects(overlay["cells"], "overlay cells").Any(cell => ScenarioSystem.SameCell(cell, unit)));

    private void ApplyOverlayStatuses(IEnumerable<JsonObject> scope)
    {
        foreach (var unit in scope)
            foreach (var overlay in OverlaysAt(unit))
                if (content.Get("terrainOverlays", Text(overlay, "type"))["turnStartStatus"] is JsonObject effect)
                    AddStatus(unit, Text(effect, "id"), Integer(effect, "duration"));
    }

    private void AddStatus(JsonObject unit, string id, int duration)
    {
        if (duration < 1) throw new DomainInvariantException("status duration must be >= 1");
        var definition = content.Get("statuses", id);
        var statuses = Array(unit["statuses"], "statuses");
        var existing = Objects(statuses, "statuses").FirstOrDefault(status => Text(status, "id") == id);
        var carried = existing ?? new JsonObject { ["id"] = id, ["remaining"] = duration, ["stacks"] = 1 };
        if (existing is null) statuses.Add(carried);
        else switch (Text(definition, "stackMode"))
        {
            case "refresh": carried["remaining"] = Math.Max(Number(carried["remaining"], "remaining"), duration); break;
            case "extend": carried["remaining"] = Number(carried["remaining"], "remaining") + duration; break;
            case "stack":
                carried["stacks"] = Math.Min(Number(definition["maxStacks"], "maxStacks"), Number(carried["stacks"], "stacks") + 1);
                carried["remaining"] = Math.Max(Number(carried["remaining"], "remaining"), duration); break;
            default: throw new DomainInvariantException($"unknown status stack mode \"{Text(definition, "stackMode")}\"");
        }
        emit(new JsonObject { ["type"] = "statusApplied", ["unit"] = Integer(unit, "id"), ["status"] = id,
            ["remaining"] = carried["remaining"]!.DeepClone(), ["stacks"] = carried["stacks"]!.DeepClone() });
    }

    private void ResolveStatuses(IEnumerable<JsonObject> scope)
    {
        foreach (var unit in scope)
        {
            var statuses = Array(unit["statuses"], "statuses");
            foreach (var instance in Objects(statuses, "statuses").ToArray())
            {
                var definition = content.Get("statuses", Text(instance, "id"));
                if (definition["periodic"] is JsonObject periodic && Text(periodic, "timing") == "ownerTurnStart")
                {
                    var maximum = Number(content.Get("units", Text(unit, "type"))["maxHp"], "maxHp");
                    var requested = Math.Max(1, Round(maximum * Number(periodic["maxHpFraction"], "maxHpFraction") * Number(instance["stacks"], "stacks")));
                    effects.DamageStatus(state, unit, instance, requested, IsTrue(periodic["nonlethal"]), emit);
                    if (!Units(state).Any(candidate => Integer(candidate, "id") == Integer(unit, "id"))) break;
                }
                // The shipped TypeScript status behavior registry has no imperative hooks.
                var remaining = Number(instance["remaining"], "remaining") - 1;
                instance["remaining"] = remaining;
                if (remaining <= 0 && Units(state).Any(candidate => Integer(candidate, "id") == Integer(unit, "id")))
                {
                    statuses.Remove(instance);
                    emit(new JsonObject { ["type"] = "statusRemoved", ["unit"] = Integer(unit, "id"), ["status"] = Text(instance, "id") });
                }
            }
        }
    }
}
