using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.State;
using Empire.Core.Tactics;

namespace Empire.Core.Abilities;

public static class DefaultAbilities
{
    public static AbilityRegistry Create() => new AbilityRegistry()
        .Register(new("attack", "攻击")
        {
            Hint = "对射程内的敌人造成伤害；若对方也能打到你，会遭到反击。",
            SelfTargeted = false, Priority = 10, Tags = ["attack"], Engagement = "attack",
            WeaponFor = SelectedWeapon,
            WeaponChoices = (rules, query) => rules.Combat.Forecasts.UnitWeapons(query.Unit),
            Targets = (rules, query) => SelectedWeapon(rules, query) is { } weapon
                ? rules.Space.AttackTargetCoords(query.State, query.Unit, query.At, weapon) : [],
            Usable = (rules, query) => rules.Combat.CanUseWeapon(query.State, query.Unit, query.Moved, query.WeaponId),
            Execute = (rules, query, target, emit) => rules.Combat.ExecuteAttack(query.State, query.Unit, query.At,
                target ?? throw new DomainInvariantException("attack requires a target"), query.WeaponId, emit),
        })
        .Register(new("heal", "治疗")
        {
            Hint = "恢复相邻友军的生命值。", SelfTargeted = false, Priority = 20, Tags = ["healing"],
            Targets = (rules, query) => rules.Space.HealTargetsFrom(query.State, query.Unit, query.At)
                .Select(TacticalQueries.Position).ToArray(),
            Execute = (rules, query, target, emit) => Heal(rules, query,
                target ?? throw new DomainInvariantException("heal requires a target"), emit),
        })
        .Register(new("capture", "占领")
        {
            Hint = "占下城镇、兵营或城堡；只有人类步兵可以占领。", Priority = 5,
            Usable = (rules, query) => rules.Support.CanCapture(query.State, query.Unit, query.At),
            Execute = (rules, query, _, emit) => rules.Support.Capture(query.State, query.Unit, query.At, emit),
        })
        .Register(new("wait", "待机") { Hint = "结束该单位的行动。", Priority = 90 });

    private static JsonObject? SelectedWeapon(AbilityRules rules, AbilityQuery query) =>
        rules.Combat.SelectedWeapon(query.State, query.Unit, query.WeaponId);

    public static double HealAmount(ReadOnlyContent content, JsonObject unit) =>
        Math.Min(30, new UnitHealth(content, unit).Missing);

    private static void Heal(AbilityRules rules, AbilityQuery query, Coord target, Action<JsonObject> emit)
    {
        var unit = TacticalQueries.UnitAt(query.State, target) ?? throw new DomainInvariantException("no unit to heal");
        var amount = new UnitHealth(rules.Content, unit).Heal(HealAmount(rules.Content, unit));
        emit(new JsonObject { ["type"] = "heal", ["source"] = query.Unit["id"]!.DeepClone(), ["target"] = unit["id"]!.DeepClone(), ["amount"] = amount });
        rules.Combat.Effects.AwardRankProgress(query.Unit, Math.Max(5, amount), emit);
    }
}
