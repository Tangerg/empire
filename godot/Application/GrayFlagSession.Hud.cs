using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Combat;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
using Empire.Core.Resources;
using Empire.Core.Tactics;
using Empire.Presentation;
using Empire.Presentation.Tactical;

namespace Empire.Application;

public sealed partial class GrayFlagSession
{
    public TacticalHudProjection ProjectHud(TacticalStamp stamp, TacticalSelection selection, BoardCell? cursor, int? inspectId, bool busy)
    {
        if (stamp.Revision != revision) throw new DomainInvariantException("HUD projection belongs to a stale session revision.");
        var state = battle.Snapshot();
        var sections = new List<TacticalHudSection>();
        var current = TacticalQueries.Player(state, N(state, "currentPlayer"));
        var human = S(current, "controller") == "human";
        var visible = VisibleUnitIds(state, Viewer(state));
        var selected = selection.UnitId is { } selectedId && visible.Contains(selectedId) ? TacticalQueries.UnitById(state, selectedId) : null;
        var commandable = human && selected is not null && engine.Turns.MayAct(state, selected) ? selected : null;
        var formations = commandable is null ? [] : engine.Progression.Formations.Options(state, commandable);
        var inspect = inspectId is { } iid && visible.Contains(iid) ? TacticalQueries.UnitById(state, iid) : null;
        void Section(string key, string title, IEnumerable<TacticalHudRow> rows) { var values = rows.ToArray(); if (values.Length > 0) sections.Add(new(key, title, System.Array.AsReadOnly(values))); }
        TacticalHudRow Pick(string key, string label, TacticalHudIntentKind kind, string? argument = null, int? unit = null, string hint = "", bool enabled = true, bool active = false) =>
            new(key, label, hint, enabled, active, new(stamp, kind, argument, unit));
        TacticalHudRow Action(string key, string label, JsonObject action, string hint = "", bool enabled = true, bool active = false) =>
            new(key, label, hint, enabled, active, new(stamp, TacticalHudIntentKind.Action, Action: EcmaJson.ToElement(action)));
        TacticalHudPortrait UnitIcon(JsonObject unit) => new(S(unit,"type"),unit["key"] is null ? null : S(unit,"key"),state["players"]!.AsArray().OfType<JsonObject>().FirstOrDefault(player => N(player,"id") == N(unit,"owner")) is { } team ? S(team,"color") : "#999999");
        string Name(JsonObject unit) => S(Definition("units", S(unit, "type")), "name");
        string Accounts(string kind, JsonObject holder, string? weapon = null) => string.Join(" · ", engine.Resources.Accounts(new(kind, holder, weapon)).Select(account =>
            $"{account.Name} {(account.Current is null ? "∞" : account.Capacity is null ? F(account.Current.Value) : $"{F(account.Current.Value)}/{F(account.Capacity.Value)}")}"));
        string Costs(IEnumerable<BattleResources.Cost> costs) { var text = string.Join(" · ", costs.Select(cost => $"{engine.Resources.Name(cost.Resource) ?? cost.Resource} {F(cost.Amount)}")); return text.Length == 0 ? "无" : text; }
        string Amounts(JsonNode? amounts) => Costs((amounts as JsonArray ?? new()).OfType<JsonObject>().Select(cost => new BattleResources.Cost(S(cost,"resource"), D(cost,"amount"))));
        Section("crown", S(state,"levelName"), new[] { new TacticalHudRow("turn", $"第 {state["turn"]}{(state["rules"]?["turnLimit"] is { } limit && limit.GetValue<double>() != 0 ? $" / {limit}" : "")} 回合 · {S(current,"name")} · {(human ? "你的回合" : "AI 行动中")}"), new("accounts", Accounts("player",current)) });
        if (state["turnOrder"]?["activeUnit"] is not null)
            Section("initiative", "行动序", engine.Turns.Preview(state,6).Select((id,index) => new TacticalHudRow($"actor:{index}", Name(TacticalQueries.UnitById(state,id)!), Selected:index == 0 && id == N(state["turnOrder"]!.AsObject(),"activeUnit"), UnitIcon:UnitIcon(TacticalQueries.UnitById(state,id)!))));
        Section("casts", "咏唱中", engine.Combat.ActiveCasts(state).Select((cast,index) => new TacticalHudRow($"cast:{index}", $"{cast.Name} · 目标 {cast.Target.X},{cast.Target.Y} · 还有 {F(cast.Remaining)} 个行动轮", $"进度 {Pct(cast.Progress)}",Gauge:new(cast.Progress,1))));
        Section("objectives", "作战目标", engine.Victory.Objectives(state,N(state["players"]!.AsArray().OfType<JsonObject>().FirstOrDefault(player => S(player,"controller") == "human") ?? state["players"]![0]!.AsObject(),"id")).Select(objective => new TacticalHudRow(objective.Id,$"{objective.Description} · {objective.Progress}")));
        Section("roster", "双方兵力", state["players"]!.AsArray().OfType<JsonObject>().Select(player => new TacticalHudRow($"player:{player["id"]}",$"{S(player,"name")} · {state["units"]!.AsArray().OfType<JsonObject>().Count(unit => N(unit,"owner") == N(player,"id"))} 单位 · {Accounts("player",player)}")));
        if (S(state,"phase") == "deployment" && human)
        {
            Section("deployment","战前部署",engine.Deployment.Roster(state)!.Units.Select(unit => Pick($"deploy:{unit["id"]}",$"{Name(unit)} {unit["x"]},{unit["y"]}",TacticalHudIntentKind.DeployPick,unit:N(unit,"id"),enabled:!busy,active:selection.UnitId == N(unit,"id"))));
            if (selected is not null && cursor is { } deploymentCursor)
            {
                var swap = engine.Deployment.Spots(state,selected).FirstOrDefault(spot => spot.At == new Coord(deploymentCursor.X,deploymentCursor.Y))?.Swaps;
                if (swap is not null) Section("swap","部署换位",new[] {new TacticalHudRow("swap",$"落在此格：与{Name(swap)}换位")});
            }
        }
        else if (selection.TargetingLabel is not null) Section("target","选择目标",new[] {Pick("cancel","取消 Esc",TacticalHudIntentKind.Back)});
        else if (!busy && selection.Kind == TacticalSelectionKind.Destination && selected is not null && selection.Destination is { } destination)
            Section("commands","指令",engine.CommandsAt(state,selected,new(destination.X,destination.Y)).Select(command => Pick(command.Key,command.Name,TacticalHudIntentKind.Command,command.Key,hint:command.Hint) with { Icon = command.Weapon is { } id ? "weapon:" + id : "ability:" + command.Ability }).Append(Pick("cancel","取消 Esc",TacticalHudIntentKind.Back)));
        if (!busy && human && selection.Kind == TacticalSelectionKind.Idle && S(state,"phase") == "playing")
            Section("tactics","指挥战术",state["commanders"]!.AsArray().OfType<JsonObject>().SelectMany(commander => engine.Support.Tactics(state,S(commander,"id")).Select(tactic => Pick($"{S(commander,"id")}:{tactic.Id}",$"{tactic.Name} · {Costs(tactic.Costs)}",TacticalHudIntentKind.Tactic,$"{S(commander,"id")}:{tactic.Id}"))));
        if (selection.Kind == TacticalSelectionKind.Target && selected is not null && selection.Weapon is { } weapon && cursor is { } target && selection.Targets.Contains(target) && selection.Destination is { } from)
        {
            var defender = TacticalQueries.UnitAt(state,new(target.X,target.Y));
            if (defender is not null)
            {
                var plan = engine.Combat.Forecast(state,selected,new(target.X,target.Y),new(from.X,from.Y),weapon);
                if (plan.PrimaryUnit is { } exchange)
                {
                    var recipient = TacticalQueries.UnitById(state,exchange.DamageRecipient) ?? defender;
                    var rows = new List<TacticalHudRow> { new("exchange",$"{Name(selected)} → {Name(defender)}") };
                    if (exchange.Interceptor is not null) rows.Add(new("interceptor",$"{Name(recipient)} 将进行援护并承受伤害"));
                    else if (exchange.Reaction is { } reaction) rows.Add(new("reaction",$"目标将触发「{engine.Combat.Forecasts.Reaction(reaction.Stance).Name}」姿态"));
                    if (plan.UnitHits.Count + plan.StructureHits.Count > 1) rows.Add(new("collateral",$"范围攻击还将波及 {plan.UnitHits.Count - 1} 个单位、{plan.StructureHits.Count} 个结构"));
                    if (plan.SupportAttack is { } support) rows.Add(new("support",$"援护攻击预计追加 {F(support.Damage.Damage)} 点伤害"));
                    rows.Add(new("strike",$"造成伤害 {F(exchange.Strike.Damage)} · {recipient["hp"]} → {F(exchange.RecipientHpAfter)}",Gauge:new(exchange.RecipientHpAfter,D(Definition("units",S(recipient,"type")),"maxHp"))));
                    rows.Add(new("counter",$"遭到反击 {(exchange.Counter is null ? "无" : F(exchange.Counter.Damage))} · {selected["hp"]} → {F(exchange.AttackerHpAfter)}",Gauge:new(exchange.AttackerHpAfter,D(Definition("units",S(selected,"type")),"maxHp"))));
                    rows.Add(new("damage-type",$"攻击修正链 · {S(Definition("damageTypes",exchange.Strike.DamageType),"name")} → {S(Definition("armorClasses",S(Definition("units",S(recipient,"type")),"armorClass")),"name")}"));
                    rows.AddRange(exchange.Strike.Modifiers.Select((modifier,index) => new TacticalHudRow($"strike-mod:{index}",ModifierLabel(modifier))));
                    rows.Add(new("mitigation",$"最终减伤上限后 {Pct(exchange.Strike.Mitigation)}"));
                    if (exchange.RecipientDies) rows.Add(new("recipient-dies","可以击杀伤害承担者"));
                    if (exchange.AttackerDies) rows.Add(new("attacker-dies","反击会导致我方阵亡"));
                    if (exchange.Counter is { } counter) rows.AddRange(counter.Modifiers.Select((modifier,index) => new TacticalHudRow($"counter-mod:{index}",$"反击 · {ModifierLabel(modifier)}")));
                    Section("forecast","战斗预测",rows);
                }
            }
        }
        if (inspect is not null)
        {
            var definition = Definition("units",S(inspect,"type"));
            var weapons = definition["weapons"]!.AsArray().Select(id => Definition("weapons",EcmaJson.ReadString(id))).ToArray();
            var owner = state["players"]!.AsArray().OfType<JsonObject>().FirstOrDefault(player => N(player,"id") == N(inspect,"owner"));
            var rows = new List<TacticalHudRow> {
                new("owner",owner is null ? "中立" : S(owner,"name")), new("hp",$"生命 {inspect["hp"]} / {definition["maxHp"]}", Gauge:new(D(inspect,"hp"),D(definition,"maxHp"))), new("blurb",S(definition,"blurb")),
                new("power",$"最高威力 {(weapons.Length == 0 ? "-Infinity" : F(weapons.Max(value => D(value,"power"))))}"),
                new("defense",$"减伤 {Pct(D(definition,"defense"))}"), new("movement",$"移动 {definition["movement"]}"),
                new("range",$"射程 {Range(weapons)}"), new("damage",$"伤害 {string.Join(" / ",weapons.Select(value => S(Definition("damageTypes",S(value,"damageType")),"name")).Distinct())}"),
                new("armor",$"护甲 {S(Definition("armorClasses",S(definition,"armorClass")),"name")}"),
                new("movement-type",$"移动型 {S(Definition("movementProfiles",S(definition,"movementClass")),"name")}"), new("value",$"战力价值 {definition["value"]}") };
            if (!weapons.Any(value => B(value,"moveAndAttack"))) rows.Add(new("stationary","移动后无法攻击"));
            rows.Add(new("capture",definition["abilities"]!.AsArray().Any(id => EcmaJson.ReadString(id) == "capture") ? "可占领" : "不可占领"));
            if (definition["abilities"]!.AsArray().Any(id => EcmaJson.ReadString(id) == "heal")) rows.Add(new("heal","可治疗"));
            rows.Add(new("reaction",$"反应：{engine.Combat.Forecasts.Reaction(S(inspect,"reaction")).Name}"));
            if (B(inspect,"done")) rows.Add(new("done","已行动"));
            foreach (var arm in weapons)
            {
                var id = S(arm,"id"); var stock = Accounts("weapon",inspect,id);
                var cooldown = D(inspect["weaponState"]![id]!.AsObject(),"cooldownRemaining");
                rows.Add(new($"weapon:{id}",$"{S(arm,"name")} · {S(Definition("damageTypes",S(arm,"damageType")),"name")} {arm["power"]} · 射程 {Range(new[] {arm})} · {(stock.Length > 0 ? stock : "无限制")}{(cooldown > 0 ? $" · 冷却 {F(cooldown)}" : "")}{(arm["resourceRequirements"]!.AsArray().Count > 0 ? $" · 需要 {Amounts(arm["resourceRequirements"])}" : "")}{(arm["resourceCosts"]!.AsArray().Count > 0 ? $" · 消耗 {Amounts(arm["resourceCosts"])}" : "")}", Icon:"weapon:" + id));
            }
            var statuses = inspect["statuses"]!.AsArray().OfType<JsonObject>().ToArray();
            if (statuses.Length == 0) rows.Add(new("status:none","无状态效果"));
            rows.AddRange(statuses.Select((status,index) => new TacticalHudRow($"status:{index}",$"{S(Definition("statuses",S(status,"id")),"name")} · {status["remaining"]} 回合{(D(status,"stacks") > 1 ? $" · {status["stacks"]} 层" : "")}", Icon:"status:" + S(status,"id"))));
            var threshold = engine.Combat.Effects.NextRankThreshold(N(inspect,"rank"));
            rows.Add(new("rank",$"军衔 {new[] {"新兵","老兵","精英"}[N(inspect,"rank")]}{(threshold is { } next ? $" · {inspect["rankProgress"]}/{F(next)}" : "")}"));
            rows.Add(new("facing",$"朝向 {Facing(state,S(inspect,"facing"))}"));
            if (inspect["career"]?["current"] is { } careerId)
            {
                var id = EcmaJson.ReadString(careerId); var career = Definition("careers",id);
                rows.Add(new("career",$"职业 {S(career,"name")} · 熟练度 {inspect["career"]?["mastery"]?[id] ?? JsonValue.Create(0)}/{career["masteryThreshold"]}"));
            }
            if (inspect["formation"] is { } formationId)
            {
                // Preserve source: holding is read from commandable, not inspected, options.
                var holds = formations.Any(option => option.Current && option.Eligible);
                rows.Add(new("formation",$"阵形 {S(Definition("formations",EcmaJson.ReadString(formationId)),"name")}{(holds ? "" : " · 缺少相邻友军")}"));
            }
            var account = Accounts("unit",inspect); if (account.Length > 0) rows.Add(new("unit-resources",$"单位资源 {account}"));
            var commander = state["commanders"]!.AsArray().OfType<JsonObject>().FirstOrDefault(value => S(value,"id") == S(inspect,"commanderId"));
            if (commander is not null) rows.Add(new("commander",$"编队 {S(commander,"id")} · {(TacticalModifiers.CommanderUnit(state,commander) is null ? "指挥官已离场" : TacticalModifiers.ActiveCommanderFor(RuleGrid(state),state,inspect) is null ? "超出指挥范围" : "光环生效")}"));
            Section("unit",Name(inspect),rows);
            if (commandable is not null && N(commandable,"id") == N(inspect,"id"))
            {
                var id = N(commandable,"id");
                Section("stances","反应与朝向",engine.Combat.Forecasts.Reactions.All().Select(stance => Action($"reaction:{stance.Id}",stance.Name,new() { ["kind"]="reaction",["unit"]=id,["stance"]=stance.Id },hint:stance.Hint,active:S(commandable,"reaction") == stance.Id))
                    .Concat(RuleGrid(state).Directions.Select(direction => Action($"face:{direction.Id}",Facing(state,direction.Id),new() { ["kind"]="face",["unit"]=id,["facing"]=direction.Id },active:S(commandable,"facing") == direction.Id))));
                Section("formations","阵形",formations.Select(option => Action($"formation:{S(option.Formation,"id")}",$"{S(option.Formation,"name")}{(option.Current ? " · 解除" : "")}",new() { ["kind"]="changeFormation",["unit"]=id,["formation"]=option.Current ? null : option.Formation["id"]!.DeepClone() },
                    option.Reasons.Count > 0 ? string.Join("；",option.Reasons) : $"攻击 ×{option.Formation["attackMultiplier"]} · 防御 {Signed(D(option.Formation,"defenseDelta"))} · 移动 {Signed(D(option.Formation,"movementDelta"))}",option.Eligible || option.Current,option.Current)));
                Section("carriers","登载",engine.Transport.CarrierOptions(state,commandable).Select(option => Action($"carrier:{option.Carrier["id"]}",Name(option.Carrier),new() { ["kind"]="embark",["unit"]=id,["carrier"]=option.Carrier["id"]!.DeepClone() },option.Reasons.Count > 0 ? string.Join("；",option.Reasons) : "登上这辆载具",option.Eligible)));
                Section("passengers","卸载",engine.Transport.PassengerOptions(state,commandable).Select(option => Pick($"passenger:{option.Unit["id"]}",Name(option.Unit),TacticalHudIntentKind.Disembark,unit:N(option.Unit,"id"),hint:option.Spots.Count > 0 ? $"可在 {option.Spots.Count} 格卸载" : "周围没有可以卸载的格子",enabled:option.Spots.Count > 0)));
                Section("careers","职业树与转职",engine.Progression.Careers.Options(state,commandable).Select(option => Action($"career:{S(option.Career,"id")}",$"{S(option.Career,"name")} · T{option.Career["tier"]}{(option.Mastered ? " · 精通" : option.Unlocked ? " · 已解锁" : "")}",new() { ["kind"]="changeCareer",["unit"]=id,["career"]=option.Career["id"]!.DeepClone() },option.Reasons.Count > 0 ? string.Join("；",option.Reasons) : option.Mastered ? "已精通，转职后可用该职业的精通技能" : option.Unlocked ? "已解锁，可自由切换" : "满足进阶条件",option.Eligible)));
            }
        }
        if (cursor is { } tile && engine.Space.Board(state).Contains(new(tile.X,tile.Y)))
        {
            var index = engine.Space.Board(state).IndexOf(new(tile.X,tile.Y));
            var terrain = Definition("terrains",EcmaJson.ReadString(state["map"]!["tiles"]![index]));
            var rows = new List<TacticalHudRow> {new("coordinate",$"{tile.X}, {tile.Y}"),new("defense",$"防御 {Pct(D(terrain,"defense"))}"),new("elevation",$"海拔 {state["map"]!["elevation"]![index]}"),new("cover",Cover(S(terrain,"cover")))};
            if (B(terrain,"capturable")) rows.Add(new("owner",state["players"]!.AsArray().OfType<JsonObject>().FirstOrDefault(player => player["id"]!.ToJsonString() == state["map"]!["owners"]![index]?.ToJsonString()) is { } holder ? S(holder,"name") : "中立"));
            if (D(terrain,"heal") != 0) rows.Add(new("heal",$"治疗 {terrain["heal"]}/回合"));
            if (terrain["ownerTurnGrants"]!.AsArray().Count > 0) rows.Add(new("grants",$"产出 {Amounts(terrain["ownerTurnGrants"])}"));
            rows.Add(new("movement-costs",string.Join(" · ",engine.Catalog.Definitions("movementProfiles").Select(value => EcmaJson.Parse(value.GetRawText())!.AsObject()).Select(profile => $"{S(profile,"name")} {terrain["cost"]?[S(profile,"id")]?.ToString() ?? "—"}"))));
            Section("tile",S(terrain,"name"),rows);
            var structure = state["structures"]!.AsArray().OfType<JsonObject>().FirstOrDefault(value => TacticalQueries.Position(value) == new Coord(tile.X,tile.Y));
            if (structure is not null)
            {
                var def = Definition("structures",S(structure,"type"));
                var facts = new List<TacticalHudRow> {new("hp",$"{structure["hp"]} / {def["maxHp"]}", Gauge:new(D(structure,"hp"),D(def,"maxHp")))};
                var holder = state["players"]!.AsArray().OfType<JsonObject>().FirstOrDefault(player => N(player,"id") == N(structure,"owner"));
                if (holder is not null) facts.Add(new("owner",S(holder,"name")));
                foreach (var (key,label) in new[] {("blocksMovement","阻挡通行"),("blocksVision","阻挡视线"),("repairable","可修复")}) if (B(def,key)) facts.Add(new(key,label));
                if (S(def,"cover") is "full" or "half") facts.Add(new("cover",Cover(S(def,"cover"))));
                if (!B(def,"targetable")) facts.Add(new("targetable","不可被攻击"));
                if (B(structure,"disabled")) facts.Add(new("disabled","已失效"));
                Section("structure",S(def,"name"),facts);
            }
        }
        if (selection.Kind == TacticalSelectionKind.Recruit && selection.Destination is { } recruitAt)
            Section("recruit","征募单位",new[] {new TacticalHudRow("accounts",Accounts("player",current)),Pick("cancel","关闭",TacticalHudIntentKind.Back)}.Concat(engine.Recruitment.Cards(state,new(recruitAt.X,recruitAt.Y)).Select(card => {
                var def = Definition("units",card.Unit); var weapons = def["weapons"]!.AsArray().Select(id => Definition("weapons",EcmaJson.ReadString(id))).ToArray();
                return Pick(card.Unit,$"{S(def,"name")} · {Costs(card.Costs)}",TacticalHudIntentKind.Recruit,card.Unit,hint:$"{(weapons.Length == 0 ? "-Infinity" : F(weapons.Max(value => D(value,"power"))))} · 生命 {def["maxHp"]} · 移动 {def["movement"]} · 射程 {Range(weapons)} · {S(def,"blurb")}",enabled:card.Affordable) with { UnitIcon = new(card.Unit,null,S(current,"color")) };
            })));
        if (stamp.Revision != revision) throw new DomainInvariantException("Battle changed during HUD projection.");
        return new(stamp,S(state,"phase") == "over" ? "对局结束。" : !human ? "AI 正在思考…" : selection.Hint,System.Array.AsReadOnly(sections.ToArray()), inspect is null ? null : new(S(inspect,"type"),inspect["key"] is null ? null : S(inspect,"key"),state["players"]!.AsArray().OfType<JsonObject>().FirstOrDefault(player => N(player,"id") == N(inspect,"owner")) is { } team ? S(team,"color") : "#999999"),busy);
    }

