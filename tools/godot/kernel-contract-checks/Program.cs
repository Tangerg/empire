using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Composition;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Campaign;
using Empire.Core.Persistence;

var fixture = EcmaJson.Parse(File.ReadAllText(args[0]))!.AsObject();
var passed = 0;
var failed = 0;
foreach (var item in fixture["cases"]!.AsArray().OfType<JsonObject>())
{
    var input = item["input"]!.AsObject();
    Check(input["name"]!.GetValue<string>(), () => Equal(item["expected"]!, Run(input)));
}
Check("failed composition, repeated composition and retained scopes", () => Equal(fixture["lifecycle"]!, Lifecycle()));
Check("actual composition manifest feeds persistence identity", () => Equal(fixture["persistence"]!, Persistence()));
Check("same-ID same-type keys address one slot and preserve object identity", () =>
{
    var key = new CapabilityKey<object>("third-party.rule");
    var alias = new CapabilityKey<object>(key.Id);
    var value = new object();
    var composed = new SrpgMicrokernel().Use(new("owner", 1, c => c.Provide(alias, value), provides: [key])).Compose();
    Assert(ReferenceEquals(value, composed.Require(key)), "value was copied");
    Assert(ReferenceEquals(value, composed.Require(alias)), "alias created another identity");
    Assert(composed.ProviderOf(alias) == "owner", "alias lost its introducer");
});
Check("declaration type conflict fails deterministically before installation", () =>
{
    var calls = 0;
    var kernel = new SrpgMicrokernel()
        .Use(new("string", 1, _ => calls++, provides: [new CapabilityKey<string>("same")]))
        .Use(new("integer", 1, _ => calls++, requiresCapabilities: [new CapabilityKey<int>("same")]));
    Throws(() => kernel.Compose(), "kernel capability \"same\" has type \"System.String\", not \"System.Int32\"");
    Assert(calls == 0, "type conflict discovered after installation side effects");
});
Check("installation type conflict cannot evade manifest types", () =>
{
    var key = new CapabilityKey<string>("same");
    var kernel = new SrpgMicrokernel().Use(new("owner", 1, c => c.Provide(new CapabilityKey<int>(key.Id), 1), provides: [key]));
    Throws(() => kernel.Compose(), "kernel capability \"same\" has type \"System.String\", not \"System.Int32\"");
});
Check("read and probe types share stable-ID contract", () =>
{
    var key = new CapabilityKey<string>("same");
    var wrong = new CapabilityKey<int>(key.Id);
    var composed = new SrpgMicrokernel().Use(new("owner", 1, c => c.Provide(key, "value"), provides: [key])).Compose();
    foreach (var operation in new Action[] { () => composed.Require(wrong), () => composed.Has(wrong), () => composed.ProviderOf(wrong) })
        Throws(operation, "kernel capability \"same\" has type \"System.String\", not \"System.Int32\"");
    var missing = new CapabilityKey<string>("absent");
    Assert(!composed.Has(missing), "probe should not install a capability");
    Throws(() => composed.Has(new CapabilityKey<int>(missing.Id)), "kernel capability \"absent\" has type \"System.String\", not \"System.Int32\"");
});
Check("manifest is a detached immutable registration-order snapshot", () =>
{
    var kernel = new SrpgMicrokernel().Use(new("z", 1, _ => { }));
    var before = kernel.PluginManifest;
    var result = kernel.Compose();
    kernel.Use(new("a", 2, _ => { }));
    Assert(before.Count == 1 && result.PluginManifest.Count == 1, "manifest changed with builder");
    Assert(kernel.PluginManifest.Keys.SequenceEqual(new[] { "z", "a" }), "manifest globally sorted");
    var dictionary = (IDictionary<string, double>)result.PluginManifest;
    try { dictionary.Add("intruder", 4); throw new Exception("manifest allowed mutation"); }
    catch (NotSupportedException) { }
    Assert(!kernel.PluginManifest.ContainsKey("intruder"), "manifest mutation changed builder");
});
Check("plugin declarations snapshot caller arrays", () =>
{
    var key = new CapabilityKey<string>("third-party.rule");
    CapabilityKey[] declarations = [key];
    var plugin = new EnginePlugin("owner", 1, c => c.Provide(key, "value"), provides: declarations);
    declarations[0] = new CapabilityKey<int>("changed");
    var result = new SrpgMicrokernel().Use(plugin).Compose();
    Assert(result.Require(key) == "value", "external declaration array changed composition");
    try { ((IList<CapabilityKey>)plugin.Provides)[0] = declarations[0]; throw new Exception("plugin manifest allowed mutation"); }
    catch (NotSupportedException) { }
});
Check("anonymous composition cannot be recorded or saved", () =>
{
    var key = new CapabilityKey<string>("test.a");
    var result = new SrpgMicrokernel()
        .Use(new("owner", 1, c => c.Provide(key, "original"), provides: [key]))
        .Use(new("engine.override.test.a", 1, c => c.Replace(key, "changed"), overrides: [key])).Compose();
    var packs = Packs();
    var manifest = RulesetManifest.Create(packs, result.PluginManifest);
    const string error = "ruleset contains unversioned override \"engine.override.test.a\"; persist it through a named, versioned plugin";
    Throws(() => new BattleRecorder(manifest, new JsonObject()), error);
    Throws(() => new BattlePersistence(packs, manifest, new BattleSaveFeatures()).Save(new JsonObject()), error);
});
var versionFixture = EcmaJson.Parse(File.ReadAllText(args[1]))!.AsObject();
var versionPacks = EcmaJson.Parse(File.ReadAllText(Path.Combine(args[2], "godot/data/content-packs.json")))!.AsArray();
var features = new BattleSaveFeatures();
foreach (var pair in versionFixture["features"]!.AsObject()) features.Register(pair.Key, Strings(pair.Value));
foreach (var item in versionFixture["cases"]!.AsArray().OfType<JsonObject>())
{
    Check($"source numeric version save/load {item["version"]}", () =>
    {
        var version = item["version"]!.GetValue<double>();
        var composed = new SrpgMicrokernel().Use(new("test.versioned", version, _ => { })).Compose();
        var manifest = RulesetManifest.Create(versionPacks, composed.PluginManifest);
        Equal(item["manifest"]!, manifest);
        var persistence = new BattlePersistence(versionPacks, manifest, features);
        var save = persistence.Save(versionFixture["state"]!.AsObject(), versionFixture["savedAt"]!.GetValue<string>());
        Equal(item["savedRuleset"]!, save["ruleset"]!);
        var loaded = persistence.Load(save);
        Assert(StateDigest.Hash(loaded) == item["loadedStateHash"]!.GetValue<string>(), "wide version changed loaded state");
        var changed = RulesetManifest.Create(versionPacks, new Dictionary<string, double> { ["test.versioned"] = version / 2 });
        Equal(item["differences"]!, new JsonArray(RulesetManifest.Differences(manifest, changed).Select(value => (JsonNode)JsonValue.Create(value)!).ToArray()));
    });
}
foreach (var item in versionFixture["errors"]!.AsArray().OfType<JsonObject>())
{
    Check($"source malformed stored version {item["version"]}", () =>
    {
        var goodManifest = versionFixture["cases"]![0]!["manifest"]!.AsObject();
        var persistence = new BattlePersistence(versionPacks, goodManifest, features);
        var save = persistence.Save(versionFixture["state"]!.AsObject(), versionFixture["savedAt"]!.GetValue<string>());
        save["ruleset"]!["plugins"]!["test.versioned"] = Version(item["version"]);
        try { persistence.Load(save); throw new Exception("invalid stored version accepted"); }
        catch (StoredDocumentException error) { Assert(error.Message == item["error"]!.GetValue<string>(), $"Wrong stored-version error: {error.Message}"); }
    });
}
foreach (var item in versionFixture["idCases"]!.AsArray().OfType<JsonObject>())
{
    var id = EcmaJson.ReadString(item["id"]);
    Check($"source ID trim and stored admission {CodeUnits(id)}", () =>
    {
        Assert(EcmaText.IsBlank(id) == item["blank"]!.GetValue<bool>(), "ECMAScript blank classification mismatch");
        var kernel = new SrpgMicrokernel();
        if (item["registrationError"] is not null)
            Throws(() => kernel.Use(new(id, 1, _ => { })), item["registrationError"]!.GetValue<string>());
        else
        {
            kernel.Use(new(id, 1, _ => { }));
            Equal(item["manifest"]!, RulesetManifest.Create(versionPacks, kernel.Compose().PluginManifest));
        }
        if (item["blank"]!.GetValue<bool>())
            Throws(() => RulesetManifest.Create(versionPacks, new Dictionary<string, double> { [id] = 1 }),
                "plugin identity requires a nonempty id and positive integer version");
        var manifest = item["manifest"]!.AsObject();
        var persistence = new BattlePersistence(versionPacks, manifest, features);
        var initialPersistence = new BattlePersistence(versionPacks, versionFixture["cases"]![0]!["manifest"]!.AsObject(), features);
        var save = initialPersistence.Save(versionFixture["state"]!.AsObject(), versionFixture["savedAt"]!.GetValue<string>());
        save["ruleset"] = manifest.DeepClone();
        if (item["error"] is null)
            Assert(StateDigest.Hash(persistence.Load(save)) == item["loadedStateHash"]!.GetValue<string>(), "ID changed stored state");
        else
        {
            try { persistence.Load(save); throw new Exception("invalid stored ID accepted"); }
            catch (StoredDocumentException error)
            { Assert(error.Message == EcmaJson.ReadString(item["error"]), "Wrong stored-ID error"); }
        }
    });
}
foreach (var item in versionFixture["boundaryCases"]!.AsArray().OfType<JsonObject>())
{
    var id = EcmaJson.ReadString(item["id"]);
    Check($"source content pack and campaign level blank admission {CodeUnits(id)}", () =>
    {
        string? packError = null;
        try { RulesetManifest.Create([new JsonObject { ["id"] = id, ["version"] = 1 }], new Dictionary<string, double>()); }
        catch (DomainInvariantException error) { packError = error.Message; }
        Assert(packError == item["packError"]?.GetValue<string>(), "content pack whitespace admission differs");
        var definition = item["definition"]!.AsObject();
        string? battleError = null;
        try { CampaignNodeRegistry.Core().Validate(new CampaignNodeInspection(definition), definition["nodes"]![0]!.AsObject()); }
        catch (CampaignInvariantException error) { battleError = error.Message; }
        Assert(battleError == item["battleError"]?.GetValue<string>(), "campaign level whitespace admission differs");
    });
}
Check("double manifest creation refuses invalid plugin versions", () =>
{
    foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, 1.5, 0, -1 })
        Throws(() => RulesetManifest.Create(Packs(), new Dictionary<string, double> { ["invalid"] = value }),
            "plugin identity requires a nonempty id and positive integer version");
});
Check("existing default manifest and hash unchanged", () =>
{
    var actual = new GrayFlagEngine(versionPacks).RulesetManifest;
    Equal(versionFixture["defaultManifest"]!, actual);
    Assert(StateDigest.Hash(versionFixture["defaultManifest"]!) == StateDigest.Hash(actual), "default manifest hash changed");
});
Console.WriteLine($"Kernel contracts: {passed} passed, {failed} failed.");
if (failed != 0) throw new Exception($"Kernel contract failures: {failed}");

