using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Combat;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
using Empire.Core.Resources;
using Empire.Core.Tactics;
using Empire.Presentation;
using Empire.Presentation.Feedback;
using Empire.Presentation.Tactical;

namespace Empire.Application;

public sealed partial class GrayFlagSession
{
    public TacticalHudProjection ProjectHud(TacticalStamp stamp, TacticalSelection selection, BoardCell? cursor, int? inspectId, bool busy)
    {
        if (stamp.Revision != revision)
            throw new DomainInvariantException("HUD projection belongs to a stale session revision.");
        var state = battle.Snapshot();
        var sections = new List<TacticalHudSection>();
        var current = TacticalQueries.Player(state, N(state, "currentPlayer"));
        var human = S(current, "controller") == "human";
        var visible = VisibleUnitIds(state, Viewer(state));
        var selected = selection.UnitId is { } selectedId && visible.Contains(selectedId) ? TacticalQueries.UnitById(state, selectedId) : null;
        var commandable = human && selected is not null && engine.Turns.MayAct(state, selected) ? selected : null;
        var formations = commandable is null ? [] : engine.Progression.Formations.Options(state, commandable);
        var inspect = inspectId is { } iid && visible.Contains(iid) ? TacticalQueries.UnitById(state, iid) : null;
        void Section(string key, string title, IEnumerable<TacticalHudRow> rows)
        {
            var values = rows.ToArray();
            if (values.Length > 0)
                sections.Add(new(key, title, System.Array.AsReadOnly(values)));
        }
        TacticalHudRow Pick(string key,
            string label,
            TacticalHudIntentKind kind,
            string? argument = null,
            int? unit = null,
            string hint = "",
            bool enabled = true,
            bool active = false) =>
            new(key, label, hint, enabled, active, new(stamp, kind, argument, unit));
        TacticalHudRow Action(string key, string label, JsonObject action, string hint = "", bool enabled = true, bool active = false) =>
            new(key, label, hint, enabled, active, new(stamp, TacticalHudIntentKind.Action, Action: EcmaJson.ToElement(action)));
        TacticalHudPortrait UnitIcon(JsonObject unit)
        {
            var team = state["players"]!.AsArray().OfType<JsonObject>()
                .FirstOrDefault(player => N(player, "id") == N(unit, "owner"));
            return new(
                S(unit, "type"),
                unit["key"] is null ? null : S(unit, "key"),
                team is not null ? S(team, "color") : "#999999");
        }
        string Name(JsonObject unit) => contentText.UnitName(S(unit, "type"));
        string Accounts(string kind, JsonObject holder, string? weapon = null) =>
            string.Join(" · ", engine.Resources.Accounts(new(kind, holder, weapon)).Select(account =>
            {
                var quantity = account.Current is null
                    ? "∞"
                    : account.Capacity is null
                        ? F(account.Current.Value)
                        : $"{F(account.Current.Value)}/{F(account.Capacity.Value)}";
                return $"{contentText.ResourceName(account.Resource)} {quantity}";
            }));
        string Costs(IEnumerable<BattleResources.Cost> costs)
        {
            var amounts = string.Join(" · ", costs.Select(cost => $"{contentText.ResourceName(cost.Resource)} {F(cost.Amount)}"));
            return amounts.Length == 0 ? text.Format("ui.hud.none") : amounts;
        }
        string Amounts(JsonNode? amounts) => Costs((amounts as JsonArray ?? new()).OfType<JsonObject>()
            .Select(cost => new BattleResources.Cost(S(cost, "resource"), D(cost, "amount"))));
        Section("crown",
            battleText.LevelTitle(S(state, "levelId")),
            new[] { new TacticalHudRow("turn",
                text.Format("ui.hud.turn",
                    state["rules"]?["turnLimit"] is { } limit && Number(limit) != 0 ? text.Format("ui.hud.turn_limit",
                        D(state, "turn"),
                        Number(limit)) : F(D(state, "turn")),
                    battleText.PlayerName(S(state, "levelId"), N(current, "id")),
                    text.Format(human ? "ui.hud.human_turn" : "ui.hud.computer_turn"))),
                new("accounts", Accounts("player", current)) });
        if (state["turnOrder"]?["activeUnit"] is not null)
            Section("initiative",
                text.Format("ui.hud.initiative"),
                engine.Turns.Preview(state, 6).Select((id, index) => new TacticalHudRow($"actor:{index}",
                        Name(TacticalQueries.UnitById(state, id)!),
                        Selected: index == 0 && id == N(state["turnOrder"]!.AsObject(), "activeUnit"),
                        UnitIcon: UnitIcon(TacticalQueries.UnitById(state, id)!))));
        Section("casts",
            text.Format("ui.hud.casts"),
            engine.Combat.ActiveCasts(state).Select((cast, index) => new TacticalHudRow($"cast:{index}",
                text.Format("ui.hud.cast", contentText.Name(ContentTextKind.Weapon, cast.Weapon), cast.Target.X, cast.Target.Y, cast.Remaining),
                text.Format("ui.hud.progress", Pct(cast.Progress)),
                Gauge: new(cast.Progress, 1))));
        var objectivePlayer = state["players"]!.AsArray().OfType<JsonObject>()
            .FirstOrDefault(player => S(player, "controller") == "human") ?? state["players"]![0]!.AsObject();
        Section("objectives",
            text.Format("battle.objective.heading"),
            engine.Victory.Objectives(state, N(objectivePlayer, "id"))
                .Select(objective => new TacticalHudRow(objective.Id,
                    text.Format("battle.objective.row",
                        battleText.ObjectiveDescription(S(state, "levelId"), Viewer(state), objective),
                        battleText.ObjectiveProgress(objective)))));
        Section("roster",
            text.Format("ui.hud.roster"),
            state["players"]!.AsArray().OfType<JsonObject>().Select(player => new TacticalHudRow($"player:{player["id"]}",
                text.Format("ui.hud.player_forces",
                    battleText.PlayerName(S(state, "levelId"), N(player, "id")),
                    state["units"]!.AsArray().OfType<JsonObject>().Count(unit => N(unit, "owner") == N(player, "id")),
                    Accounts("player", player)))));
        if (S(state, "phase") == "deployment" && human)
        {
            Section("deployment",
                text.Format("ui.hud.deployment"),
                engine.Deployment.Roster(state)!.Units.Select(unit => Pick($"deploy:{unit["id"]}",
                    $"{Name(unit)} {unit["x"]},{unit["y"]}",
                    TacticalHudIntentKind.DeployPick,
                    unit: N(unit, "id"),
                    enabled: !busy,
                    active: selection.UnitId == N(unit, "id"))));
            if (selected is not null && cursor is { } deploymentCursor)
            {
                var swap = engine.Deployment.Spots(state, selected).FirstOrDefault(spot => spot.At == new Coord(deploymentCursor.X, deploymentCursor.Y))?.Swaps;
                if (swap is not null)
                    Section("swap",
                        text.Format("ui.hud.deployment_swap"),
                        new[] { new TacticalHudRow("swap", text.Format("ui.hud.swap_hint", Name(swap))) });
            }
        }
        else if (selection.Kind is TacticalSelectionKind.Target or TacticalSelectionKind.Tactic or TacticalSelectionKind.Disembark)
            Section("target",
                text.Format("ui.hud.choose_target"),
                new[] { Pick("cancel", text.Format("ui.hud.cancel_shortcut"), TacticalHudIntentKind.Back) });
        else if (!busy && selection.Kind == TacticalSelectionKind.Destination && selected is not null && selection.Destination is { } destination)
            Section("commands",
                text.Format("ui.hud.commands"),
                engine.CommandsAt(state,
                    selected,
                    new(destination.X,
                        destination.Y)).Select(command => Pick(command.Key,
                        contentText.CommandName(command),
                        TacticalHudIntentKind.Command,
                        command.Key,
                        hint: contentText.CommandHint(command)) with
                        {
                            Icon = command.Weapon is { } id ? "weapon:" + id : "ability:" + command.Ability
                        }).Append(Pick("cancel",
                        text.Format("ui.hud.cancel_shortcut"),
                        TacticalHudIntentKind.Back)));
        if (!busy && human && selection.Kind == TacticalSelectionKind.Idle && S(state, "phase") == "playing")
            Section("tactics",
                text.Format("ui.hud.tactics"),
                state["commanders"]!.AsArray().OfType<JsonObject>().SelectMany(commander => engine.Support.Tactics(state,
                    S(commander, "id")).Select(tactic => Pick($"{S(commander, "id")}:{tactic.Id}",
                        $"{contentText.Name(ContentTextKind.Tactic, tactic.Id)} · {Costs(tactic.Costs)}",
                        TacticalHudIntentKind.Tactic,
                        $"{S(commander, "id")}:{tactic.Id}"))));
        if (selection.Kind == TacticalSelectionKind.Target && selected is not null
            && selection.Weapon is { } weapon && cursor is { } target
            && selection.Targets.Contains(target) && selection.Destination is { } from)
        {
            var defender = TacticalQueries.UnitAt(state, new(target.X, target.Y));
            var plan = engine.Combat.Forecast(state, selected, new(target.X, target.Y), new(from.X, from.Y), weapon);
            if (plan.PrimaryStructure is { } structureForecast)
            {
                var structure = state["structures"]!.AsArray().OfType<JsonObject>()
                    .Single(value => S(value, "id") == structureForecast.Structure);
                var definition = Definition("structures", S(structure, "type"));
                var rows = new List<TacticalHudRow>
                {
                    new("exchange", $"{Name(selected)} → {contentText.Name(ContentTextKind.Structure, S(structure, "type"))}"),
                    new("strike", text.Format("ui.hud.strike", structureForecast.Damage, D(structure, "hp"), structureForecast.HpAfter),
                        Gauge: new(structureForecast.HpAfter, D(definition, "maxHp")))
                };
                if (structureForecast.Destroyed)
                    rows.Add(new("structure-destroyed", text.Format("ui.hud.structure_destroyed")));
                Section("forecast", text.Format("ui.hud.forecast"), rows);
            }
            if (defender is not null)
            {
                if (plan.PrimaryUnit is { } exchange)
                {
                    var recipient = TacticalQueries.UnitById(state, exchange.DamageRecipient) ?? defender;
                    var rows = new List<TacticalHudRow> { new("exchange", $"{Name(selected)} → {Name(defender)}") };
                    if (exchange.Interceptor is not null)
                        rows.Add(new("interceptor", text.Format("ui.hud.interception", Name(recipient))));
                    else if (exchange.Reaction is { } reaction)
                        rows.Add(new("reaction", text.Format("ui.hud.target_reaction", contentText.ReactionName(reaction.Stance))));
                    if (plan.UnitHits.Count + plan.StructureHits.Count > 1)
                        rows.Add(new("collateral", text.Format("ui.hud.collateral", plan.UnitHits.Count - 1, plan.StructureHits.Count)));
                    if (plan.SupportAttack is { } support)
                        rows.Add(new("support", text.Format("ui.hud.support_damage", support.Damage.Damage)));
                    rows.Add(new("strike",
                        text.Format("ui.hud.strike", exchange.Strike.Damage, D(recipient, "hp"), exchange.RecipientHpAfter),
                        Gauge: new(exchange.RecipientHpAfter, D(Definition("units", S(recipient, "type")), "maxHp"))));
                    rows.Add(new("counter",
                        text.Format("ui.hud.counter",
                            exchange.Counter is null ? text.Format("ui.hud.none") : F(exchange.Counter.Damage),
                            D(selected, "hp"),
                            exchange.AttackerHpAfter),
                        Gauge: new(exchange.AttackerHpAfter, D(Definition("units", S(selected, "type")), "maxHp"))));
                    rows.Add(new("damage-type",
                        text.Format("ui.hud.damage_chain",
                            contentText.Name(ContentTextKind.DamageType, exchange.Strike.DamageType),
                            contentText.Name(ContentTextKind.ArmorClass, S(Definition("units", S(recipient, "type")), "armorClass")))));
                    rows.AddRange(exchange.Strike.Modifiers.Select((modifier, index) => new TacticalHudRow($"strike-mod:{index}",
                        ModifierLabel(modifier))));
                    rows.Add(new("mitigation", text.Format("ui.hud.mitigation", Pct(exchange.Strike.Mitigation))));
                    if (exchange.RecipientDies)
                        rows.Add(new("recipient-dies", text.Format("ui.hud.recipient_dies")));
                    if (exchange.AttackerDies)
                        rows.Add(new("attacker-dies", text.Format("ui.hud.attacker_dies")));
                    if (exchange.Counter is { } counter)
                        rows.AddRange(counter.Modifiers.Select((modifier, index) => new TacticalHudRow($"counter-mod:{index}",
                            text.Format("ui.hud.counter_modifier", ModifierLabel(modifier)))));
                    Section("forecast", text.Format("ui.hud.forecast"), rows);
                }
            }
        }
        if (inspect is not null)
        {
            var definition = Definition("units", S(inspect, "type"));
            var weapons = definition["weapons"]!.AsArray().Select(id => Definition("weapons", EcmaJson.ReadString(id))).ToArray();
            var owner = state["players"]!.AsArray().OfType<JsonObject>().FirstOrDefault(player => N(player, "id") == N(inspect, "owner"));
            var rows = new List<TacticalHudRow>
            {
                new("owner",owner is null ? text.Format("ui.hud.neutral") : battleText.PlayerName(S(state,"levelId"), N(owner,"id"))),
                new("hp",text.Format("ui.hud.health", D(inspect,"hp"), D(definition,"maxHp")), Gauge:new(D(inspect,"hp"),D(definition,"maxHp"))),
                new("blurb",contentText.UnitBlurb(S(definition,"id"))),
                new("power",text.Format("ui.hud.power", Power(weapons))),
                new("defense",text.Format("ui.hud.defense", Pct(D(definition,"defense")))),
                new("movement",text.Format("ui.hud.movement", D(definition,"movement"))),
                new("range",text.Format("ui.hud.range", Range(weapons))),
                new("damage",
                    text.Format("ui.hud.damage",
                        string.Join(" / ",weapons.Select(value => contentText.Name(ContentTextKind.DamageType,S(value,"damageType"))).Distinct()))),
                new("armor",text.Format("ui.hud.armor", contentText.Name(ContentTextKind.ArmorClass,S(definition,"armorClass")))),
                new("movement-type",text.Format("ui.hud.movement_type", contentText.Name(ContentTextKind.MovementProfile,S(definition,"movementClass")))),
                new("value",text.Format("ui.hud.value", D(definition,"value"))) };
            if (!weapons.Any(value => B(value, "moveAndAttack")))
                rows.Add(new("stationary", text.Format("ui.hud.stationary_attack")));
            rows.Add(new("capture",
                definition["abilities"]!.AsArray().Any(id => EcmaJson.ReadString(id) == "capture") ? text.Format("ui.hud.can_capture") : text.Format("ui.hud.cannot_capture")));
            if (definition["abilities"]!.AsArray().Any(id => EcmaJson.ReadString(id) == "heal"))
                rows.Add(new("heal", text.Format("ui.hud.can_heal")));
            rows.Add(new("reaction", text.Format("ui.hud.reaction", contentText.ReactionName(S(inspect, "reaction")))));
            if (B(inspect, "done"))
                rows.Add(new("done", text.Format("ui.hud.acted")));
            foreach (var arm in weapons)
            {
                var id = S(arm, "id");
                var stock = Accounts("weapon", inspect, id);
                var cooldown = D(inspect["weaponState"]![id]!.AsObject(), "cooldownRemaining");
                rows.Add(new($"weapon:{id}",
                    text.Format("ui.hud.weapon",
                        contentText.Name(ContentTextKind.Weapon, id),
                        contentText.Name(ContentTextKind.DamageType, S(arm, "damageType")),
                        D(arm, "power"),
                        Range(new[] { arm }),
                        stock.Length > 0 ? stock : text.Format("ui.hud.unlimited"),
                        cooldown > 0 ? text.Format("ui.hud.weapon_cooldown", cooldown) : "",
                        arm["resourceRequirements"]!.AsArray().Count > 0 ? text.Format("ui.hud.weapon_requirements",
                            Amounts(arm["resourceRequirements"])) : "",
                        arm["resourceCosts"]!.AsArray().Count > 0 ? text.Format("ui.hud.weapon_costs", Amounts(arm["resourceCosts"])) : ""),
                    Icon: "weapon:" + id));
            }
            var statuses = inspect["statuses"]!.AsArray().OfType<JsonObject>().ToArray();
            if (statuses.Length == 0)
                rows.Add(new("status:none", text.Format("ui.hud.no_status")));
            rows.AddRange(statuses.Select((status, index) => new TacticalHudRow($"status:{index}",
                text.Format("ui.hud.status",
                    contentText.Name(ContentTextKind.Status, S(status, "id")),
                    D(status, "remaining"),
                    D(status, "stacks") > 1 ? text.Format("ui.hud.status_stacks", D(status, "stacks")) : ""),
                Icon: "status:" + S(status, "id"))));
            var threshold = engine.Combat.Effects.NextRankThreshold(N(inspect, "rank"));
            rows.Add(new("rank",
                text.Format("ui.hud.rank",
                    text.Format(new[] { "ui.rank.recruit", "ui.rank.veteran", "ui.rank.elite" }[N(inspect, "rank")]),
                    threshold is { } next ? text.Format("ui.hud.rank_progress", D(inspect, "rankProgress"), next) : "")));
            rows.Add(new("facing", text.Format("ui.hud.facing", Facing(state, S(inspect, "facing")))));
            if (inspect["career"]?["current"] is { } careerId)
            {
                var id = EcmaJson.ReadString(careerId);
                var career = Definition("careers", id);
                rows.Add(new("career",
                    text.Format("ui.hud.career",
                        contentText.CareerName(S(career, "id")),
                        Number(inspect["career"]?["mastery"]?[id]),
                        D(career, "masteryThreshold"))));
            }
            if (inspect["formation"] is { } formationId)
            {
                // Preserve source: holding is read from commandable, not inspected, options.
                var holds = formations.Any(option => option.Current && option.Eligible);
                rows.Add(new("formation",
                    text.Format("ui.hud.formation",
                        contentText.Name(ContentTextKind.Formation, EcmaJson.ReadString(formationId)),
                        holds ? "" : text.Format("ui.hud.formation_inactive"))));
            }
            var account = Accounts("unit", inspect);
            if (account.Length > 0)
                rows.Add(new("unit-resources", text.Format("ui.hud.unit_resources", account)));
            var commander = state["commanders"]!.AsArray().OfType<JsonObject>().FirstOrDefault(value => S(value, "id") == S(inspect, "commanderId"));
            if (commander is not null)
            {
                var commanderState = TacticalModifiers.CommanderUnit(state, commander) is null
                    ? "ui.hud.commander_absent"
                    : TacticalModifiers.ActiveCommanderFor(RuleGrid(state), state, inspect) is null
                        ? "ui.hud.commander_out_of_range"
                        : "ui.hud.commander_active";
                rows.Add(new("commander", text.Format("ui.hud.commander", S(commander, "id"), text.Format(commanderState))));
            }
            Section("unit", Name(inspect), rows);
            if (commandable is not null && N(commandable, "id") == N(inspect, "id"))
            {
                var id = N(commandable, "id");
                Section("stances",
                    text.Format("ui.hud.stances"),
                    engine.Combat.Forecasts.Reactions.All().Select(stance => Action($"reaction:{stance.Id}",
                        contentText.ReactionName(stance.Id),
                        new()
                        {
                            ["kind"] = "reaction",
                            ["unit"] = id,
                            ["stance"] = stance.Id
                        },
                        hint: contentText.ReactionHint(stance.Id),
                        active: S(commandable, "reaction") == stance.Id))
                    .Concat(RuleGrid(state).Directions.Select(direction => Action($"face:{direction.Id}",
                        Facing(state, direction.Id),
                        new()
                        {
                            ["kind"] = "face",
                            ["unit"] = id,
                            ["facing"] = direction.Id
                        },
                        active: S(commandable, "facing") == direction.Id))));
                Section("formations",
                    text.Format("ui.hud.formations"),
                    formations.Select(option => Action($"formation:{S(option.Formation, "id")}",
                        text.Format("ui.hud.formation_option",
                            contentText.Name(ContentTextKind.Formation, S(option.Formation, "id")),
                            option.Current ? text.Format("ui.hud.formation_clear") : ""),
                        new()
                        {
                            ["kind"] = "changeFormation",
                            ["unit"] = id,
                            ["formation"] = option.Current ? null : option.Formation["id"]!.DeepClone()
                        },
                    option.Reasons.Count > 0 ? refusals.DescribeMany(option.Reasons) : text.Format("ui.hud.formation_effect",
                        D(option.Formation, "attackMultiplier"),
                        Signed(D(option.Formation, "defenseDelta")),
                        Signed(D(option.Formation, "movementDelta"))),
                        option.Eligible || option.Current,
                        option.Current)));
                Section("carriers",
                    text.Format("ui.hud.embark"),
                    engine.Transport.CarrierOptions(state, commandable)
                        .Select(option => Action($"carrier:{option.Carrier["id"]}",
                            Name(option.Carrier),
                            new()
                            {
                                ["kind"] = "embark",
                                ["unit"] = id,
                                ["carrier"] = option.Carrier["id"]!.DeepClone()
                            },
                            option.Reasons.Count > 0 ? refusals.DescribeMany(option.Reasons) : text.Format("ui.hud.embark_hint"),
                            option.Eligible)));
                Section("passengers",
                    text.Format("ui.hud.disembark"),
                    engine.Transport.PassengerOptions(state, commandable)
                        .Select(option => Pick($"passenger:{option.Unit["id"]}",
                            Name(option.Unit),
                            TacticalHudIntentKind.Disembark,
                            unit: N(option.Unit, "id"),
                            hint: option.Spots.Count > 0 ? text.Format("ui.hud.disembark_spots",
                                option.Spots.Count) : text.Format("ui.hud.no_disembark_spot"),
                            enabled: option.Spots.Count > 0)));
                Section("careers",
                    text.Format("ui.hud.careers"),
                    engine.Progression.Careers.Options(state, commandable)
                        .Select(option => Action($"career:{S(option.Career, "id")}",
                            text.Format("ui.hud.career_option",
                                contentText.CareerName(S(option.Career, "id")),
                                D(option.Career, "tier"),
                                option.Mastered ? text.Format("ui.hud.career_mastered") : option.Unlocked ? text.Format("ui.hud.career_unlocked") : ""),
                            new()
                            {
                                ["kind"] = "changeCareer",
                                ["unit"] = id,
                                ["career"] = option.Career["id"]!.DeepClone()
                            },
                            option.Reasons.Count > 0
                                ? refusals.DescribeMany(option.Reasons)
                                : option.Mastered
                                    ? text.Format("ui.hud.career_mastered_hint")
                                    : option.Unlocked
                                        ? text.Format("ui.hud.career_unlocked_hint")
                                        : text.Format("ui.hud.career_eligible_hint"),
                            option.Eligible)));
            }
        }
        if (selection.Kind == TacticalSelectionKind.Recruit && selection.Destination is { } recruitAt)
            Section("recruit",
                text.Format("ui.hud.recruit"),
                new[] { new TacticalHudRow("accounts", Accounts("player", current)),
                    Pick("cancel",
                        text.Format("ui.common.close"),
                        TacticalHudIntentKind.Back) }.Concat(engine.Recruitment.Cards(state,
                        new(recruitAt.X, recruitAt.Y)).Select(card =>
            {
                var def = Definition("units", card.Unit);
                var weapons = def["weapons"]!.AsArray().Select(id => Definition("weapons", EcmaJson.ReadString(id))).ToArray();
                return Pick(card.Unit,
                    $"{contentText.UnitName(card.Unit)} · {Costs(card.Costs)}",
                    TacticalHudIntentKind.Recruit,
                    card.Unit,
                    hint: text.Format("ui.hud.recruit_hint",
                        Power(weapons),
                        D(def, "maxHp"),
                        D(def, "movement"),
                        Range(weapons),
                        contentText.UnitBlurb(card.Unit)),
                    enabled: card.Affordable) with
                {
                    UnitIcon = new(card.Unit, null, S(current, "color"))
                };
            })));
        var inspectionCell = selection.Kind is TacticalSelectionKind.Site or TacticalSelectionKind.Recruit
            ? selection.Destination : cursor;
        if (inspectionCell is { } tile && engine.Space.Board(state).Contains(new(tile.X, tile.Y)))
        {
            var structure = state["structures"]!.AsArray().OfType<JsonObject>().FirstOrDefault(value => TacticalQueries.Position(value) == new Coord(tile.X, tile.Y));
            if (structure is not null)
            {
                var def = Definition("structures", S(structure, "type"));
                var facts = new List<TacticalHudRow> { new("hp", $"{structure["hp"]} / {def["maxHp"]}", Gauge: new(D(structure, "hp"), D(def, "maxHp"))) };
                var holder = state["players"]!.AsArray().OfType<JsonObject>().FirstOrDefault(player => N(player, "id") == N(structure, "owner"));
                if (holder is not null)
                    facts.Add(new("owner", battleText.PlayerName(S(state, "levelId"), N(holder, "id"))));
                if (B(structure, "disabled"))
                    facts.Add(new("disabled", text.Format("ui.hud.disabled")));
                foreach (var (key,
                    label) in new[] { ("blocksMovement", text.Format("ui.hud.blocks_movement")),
                    ("blocksVision", text.Format("ui.hud.blocks_vision")),
                    ("repairable", text.Format("ui.hud.repairable")) })
                    if (B(def, key))
                        facts.Add(new(key, label));
                if (S(def, "cover") is "full" or "half")
                    facts.Add(new("cover", Cover(S(def, "cover"))));
                if (!B(def, "targetable"))
                    facts.Add(new("targetable", text.Format("ui.hud.untargetable")));
                Section("structure", contentText.Name(ContentTextKind.Structure, S(def, "id")), facts);
            }
            var index = engine.Space.Board(state).IndexOf(new(tile.X, tile.Y));
            var terrain = Definition("terrains", EcmaJson.ReadString(state["map"]!["tiles"]![index]));
            var rows = new List<TacticalHudRow> { new("coordinate", $"{tile.X}, {tile.Y}"),
                new("defense", text.Format("ui.hud.terrain_defense", Pct(D(terrain, "defense")))),
                new("elevation", text.Format("ui.hud.elevation", Number(state["map"]!["elevation"]![index]))),
                new("cover", Cover(S(terrain, "cover"))) };
            if (B(terrain, "capturable"))
            {
                var siteOwner = state["map"]!["owners"]![index]!.GetValue<int>();
                var holder = state["players"]!.AsArray().OfType<JsonObject>().FirstOrDefault(player => N(player, "id") == siteOwner);
                var facts = new List<TacticalHudRow>
                {
                    new("owner", text.Format("ui.hud.site.owner", holder is not null
                        ? battleText.PlayerName(S(state, "levelId"), N(holder, "id"))
                        : text.Format("ui.hud.neutral")))
                };
                var heal = new TacticalBattlefield(state, engine.Catalog).Cell(new(tile.X, tile.Y)).Heal;
                if (heal > 0)
                {
                    facts.Add(new("heal", text.Format("ui.hud.terrain_heal", heal)));
                    facts.Add(new("heal-use", B(state["rules"]!.AsObject(), "healOnOwnedBuilding")
                        ? text.Format("ui.hud.site.heal_use", text.Format(state["turnOrder"]!["activeUnit"] is null
                            ? "ui.hud.site.heal_timing_side" : "ui.hud.site.heal_timing_unit"))
                        : text.Format("ui.hud.site.heal_disabled")));
                }
                if (siteOwner != Viewer(state))
                    facts.Add(new("capture-use", text.Format("ui.hud.site.capture_use")));
                var progress = Number(state["map"]!["captureProgress"]![index]);
                if (progress > 0)
                    facts.Add(new("capture-progress", text.Format("ui.hud.site.capture_progress", progress,
                        Number(state["rules"]!["captureThreshold"])), Gauge: new(progress, Number(state["rules"]!["captureThreshold"]))));
                facts.Add(new("recruit-use", text.Format(terrain["produces"]!.AsArray().Count == 0
                    ? "ui.hud.site.no_recruitment"
                    : GetTacticalQueries(stamp.Binding).CanOpenRecruitment(tile)
                        ? "ui.hud.site.recruit_use" : "ui.hud.site.recruit_unavailable")));
                if (selection.Kind == TacticalSelectionKind.Site)
                    facts.Add(Pick("close", text.Format("ui.hud.cancel_shortcut"), TacticalHudIntentKind.Back));
                Section("site", contentText.Name(ContentTextKind.Terrain, S(terrain, "id")), facts);
            }
            else if (D(terrain, "heal") != 0)
                rows.Add(new("heal", text.Format("ui.hud.terrain_heal", D(terrain, "heal"))));
            if (terrain["ownerTurnGrants"]!.AsArray().Count > 0)
                rows.Add(new("grants", text.Format("ui.hud.production", Amounts(terrain["ownerTurnGrants"]))));
            var movementCosts = engine.Catalog.Definitions("movementProfiles")
                .Select(value => EcmaJson.Parse(value.GetRawText())!.AsObject())
                .Select(profile =>
                {
                    var profileId = S(profile, "id");
                    var profileName = contentText.Name(ContentTextKind.MovementProfile, profileId);
                    var cost = terrain["cost"]?[profileId]?.ToString() ?? "—";
                    return $"{profileName} {cost}";
                });
            rows.Add(new("movement-costs", string.Join(" · ", movementCosts)));
            Section("tile", B(terrain, "capturable") ? text.Format("ui.hud.site.terrain_details")
                : contentText.Name(ContentTextKind.Terrain, S(terrain, "id")), rows);
        }
        // Site use stays above long unit statistics, including when a selected unit stands on it.
        var siteSection = sections.FirstOrDefault(section => section.Key == "site");
        if (siteSection is not null)
        {
            sections.Remove(siteSection);
            var details = sections.FindIndex(section => section.Key is "unit" or "structure" or "tile");
            sections.Insert(details < 0 ? sections.Count : details, siteSection);
        }
        if (stamp.Revision != revision)
            throw new DomainInvariantException("Battle changed during HUD projection.");
        var recruitmentQueries = human && selection.Kind == TacticalSelectionKind.Idle
            ? GetTacticalQueries(stamp.Binding) : null;
        var canOpenRecruitment = recruitmentQueries is not null
            && recruitmentQueries.InteractiveCells.Any(recruitmentQueries.CanOpenRecruitment);
        var selectionHint = S(state, "phase") == "over"
            ? text.Format("ui.hud.battle_over")
            : !human ? text.Format("ui.hud.computer_thinking") : SelectionHint(selection.Kind, canOpenRecruitment);
        return new(
            stamp,
            selectionHint,
            System.Array.AsReadOnly(sections.ToArray()),
            inspect is null ? null : UnitIcon(inspect),
            busy);
    }