    private JsonObject Definition(string family,string id) => engine.Catalog.Definition(family,id) is { } value
        ? EcmaJson.Parse(value.GetRawText())!.AsObject() : throw new DomainInvariantException($"Unknown {family} definition {id}.");
    private static string S(JsonObject value,string key) => value[key] is { } node ? EcmaJson.ReadString(node) : "";
    private static int N(JsonObject value,string key) => value[key]!.GetValue<int>();
    private static double D(JsonObject value,string key) => value[key]?.GetValue<double>() ?? 0;
    private static bool B(JsonObject value,string key) => value[key]?.GetValue<bool>() == true;
    private static string F(double value) => EcmaJson.Stringify(JsonValue.Create(value));
    private static string Signed(double value) => (value >= 0 ? "+" : "") + F(value);
    private static string Pct(double value) => F(Math.Floor(value * 100 + 0.5)) + "%";
    private static string Cover(string cover) => cover == "full" ? "全掩体" : cover == "half" ? "半掩体" : "无掩体";
    private static string Range(IReadOnlyList<JsonObject> weapons)
    {
        if (weapons.Count == 0) return "Infinity--Infinity";
        var min = weapons.Min(value => D(value,"minRange")); var max = weapons.Max(value => D(value,"maxRange"));
        return min == max ? F(min) : $"{F(min)}-{F(max)}";
    }
    private string Facing(JsonObject state,string facing)
    {
        var name = RuleGrid(state).Directions.Where(direction => direction.Id == facing).Select(direction => direction.Name).FirstOrDefault() ?? facing;
        var arrow = facing switch { "north" => "↑", "east" or "hexEast" => "→", "south" => "↓", "west" or "hexWest" => "←", "northeast" or "hexNortheast" => "↗", "southeast" or "hexSoutheast" => "↘", "southwest" or "hexSouthwest" => "↙", "northwest" or "hexNorthwest" => "↖", _ => "" };
        return name + (arrow.Length > 0 ? " " + arrow : "");
    }
    private static string ModifierLabel(CombatModifier modifier)
    {
        var source = modifier.Source switch { "weapon" => "武器", "matchup" => "克制", "unit" => "单位", "status" => "状态", "commander" => "指挥", "terrain" => "地形", "reaction" => "反应", "elevation" => "高低差", "position" => "方位", "cover" => "掩体", "formation" => "阵形", "extension" => "扩展", _ => modifier.Source };
        var value = modifier.Stage == "mitigation" ? (modifier.Value >= 0 ? "+" : "") + Pct(modifier.Value) + " 减伤" : modifier.Operation == "multiply" ? "×" + modifier.Value.ToString("F2",System.Globalization.CultureInfo.InvariantCulture) : (modifier.Value >= 0 ? "+" : "") + modifier.Value.ToString("F2",System.Globalization.CultureInfo.InvariantCulture);
        return $"{source} {modifier.Label} {value}";
    }
}
