using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core;
using Empire.Core.Determinism;
using Empire.Core.Transactions;

var oracle = JsonNode.Parse(File.ReadAllText(args[0]))!.AsObject();
var cases = oracle["cases"]!.AsArray();
var sampleCount = 0;
foreach (var fixture in cases.OfType<JsonObject>())
{
    var pacer = new AiTurnPacer();
    var selected = 0;
    var committed = 0;
    var computer = true;
    var busy = false;
    var thrown = false;
    var actions = fixture["actions"]!.AsArray().OfType<JsonObject>().ToArray();
    var attempts = new List<string>();
    var logs = new List<string>();
    var settlement = fixture["settlement"]!.GetValue<double>();
    var registry = new BattleActionRegistry();
    foreach (var sourceAction in actions)
    {
        var definition = sourceAction;
        registry.Register(definition["kind"]!.GetValue<string>(), (_, _, _) => {
            if (definition["invariant"]?.GetValue<bool>() == true) throw new DomainInvariantException("broken rule");
            if (definition["refuse"]?.GetValue<bool>() == true) throw new IllegalActionException(new ActionRefusal("test.refused"));
            committed++;
            if (definition["human"]?.GetValue<bool>() == true || definition["terminal"]?.GetValue<bool>() == true) computer = false;
        }, definition["handsOff"]?.GetValue<bool>() ?? false);
    }
    registry.Register("endTurn", (_, _, _) => {
        if (fixture["fallbackRefuses"]!.GetValue<bool>()) throw new IllegalActionException(new ActionRefusal("test.refused"));
        committed++; computer = false;
    }, handsOffTurn: true);
    var battle = new Battle(new JsonObject(), registry);
    BattleReceipt Dispatch(JsonObject action) { attempts.Add(action["kind"]!.GetValue<string>()); return battle.Dispatch(action); }
    foreach (var sample in fixture["samples"]!.AsArray().OfType<JsonObject>())
    {
        var now = sample["at"]!.GetValue<double>();
        try {
            pacer.Tick(now, computer, selected > 0 && now < settlement,
                () => ComputerActionExecution.Execute(actions[selected++], Dispatch, registry.HandsOffTurn, refusal => { Require(refusal.Code == "test.refused" && !refusal.Details.EnumerateObject().Any(), "AI refusal facts changed"); logs.Add(refusal.Code); }),
                () => now < settlement, value => busy = value);
        }
        catch (DomainInvariantException) { thrown = true; }
        var actual = new JsonObject {
            ["at"] = now, ["committed"] = committed, ["busy"] = busy,
            ["attempts"] = new JsonArray(attempts.Select(value => (JsonNode)JsonValue.Create(value)!).ToArray()),
            ["logs"] = new JsonArray(logs.Select(value => (JsonNode)JsonValue.Create(value)!).ToArray()), ["thrown"] = thrown,
        };
        var expected = sample.DeepClone().AsObject();
        expected["logs"] = new JsonArray(sample["logs"]!.AsArray().Select(_ => (JsonNode?)JsonValue.Create("test.refused")).ToArray());
        Require(EcmaJson.DeepEquals(actual, expected), $"{fixture["name"]} at {now}: {actual} != {sample}");
        sampleCount++;
    }
}

// No repeat start or commit while a previous human/AI animation remains unsettled.
var pacing = new AiTurnPacer();
var calls = 0;
var isBusy = false;
ComputerStepResult Advance() { calls++; return new(true, false); }
pacing.Tick(0, true, true, Advance, () => false, value => isBusy = value);
Require(calls == 0 && !isBusy, "AI began during an existing animation");
pacing.Tick(1, true, false, Advance, () => true, value => isBusy = value);
pacing.Tick(2000, true, true, Advance, () => true, value => isBusy = value);
Require(calls == 1 && isBusy, "AI committed during settlement");
pacing.Tick(2001, true, false, Advance, () => false, value => isBusy = value);
pacing.Tick(2090, true, false, Advance, () => false, value => isBusy = value);
Require(calls == 1 && isBusy, "89ms pause ended early");
pacing.Tick(2091, true, false, Advance, () => false, value => isBusy = value);
Require(calls == 2, "90ms pause did not end");

Require(oracle["cancelCalls"]!.GetValue<int>() == 1, "source disposal contract changed");

// Binding replacement/unmount drops the pending deadline; a new binding starts immediately.
pacing.Cancel();
Require(!pacing.IsRunning, "cancel retained running state");
pacing.Tick(9999, false, false, Advance, () => false, _ => { });
Require(calls == 2, "cancelled binding committed an action");
pacing.Tick(10000, true, false, Advance, () => false, _ => { });
Require(calls == 3, "new binding inherited old pre-delay");
pacing.Cancel();
pacing.Tick(10001, false, false, Advance, () => false, _ => { });
Require(calls == 3, "unmounted binding committed an action");

// The source guard stops after 2000 attempted actions, not 2001, without frame-driven restart.
var guarded = new AiTurnPacer();
var guardedCalls = 0;
for (var i = 0; i <= 2001; i++)
    guarded.Tick(i * 90, true, false, () => { guardedCalls++; return new(false, false); }, () => false, _ => { });
Require(guardedCalls == oracle["guardCalls"]!.GetValue<int>() && !guarded.IsRunning, "2000-action guard did not terminate loop");

// Invariants from the fallback are not swallowed either.
var failureRegistry = new BattleActionRegistry();
failureRegistry.Register("bad", (_, _, _) => throw new IllegalActionException(new ActionRefusal("test.refused")));
failureRegistry.Register("endTurn", (_, _, _) => throw new DomainInvariantException("fallback defect"));
var failureBattle = new Battle(new JsonObject(), failureRegistry);
var propagated = false;
try { ComputerActionExecution.Execute(new JsonObject { ["kind"] = "bad" }, failureBattle.Dispatch, failureRegistry.HandsOffTurn, _ => { }); }
catch (DomainInvariantException) { propagated = true; }
Require(propagated, "fallback invariant swallowed");
Console.WriteLine($"PASS {cases.Count} actual source AI loop cases / {sampleCount} fake-clock observations; cancellation, settlement, guard and fallback invariants");
static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