    private string SelectionHint(TacticalSelectionKind kind, bool canRecruit) => kind switch
    {
        TacticalSelectionKind.Idle => text.Format(canRecruit ? "ui.hud.selection.idle_recruit" : "ui.hud.selection.idle"),
        TacticalSelectionKind.Unit => text.Format("ui.hud.selection.unit"),
        TacticalSelectionKind.Destination => text.Format("ui.hud.selection.destination"),
        TacticalSelectionKind.Target => text.Format("ui.hud.selection.target"),
        TacticalSelectionKind.Deploying => text.Format("ui.hud.selection.deploying"),
        TacticalSelectionKind.DeploymentUnit => text.Format("ui.hud.selection.deployment_unit"),
        TacticalSelectionKind.Disembark => text.Format("ui.hud.selection.disembark"),
        TacticalSelectionKind.Tactic => text.Format("ui.hud.selection.tactic"),
        TacticalSelectionKind.Recruit => text.Format("ui.hud.selection.recruit"),
        TacticalSelectionKind.Site => text.Format("ui.hud.selection.site"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private JsonObject Definition(string family, string id) => engine.Catalog.Definition(family, id) is { } value
        ? EcmaJson.Parse(value.GetRawText())!.AsObject() : throw new DomainInvariantException($"Unknown {family} definition {id}.");
    private static string S(JsonObject value, string key) => value[key] is { } node ? EcmaJson.ReadString(node) : "";
    private static int N(JsonObject value, string key) => value[key]!.GetValue<int>();
    private static double D(JsonObject value, string key) => Number(value[key]);
    private static double Number(JsonNode? value) => value is null ? 0 : EcmaJson.ToElement(value).GetDouble();
    private static bool B(JsonObject value, string key) => value[key]?.GetValue<bool>() == true;
    private static string F(double value) => EcmaJson.Stringify(JsonValue.Create(value));
    private static string Signed(double value) => (value >= 0 ? "+" : "") + F(value);
    private static string Pct(double value) => F(Math.Floor(value * 100 + 0.5)) + "%";
    private string Cover(string cover) => text.Format(cover == "full" ? "ui.hud.cover_full" : cover == "half" ? "ui.hud.cover_half" : "ui.hud.cover_none");
    private string Power(IReadOnlyList<JsonObject> weapons) => weapons.Count == 0
        ? text.Format("ui.hud.not_applicable") : F(weapons.Max(value => D(value, "power")));

    private string Range(IReadOnlyList<JsonObject> weapons)
    {
        if (weapons.Count == 0)
            return text.Format("ui.hud.not_applicable");
        var min = weapons.Min(value => D(value, "minRange"));
        var max = weapons.Max(value => D(value, "maxRange"));
        return min == max ? F(min) : $"{F(min)}-{F(max)}";
    }
    private string Facing(JsonObject state, string facing) => contentText.DirectionName(RuleGrid(state).Id, facing);
    private string ModifierLabel(CombatModifier modifier) => modifierText.Describe(modifier);
}
