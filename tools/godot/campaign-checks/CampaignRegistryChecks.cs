using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Campaign;
using Empire.Core.Registries;

internal static class CampaignRegistryChecks
{
    public static void Run(string path, Action<JsonNode?, JsonNode?, string> equal)
    {
        var fixture = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var registries = new JsonArray
        {
            new JsonObject
            {
                ["family"] = "condition",
                ["checks"] = RegistryChecks(new CampaignConditionRegistry(),
                    kind => new CampaignConditionHandler(kind, (_, _, _) => true),
                    handler => handler.Kind, registry => registry.Clone()),
            },
            new JsonObject
            {
                ["family"] = "effect",
                ["checks"] = RegistryChecks(new CampaignEffectRegistry(),
                    kind => new CampaignEffectHandler(kind, (_, _) => { }),
                    handler => handler.Kind, registry => registry.Clone()),
            },
            new JsonObject
            {
                ["family"] = "node",
                ["checks"] = RegistryChecks(new CampaignNodeRegistry(),
                    kind => new CampaignNodeHandler(kind, (_, _) => { }),
                    handler => handler.Kind, registry => registry.Clone()),
            },
        };
        equal(registries, fixture["registries"], "campaign registry result/error parity mismatch");
        Console.WriteLine("PASS 27 source campaign registry result/error comparisons (condition, effect, node)");

        var definition = fixture["runtimes"]!.AsArray()
            .Single(item => item!["name"]!.GetValue<string>() == "choice availability and errors")!["definition"]!.AsObject();
        equal(Extensions(definition), fixture["extensions"], "campaign custom-extension/frozen-clone parity mismatch");
        Console.WriteLine("PASS source custom campaign condition/effect/node execution, frozen-clone isolation, and validator replacement/error comparisons");
    }

    private static JsonArray RegistryChecks<TEntry, TRegistry>(TRegistry registry, Func<string, TEntry> handler,
        Func<TEntry, string> kind, Func<TRegistry, TRegistry> clone)
        where TEntry : class
        where TRegistry : KeyedRegistry<TEntry, TRegistry>
    {
        var checks = new JsonArray();
        void Check(string name, Func<object?> operation)
        {
            var result = Observe(operation);
            result["name"] = name;
            checks.Add(result);
        }

        Check("empty registry", () => new
        {
            count = registry.Count, keys = registry.Keys(), all = registry.All().Select(kind), @sealed = registry.IsSealed,
        });
        Check("register preserves insertion order", () =>
        {
            registry.Register(handler("z"));
            registry.Register(handler("a"));
            return registry.Keys();
        });
        Check("duplicate rejects", () => registry.Register(handler("z")));
        Check("lookup distinguishes query and demand", () => new
        {
            present = registry.Has("a"), absent = registry.Has("missing"),
            optional = registry.TryGet("missing"), found = kind(registry.Get("a")),
        });
        Check("unknown lookup rejects", () => registry.Get("missing"));
        Check("replace existing and introduce new", () =>
        {
            registry.Replace(handler("z"));
            registry.Replace(handler("new"));
            return registry.Keys();
        });
        Check("seal prevents register", () =>
        {
            registry.Seal();
            return registry.Register(handler("late"));
        });
        Check("seal prevents replace", () => registry.Replace(handler("a")));
        Check("clone is independent and mutable", () =>
        {
            var copied = clone(registry);
            copied.Register(handler("clone-only"));
            return new
            {
                original = registry.Keys(), cloned = copied.Keys(), originalSealed = registry.IsSealed,
                cloneSealed = copied.IsSealed, count = copied.Count,
            };
        });
        return checks;
    }

    private static JsonObject Extensions(JsonObject sourceDefinition)
    {
        var rules = CampaignRules.Create();
        rules.Conditions.Register(new("test.threshold", (state, condition, _) =>
            state["resources"]!["gold"]!.GetValue<double>() >= condition["minimum"]!.GetValue<double>()));
        rules.Effects.Register(new("test.bonus", (state, effect) =>
            state["resources"]!["gold"] = state["resources"]!["gold"]!.GetValue<double>() + effect["amount"]!.GetValue<double>() * 2));
        rules.Nodes.Register(new("test.camp", (context, node) =>
        {
            context.Apply(node["effects"] as JsonArray);
            context.MoveTo(node["next"]!.GetValue<string>());
        }, ValidateCamp));

        var definition = (JsonObject)sourceDefinition.DeepClone();
        definition["nodes"]![0]!["type"] = "test.camp";
        definition["nodes"]![0]!["effects"] = JsonNode.Parse("""[{"type":"test.bonus","amount":5}]""");
        definition["nodes"]![1]!["choices"]![1]!["condition"] = JsonNode.Parse("""
            {"type":"all","conditions":[{"type":"test.threshold","minimum":15},{"type":"not","condition":{"type":"flag","flag":"absent"}}]}
            """);
        var runtime = new CampaignRuntime(definition, rules: rules);
        rules.Conditions.Replace(new("test.threshold", (_, _, _) => false));
        rules.Effects.Replace(new("test.bonus", (_, _) => throw new Exception("mutated external effect")));
        rules.Nodes.Replace(new("test.camp", (_, _) => throw new Exception("mutated external node")));
        var checks = new JsonArray(Observe(() => runtime.Advance()), Observe(() => runtime.Choices()),
            Observe(() => runtime.Choose("locked")));

        var badDefinition = (JsonObject)definition.DeepClone();
        badDefinition["nodes"]![0]!["next"] = "missing";
        var replacedHandler = Observe(() => { _ = new CampaignRuntime(badDefinition, rules: rules); return null; });
        var validating = CampaignRules.Create();
        validating.Nodes.Register(new("test.camp", (_, _) => { }, ValidateCamp));
        var validation = Observe(() => { _ = new CampaignRuntime(badDefinition, rules: validating); return null; });
        return new JsonObject
        {
            ["checks"] = checks,
            ["after"] = runtime.Snapshot(),
            ["replacedHandlerHasNoValidator"] = replacedHandler["error"] is null,
            ["validation"] = validation,
        };
    }

    private static void ValidateCamp(CampaignNodeInspection inspection, JsonObject node)
    {
        var next = node["next"]!.GetValue<string>();
        if (!inspection.HasNode(next)) inspection.Reject($"camp target \"{next}\" is absent");
    }

    private static JsonObject Observe(Func<object?> operation)
    {
        try { return new JsonObject { ["value"] = JsonSerializer.SerializeToNode(operation()), ["error"] = null }; }
        catch (Exception error)
        {
            var name = error switch
            {
                DomainInvariantException => "DomainInvariantError",
                CampaignInvariantException => "CampaignInvariantError",
                CampaignActionException => "CampaignActionError",
                _ when error.GetType() == typeof(Exception) => "Error",
                _ => error.GetType().Name,
            };
            return new JsonObject
            {
                ["value"] = null,
                ["error"] = new JsonObject { ["name"] = name, ["message"] = error.Message },
            };
        }
    }
}
