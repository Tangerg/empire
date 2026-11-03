using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Combat;
using Empire.Core.Determinism;
using Empire.Localization;
using Empire.Presentation.Feedback;

internal static class WeaponHitEffectTextChecks
{
    internal static void Run(GameText text, ContentText content, WeaponHitEffectHandlerRegistry handlers,
        IReadOnlyDictionary<string, string> messages, Action<bool, string> check)
    {
        void Check(bool condition, string label) => check(condition, "hit effect presentation: " + label);
        void Reject<T>(Action operation, string label) where T : Exception
        {
            try
            {
                operation();
            }
            catch (T) { Check(true, label); return; }
            Check(false, label);
        }
        JsonElement Effect(string json) => EcmaJson.ToElement(EcmaJson.Parse(json)!);
        var formatters = WeaponHitEffectText.CreateDefault(new(text, content));
        formatters.ValidateCoverage(handlers.Keys());
        Check(formatters.Keys().SequenceEqual(new[] { "addStatus", "removeStatus", "forcedMove" }),
            "default kinds match their canonical handler identities");

        var status = content.Name(ContentTextKind.Status, "poisoned");
        var add = formatters.Describe(Effect("""{"type":"addStatus","status":"poisoned","duration":3}"""));
        Check(add.Compact == text.Format("combat.hit_effect.add_status.compact", status, 3)
            && add.Detail == text.Format("combat.hit_effect.add_status.detail", status, 3) && !add.Emphasis,
            "status application names content and authored duration without claiming final stacking or survival");
        var remove = formatters.Describe(Effect("""{"type":"removeStatus","status":"poisoned"}"""));
        Check(remove.Compact == text.Format("combat.hit_effect.remove_status.compact", status)
            && remove.Detail == text.Format("combat.hit_effect.remove_status.detail", status) && !remove.Emphasis,
            "status removal stays conditional when the target may not carry it");

        foreach (var mode in new[] { "push", "pull" })
        {
            var direction = text.Format("combat.hit_effect.forced_move." + mode);
            var plain = formatters.Describe(EcmaJson.ToElement(new JsonObject
            {
                ["type"] = "forcedMove",
                ["mode"] = mode,
                ["distance"] = 1,
            }));
            Check(plain.Compact == text.Format("combat.hit_effect.forced_move.compact", direction, 1)
                && plain.Detail == text.Format("combat.hit_effect.forced_move.detail", direction, 1) && !plain.Emphasis,
                mode + " without collision damage describes attempted distance, not a guaranteed destination");
            foreach (var (configured, expected) in new[] { (0.0, 0.0), (0.4, 0.0), (0.5, 1.0), (10.0, 10.0), (10.5, 11.0) })
            {
                var effect = EcmaJson.ToElement(new JsonObject
                {
                    ["type"] = "forcedMove",
                    ["mode"] = mode,
                    ["distance"] = 1,
                    ["collisionDamage"] = configured,
                });
                var before = effect.GetRawText();
                var description = formatters.Describe(effect);
                if (expected == 0)
                    Check(description == plain, $"{mode} collision {configured} rounds to no collision request");
                else
                    Check(description.Compact == text.Format("combat.hit_effect.forced_move.collision_compact", direction, 1, expected)
                        && description.Detail == text.Format("combat.hit_effect.forced_move.collision_detail", direction, 1, expected)
                        && description.Emphasis, $"{mode} collision {configured} discloses canonical request {expected} and its condition");
                Check(effect.GetRawText() == before, "formatting preserves the immutable effect payload");
            }
        }

        Reject<LocalizationException>(() => formatters.Describe(Effect("""{"type":"test.unknown"}""")),
            "unknown kind cannot disappear into a generic fallback");
        Reject<LocalizationException>(() => formatters.Describe(Effect("""{"type":"forcedMove","mode":"teleport","distance":1}""")),
            "unknown forced-movement mode cannot be mistaken for push or pull");
        Reject<LocalizationException>(() => formatters.Describe(Effect("""{"type":"forcedMove","mode":"push","distance":1,"collisionDamage":null}""")),
            "explicit null collision is not treated as an omitted zero");
        Reject<LocalizationException>(() => formatters.Describe(Effect("""{"type":"addStatus","status":"test.absent","duration":3}""")),
            "missing status metadata does not expose an untranslated content id");
        Reject<LocalizationException>(() => formatters.Describe(Effect("""{"type":7}""")),
            "effect kinds must be semantic strings");

        const string customKind = "test.after-hit";
        var applications = 0;
        var extendedHandlers = handlers.Clone().Register(new(customKind, (_, _) => applications++)).Seal();
        Reject<LocalizationException>(() => formatters.ValidateCoverage(extendedHandlers.Keys()),
            "composed extension handlers require explicit presentation coverage");
        GameText CustomCatalog(bool alternate) => new(key => key switch
        {
            "test.hit_effect.compact" => alternate ? "Echo {0}" : "Pulse {0}",
            "test.hit_effect.detail" => alternate ? "Conditional echo {0}" : "Conditional pulse {0}",
            "combat.hit_effect.forced_move.push" => alternate ? "Push" : "Repel",
            "combat.hit_effect.forced_move.collision_compact" => alternate ? "Try {0} {1}; blocked damage {2}" : "May {0} {1}; collision request {2}",
            "combat.hit_effect.forced_move.collision_detail" => alternate ? "If still present, {0} {1}; if blocked, up to {2}" : "On a surviving target, {0} {1}; obstruction may cost up to {2}",
            _ => messages.GetValueOrDefault(key),
        }, CultureInfo.InvariantCulture);
        WeaponHitEffectFormatter CustomFormatter() => new(customKind, ["test.hit_effect.compact", "test.hit_effect.detail"],
            (rules, effect) =>
            {
                var reading = effect.GetProperty("nested").GetProperty("values")[0].GetInt32();
                return new(rules.Messages.Format("test.hit_effect.compact", reading),
                    rules.Messages.Format("test.hit_effect.detail", reading), true);
            });
        WeaponHitEffectText Extended(GameText catalog)
        {
            var result = WeaponHitEffectText.CreateDefault(new(catalog, content)).Register(CustomFormatter());
            result.ValidateCoverage(extendedHandlers.Keys());
            return result.Seal();
        }
        var first = Extended(CustomCatalog(false));
        var second = Extended(CustomCatalog(true));
        var mutable = new JsonObject
        {
            ["type"] = customKind,
            ["nested"] = new JsonObject { ["values"] = new JsonArray(7) },
        };
        var facts = EcmaJson.ToElement(mutable);
        mutable["nested"]!["values"]![0] = 99;
        var originalFacts = facts.GetRawText();
        Check(first.Describe(facts) == new WeaponHitEffectDescription("Pulse 7", "Conditional pulse 7", true)
            && second.Describe(facts) == new WeaponHitEffectDescription("Echo 7", "Conditional echo 7", true),
            "an external kind renders immutable nested facts through two injected catalogs");
        var collisionFacts = Effect("""{"type":"forcedMove","mode":"push","distance":1,"collisionDamage":10}""");
        Check(first.Describe(collisionFacts).Compact == "May Repel 1; collision request 10"
            && second.Describe(collisionFacts).Compact == "Try Push 1; blocked damage 10"
            && first.Describe(collisionFacts).Detail != second.Describe(collisionFacts).Detail,
            "default direction, conditional amount and detail all use injected messages");
        Check(applications == 0 && facts.GetRawText() == originalFacts && mutable["nested"]!["values"]![0]!.GetValue<int>() == 99,
            "presentation neither invokes effect handlers nor changes caller or detached facts");

        var clone = first.Clone();
        clone.Replace(CustomFormatter() with
        {
            Describe = (_, _) => new("Changed", "Changed detail", false)
        });
        Check(!clone.IsSealed && first.IsSealed && clone.Keys().SequenceEqual(first.Keys())
            && clone.Describe(facts) == new WeaponHitEffectDescription("Changed", "Changed detail", false)
            && first.Describe(facts).Compact == "Pulse 7", "replacement changes only the mutable clone and preserves key order");
        Reject<DomainInvariantException>(() => first.Register(CustomFormatter()), "sealed registry refuses registration");
        Reject<DomainInvariantException>(() => first.Replace(CustomFormatter()), "sealed registry refuses replacement");
        Reject<DomainInvariantException>(() => clone.Register(CustomFormatter()), "duplicate registration fails explicitly");
        Reject<LocalizationException>(() => clone.Register(CustomFormatter() with { Kind = " " }), "empty formatter identity fails explicitly");
        Reject<LocalizationException>(() => clone.Register(CustomFormatter() with
        {
            Kind = "test.no-message",
            MessageKeys = ["test.missing.hit-effect-message"],
        }), "extension message coverage is checked on registration");
        var empty = first.Clone().Replace(CustomFormatter() with
        {
            Describe = (_, _) => new("", "Detail", false)
        });
        Reject<LocalizationException>(() => empty.Describe(facts), "registered effects cannot silently omit their compact description");
        var missing = new GameText(key => key == "combat.hit_effect.forced_move.collision_detail"
            ? null : messages.GetValueOrDefault(key), CultureInfo.InvariantCulture);
        Reject<LocalizationException>(() => WeaponHitEffectText.CreateDefault(new(missing, content)),
            "missing default localized detail fails at composition rather than during a battle");
    }
}
