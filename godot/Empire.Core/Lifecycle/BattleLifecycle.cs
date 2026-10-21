using System.Text.Json.Nodes;
using Empire.Core.Resources;
using Empire.Core.State;
using Empire.Core.Statuses;
using Empire.Core.Tactics;
using static Empire.Core.State.StateJson;
using static Empire.Core.Lifecycle.TurnOrderSystem;

namespace Empire.Core.Lifecycle;

public sealed record BattleLifecycleRules(ReadOnlyContent Content, TurnOrderSystem Turns, ScenarioSystem Scenarios,
    VictorySystem Victory, ILifecycleEffects Effects, BattleResources Resources, StatusLifecycle Statuses);

/// <summary>Owns phase, round and actor-turn transitions. The caller's emitter owns event counting.</summary>
public sealed class BattleLifecycle
{
    private readonly JsonObject state;
    private readonly ReadOnlyContent catalog;
    private readonly StateContent content;
    private readonly Action<JsonObject> emit;
    private readonly ILifecycleEffects effects;
    private readonly BattleResources resources;
    private readonly StatusLifecycle statuses;
    public TurnOrderSystem Turns { get; }
    public ScenarioSystem Scenarios { get; }
    public VictorySystem Victory { get; }

    public BattleLifecycle(BattleLifecycleRules rules, JsonObject state, Action<JsonObject> emit)
    {
        this.state = state;
        catalog = rules.Content;
        content = rules.Content.Source;
        this.emit = emit;
        effects = rules.Effects;
        resources = rules.Resources;
        statuses = rules.Statuses;
        Turns = rules.Turns;
        Scenarios = rules.Scenarios;
        Victory = rules.Victory;
    }

    public void Start()
    {
        Claim(Turns.Start(state, emit));
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
        var handoff = Turns.Advance(state, emit);
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
        var battlefield = new TacticalBattlefield(state, content);
        var cell = battlefield.Cell(TacticalQueries.Position(unit));
        if (Integer(Array(battlefield.Map["owners"], "owners")[cell.Index], "owner") != owner) return;
        var rate = cell.Heal;
        if (rate <= 0) return;
        var healed = new UnitHealth(catalog, unit).Heal(rate);
        if (healed > 0) emit(new JsonObject { ["type"] = "regen", ["unit"] = Integer(unit, "id"), ["amount"] = healed });
    }

    private void AdvanceOverlayRound() => Empire.Core.Terrain.BattleOverlays.AdvanceRound(state, emit);

    private IEnumerable<JsonObject> OverlaysAt(JsonObject unit) => Objects(Object(state["scenario"], "scenario")["overlays"], "overlays")
        .Where(overlay => Objects(overlay["cells"], "overlay cells").Any(cell => Empire.Core.Scenario.ScenarioSelection.SameCell(cell, unit)));

    private void ApplyOverlayStatuses(IEnumerable<JsonObject> scope)
    {
        foreach (var unit in scope)
            foreach (var overlay in OverlaysAt(unit))
                if (content.Get("terrainOverlays", Text(overlay, "type"))["turnStartStatus"] is JsonObject effect)
                    AddStatus(unit, Text(effect, "id"), Integer(effect, "duration"));
    }

    private void AddStatus(JsonObject unit, string id, int duration) =>
        StatusTransitions.Add(catalog, unit, id, duration, null, emit);

    private void ResolveStatuses(IReadOnlyList<JsonObject> scope) => statuses.Resolve(state, scope, emit);
}