void Check(string name, Action run)
{
    try { run(); passed++; }
    catch (Exception error) { failed++; Console.Error.WriteLine($"FAIL {name}: {error}"); }
}
static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
static void Throws(Action action, string message)
{
    try { action(); }
    catch (DomainInvariantException error)
    {
        Assert(error.Message == message, $"Unexpected invariant error: {error.Message}");
        return;
    }
    throw new Exception("Expected DomainInvariantException");
}
static void Equal(JsonNode expected, JsonNode actual)
{
    if (!EcmaJson.DeepEquals(expected, actual))
        throw new Exception($"Expected {expected.ToJsonString()}\nActual {actual.ToJsonString()}");
}
static double Version(JsonNode? value)
{
    if (value is null) return 1;
    if (value is JsonValue scalar && scalar.TryGetValue<string>(out var special))
        return special switch { "NaN" => double.NaN, "Infinity" => double.PositiveInfinity, "-Infinity" => double.NegativeInfinity, _ => throw new Exception("Unknown version encoding") };
    return value.GetValue<double>();
}
static string CodeUnits(string value) => value.Length == 0 ? "(empty)" : string.Join(" ", value.Select(unit => $"U+{(int)unit:X4}"));
static CapabilityKey<string?> Key(string id) => new(id);
static IEnumerable<string> Strings(JsonNode? values) => values?.AsArray().Select(value => value!.GetValue<string>()) ?? [];
static IEnumerable<CapabilityKey> Keys(JsonNode? values) => Strings(values).Select(Key);
static JsonArray Manifest(IReadOnlyDictionary<string, double> manifest) => new(manifest.Select(pair => (JsonNode)new JsonArray(pair.Key, pair.Value)).ToArray());
static JsonObject Run(JsonObject input)
{
    if (input["kind"]!.GetValue<string>() == "dependency") return Order(input);
    var trace = new JsonArray();
    try
    {
        var kernel = new SrpgMicrokernel();
        foreach (var spec in input["plugins"]!.AsArray().OfType<JsonObject>())
        {
            var id = spec["id"]!.GetValue<string>();
            kernel.Use(new EnginePlugin(id, Version(spec["version"]), context =>
            {
                trace.Add(new JsonArray("install", id));
                foreach (var operation in spec["ops"]?.AsArray().OfType<JsonObject>() ?? [])
                {
                    var op = operation["op"]!.GetValue<string>();
                    if (op == "manifest") { trace.Add(new JsonArray("manifest", Manifest(context.PluginManifest))); continue; }
                    if (op == "fail") throw new Exception(operation["value"]!.GetValue<string>());
                    var key = Key(operation["key"]!.GetValue<string>());
                    var value = operation["value"]?.GetValue<string>();
                    switch (op)
                    {
                        case "provide": context.Provide(key, value); break;
                        case "replace": context.Replace(key, value); break;
                        case "require": trace.Add(new JsonArray("require", key.Id, context.Require(key))); break;
                        case "has": trace.Add(new JsonArray("has", key.Id, context.Has(key))); break;
                        case "provider": trace.Add(new JsonArray("provider", key.Id, context.ProviderOf(key))); break;
                        default: throw new Exception($"Unknown harness operation: {op}");
                    }
                }
            }, provides: Keys(spec["provides"]), overrides: Keys(spec["overrides"]),
                requiresCapabilities: Keys(spec["requiresCapabilities"]), requires: Strings(spec["requires"])));
        }
        var result = kernel.Compose();
        return new JsonObject
        {
            ["trace"] = trace,
            ["manifest"] = Manifest(result.PluginManifest),
            ["queries"] = new JsonArray(Strings(input["queries"]).Select(id =>
            {
                var key = Key(id);
                return (JsonNode)new JsonObject { ["id"] = id, ["has"] = result.Has(key), ["provider"] = result.ProviderOf(key), ["value"] = result.Has(key) ? result.Require(key) : null };
            }).ToArray()),
        };
    }
    catch (Exception error) { return new JsonObject { ["trace"] = trace, ["error"] = error.Message }; }
}
static JsonObject Order(JsonObject input)
{
    var nodes = input["nodes"]!.AsArray().OfType<JsonObject>().ToArray();
    var before = input["nodes"]!.ToJsonString();
    try
    {
        var result = DependencyOrder.Order(nodes, new DependencyOrderOptions<JsonObject>
        {
            IdOf = node => node["id"]!.GetValue<string>(),
            DependenciesOf = node => Strings(node["requires"]).ToArray(),
            IsSatisfiedExternally = id => Strings(input["external"]).Contains(id, StringComparer.Ordinal),
            Duplicate = id => new Exception($"duplicate dependency node: \"{id}\""),
            Missing = (node, id) => new Exception($"{node["id"]!.GetValue<string>()} misses {id}"),
            Cycle = path => new Exception($"cycle: {string.Join(" -> ", path)}"),
        });
        return new JsonObject { ["order"] = new JsonArray(result.Select(node => node["id"]!.DeepClone()).ToArray()), ["inputUnchanged"] = before == input["nodes"]!.ToJsonString() };
    }
    catch (Exception error) { return new JsonObject { ["error"] = error.Message, ["inputUnchanged"] = before == input["nodes"]!.ToJsonString() }; }
}
static JsonObject Lifecycle()
{
    var calls = 0;
    var contexts = new List<KernelPluginContext>();
    var a = Key("test.a");
    var b = Key("test.b");
    var kernel = new SrpgMicrokernel().Use(new("base", 2, context =>
    {
        calls++;
        contexts.Add(context);
        context.Provide(a, $"value-{calls}");
        if (calls == 1) throw new Exception("first install fails");
    }, provides: [a]));
    var before = Manifest(kernel.PluginManifest);
    var failure = "";
    try { kernel.Compose(); } catch (Exception error) { failure = error.Message; }
    var first = kernel.Compose();
    var second = kernel.Compose();
    contexts[1].Replace(a, "late replacement");
    contexts[1].Provide(b, "late addition");
    kernel.Use(new("later", 4, _ => { }));
    var latest = kernel.Compose();
    return new JsonObject
    {
        ["failure"] = failure, ["calls"] = calls, ["failedScopeValue"] = contexts[0].Require(a),
        ["first"] = new JsonArray(first.Require(a), first.ProviderOf(a), first.Require(b), first.ProviderOf(b)),
        ["second"] = new JsonArray(second.Require(a), second.Has(b)),
        ["before"] = before, ["firstManifest"] = Manifest(first.PluginManifest), ["builderManifest"] = Manifest(kernel.PluginManifest),
        ["latestManifest"] = Manifest(latest.PluginManifest),
        ["scopeManifests"] = new JsonArray(contexts.Select(context => (JsonNode)Manifest(context.PluginManifest)).ToArray()),
    };
}
static JsonArray Packs() => [new JsonObject { ["id"] = "z", ["version"] = 2 }, new JsonObject { ["id"] = "a", ["version"] = 1 }];
static JsonObject Persistence()
{
    var kernel = new SrpgMicrokernel().Use(new("z.plugin", 3, _ => { })).Use(new("a.plugin", 2, _ => { }));
    var manifest = RulesetManifest.Create(Packs(), kernel.Compose().PluginManifest);
    var different = RulesetManifest.Create(Packs(), new Dictionary<string, int> { ["z.plugin"] = 4, ["new.plugin"] = 1 });
    var errors = new JsonArray();
    const string anonymousId = "engine.override.test.a";
    foreach (var value in new[] { RulesetManifest.Create([], new Dictionary<string, int>()), RulesetManifest.Create(Packs(), new Dictionary<string, int> { [anonymousId] = 1 }), manifest })
    {
        try { RulesetManifest.RequirePersistent(value); errors.Add("accepted"); }
        catch (DomainInvariantException error) { errors.Add(error.Message); }
    }
    return new JsonObject
    {
        ["manifest"] = manifest,
        ["differences"] = new JsonArray(RulesetManifest.Differences(manifest, different).Select(value => (JsonNode)JsonValue.Create(value)!).ToArray()),
        ["errors"] = errors,
        ["anonymousId"] = anonymousId,
    };
}
