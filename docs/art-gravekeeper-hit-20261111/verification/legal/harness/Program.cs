using System.Globalization;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Tactics;
using Empire.Localization;
using Empire.Presentation;
using Empire.Presentation.Tactical;

if (args.Length != 3)
    throw new ArgumentException("Usage: Probe <repository-root> <evidence-directory> <output-directory>");
var repository = Path.GetFullPath(args[0]);
var evidence = Path.GetFullPath(args[1]);
var output = Path.GetFullPath(args[2]);
var root = Path.Combine(repository, "godot");
var previous = Path.Combine(repository, "docs/art-gravekeeper-idle-20261110/verification/legal/source-package/heal-right");
Require(output != repository && !output.StartsWith(repository + Path.DirectorySeparatorChar, StringComparison.Ordinal), "Output must be outside the repository.");
Require(!File.Exists(Path.Combine(output, "verification-result.json")), "Refusing to overwrite an existing replay.");
var engine = new BattleEngine(Read(Path.Combine(root, "data/content-packs.json")).AsArray());
var definition = Read(Path.Combine(root, "data/grayflag-campaign.json")).AsObject();
var levels = Read(Path.Combine(root, "data/grayflag-campaign-levels.json")).AsArray();
var lookup = Read(Path.Combine(output, "translation-lookup.json")).AsObject();
var messages = new GameText(key => lookup[key]?.GetValue<string>(), CultureInfo.GetCultureInfo("zh-CN"));
var identities = Read(Path.Combine(root, "data/grayflag-story.json"))["identityNameKeys"]!.AsObject().ToDictionary(entry => entry.Key, entry => entry.Value!.GetValue<string>());
var presentation = SessionPresentationText.Compose(new(engine, messages, identities), levels,
    File.ReadAllText(Path.Combine(root, "data/grayflag-battle-text.json")), File.ReadAllText(Path.Combine(root, "data/grayflag-content-text.json")));
var rules = new CampaignApplicationRules(engine, presentation);
var source = new GrayFlagCampaign(rules, definition, levels);
var opening = new JsonArray();
while (source.Node()["type"]!.GetValue<string>() == "story")
{
    opening.Add(new JsonObject { ["kind"] = "advance", ["node"] = source.Node()["id"]!.DeepClone() });
    source.Advance();
}
opening.Add(new JsonObject { ["kind"] = "choose", ["choice"] = "steady-advance" });
source.Choose("steady-advance");
source.BeginBattle();
Require(EcmaJson.DeepEquals(opening, Read(Path.Combine(previous, "opening-actions.json"))), "Campaign opening changed.");
CompareSave(source.Save(), Read(Path.Combine(previous, "source-campaign-save.json")).AsObject(), "Campaign source");
Write(output, "opening-actions.json", opening);
Write(output, "source-campaign-save.json", source.Save());
var preserved = Read(Path.Combine(previous, "derivation-actions.json")).AsArray();
var prefix = new JsonArray();
for (var step = 0; step < 164; step++)
{
    var expected = preserved[step]!.AsObject();
    var actual = Commit(source, prefix, expected["computer"]!.GetValue<bool>() ? null : expected["action"]!.AsObject());
    Require(EcmaJson.DeepEquals(actual, expected), $"Preserved receipt changed at step {step}.");
}
Write(output, "shared-prefix-actions.json", prefix);
var checkpoint = source.Save();
var summaries = new JsonArray();
foreach (var name in new[] { "right", "left" })
{
    var current = new GrayFlagCampaign(rules, definition, levels);
    current.Load(checkpoint);
    var history = (JsonArray)prefix.DeepClone();
    var directory = Path.Combine(output, name);
    Directory.CreateDirectory(directory);
    Move(current, history, 3, name == "left" ? 22 : 21, name == "left" ? 18 : 19);
    Move(current, history, 5, 21, 21);
    Move(current, history, 10, 27, 18);
    Move(current, history, 8, 24, 18);
    Move(current, history, 4, 23, 18, "heal", name == "left" ? 22 : 24, 18);
    for (var step = 169; step <= 171; step++)
        Commit(current, history, preserved[step]!["action"]!.AsObject());
    var trigger = current.Save();
    var controller = new TacticalSelectionController(current.Battle!.GetTacticalQueries(0));
    var endTurn = new JsonObject { ["kind"] = "endTurn" };
    var input = controller.Key(controller.Stamp, "e");
    Require(input.Status == TacticalInputStatus.Ready && input.Request?.Action is { } emitted
        && EcmaJson.DeepEquals(EcmaJson.Parse(emitted.GetRawText()), endTurn), "Native E did not emit canonical endTurn.");
    Commit(current, history, endTurn);
    var before = current.Save();
    var receipt = Commit(current, history, null);
    var after = current.Save();
    var transition = current.Battle!.GetSnapshot().Transition;
    Require(transition is not null && transition.IsComputerAction, "Missing native AI presentation transition.");
    var focus = BattlefieldFocus.CommandDestination(transition!, point => point);
    Require(focus == new CameraPoint(24, 20), "Normal AI camera follow must focus the attack destination.");
    CompareSave(current.Save(), after, "Camera query must not change the official save");
    var targetBefore = TacticalQueries.RequireUnit(before["battle"]!["state"]!.AsObject(), 4);
    var targetAfter = TacticalQueries.RequireUnit(after["battle"]!["state"]!.AsObject(), 4);
    var hit = receipt["events"]!.AsArray().OfType<JsonObject>().Single(e => e["type"]?.GetValue<string>() == "attack" && e["defender"]?.GetValue<int>() == 4);
    Require(targetBefore["type"]!.GetValue<string>() == "c01.gravekeeper" && targetAfter["hp"]!.GetValue<double>() > 0 && hit["damage"]!.GetValue<double>() > 0 && !hit["killed"]!.GetValue<bool>(), "No surviving real gravekeeper hit.");
    Require(targetBefore["facing"]!.GetValue<string>() == (name == "right" ? "east" : "west"), "Keeper facing differs.");
    Require(targetBefore["x"]!.GetValue<int>() == 23 && targetBefore["y"]!.GetValue<int>() == 18, "Keeper moved off clear-ground cell.");
    foreach (var cell in new[] { (23, 19), (23, 20), (24, 19) })
        Require(!before["battle"]!["state"]!["units"]!.AsArray().OfType<JsonObject>().Any(u => u["x"]!.GetValue<int>() == cell.Item1 && u["y"]!.GetValue<int>() == cell.Item2), "Foreground occupied.");
    var beforeState = before["battle"]!["state"]!.AsObject();
    var archerBefore = TacticalQueries.RequireUnit(beforeState, 18);
    Require(engine.Space.IsUnitVisible(beforeState, 1, targetBefore) && engine.Space.IsUnitVisible(beforeState, 1, archerBefore), "Keeper and archer must be player-visible.");
    var map = beforeState["map"]!.AsObject();
    var cellIndex = 18 * map["width"]!.GetValue<int>() + 23;
    Require(map["tiles"]![cellIndex]!.GetValue<string>() == "plain" && map["elevation"]![cellIndex]!.GetValue<int>() == 0, "Keeper must stand on the known southern plain.");
    Write(directory, "before.json", before);
    Write(directory, "after.json", after);
    Write(directory, "receipt.json", receipt);
    Write(directory, "derivation-actions.json", history);
    Write(directory, "ui-trigger-before.json", trigger);
    Write(directory, "ui-trigger-action.json", endTurn);
    var extra = new JsonArray();
    while (current.Battle!.IsComputerTurn)
    {
        Require(extra.Count < 10, "Computer did not settle.");
        extra.Add(Commit(current, history, null).DeepClone());
    }
    Require(extra.Count == 1 && extra[0]!["action"]!["kind"]!.GetValue<string>() == "endTurn", "Expected only a normal endTurn after hit.");
    var settled = current.Save();
    var settledState = current.Battle.Snapshot();
    Require(!current.Battle.IsConcluded && settledState["turn"]!.GetValue<int>() == 11
        && settledState["currentPlayer"]!.GetValue<int>() == 1
        && TacticalQueries.RequireUnit(settledState, 4)["hp"]!.GetValue<double>() == 4, "Normal AI must settle at the next living-keeper human turn.");
    Write(directory, "ui-settled.json", settled);
    Write(directory, "ui-settled-extra-actions.json", extra);
    Write(directory, "ui-settled-derivation-actions.json", history);
    foreach (var save in new[] { before, after, trigger, settled })
        VerifyRoundtrip(save);
    var replay = new GrayFlagCampaign(rules, definition, levels);
    replay.Load(before);
    var replayTrace = new JsonArray();
    var replayHit = Commit(replay, replayTrace, null, receipt["step"]!.GetValue<int>());
    Require(EcmaJson.DeepEquals(replayHit, receipt), "Reloaded hit receipt changed.");
    CompareSave(replay.Save(), after, "Reloaded immediate after");
    var replayEnd = Commit(replay, replayTrace, null, extra[0]!["step"]!.GetValue<int>());
    Require(EcmaJson.DeepEquals(replayEnd, extra[0]), "Reloaded settlement receipt changed.");
    CompareSave(replay.Save(), settled, "Reloaded settled");
    var triggerReplay = new GrayFlagCampaign(rules, definition, levels);
    triggerReplay.Load(trigger);
    var triggerHistory = new JsonArray();
    var triggerReceipt = Commit(triggerReplay, triggerHistory, endTurn, receipt["step"]!.GetValue<int>() - 1);
    Require(EcmaJson.DeepEquals(triggerReceipt, history[receipt["step"]!.GetValue<int>() - 1]), "Reloaded native trigger changed.");
    CompareSave(triggerReplay.Save(), before, "Reloaded native trigger before");
    var manifest = new JsonObject
    {
        ["passed"] = true,
        ["name"] = name,
        ["originalCampaignStart"] = true,
        ["preservedPrefixActions"] = 164,
        ["fullRouteActions"] = history.Count,
        ["independentCoreStateAndEvents"] = true,
        ["officialSaveRoundtrip"] = true,
        ["reloadedNextActionAndSettlementEquality"] = true,
        ["nativeEndTurnControllerEquality"] = true,
        ["beforeHash"] = receipt["beforeHash"]!.DeepClone(),
        ["afterHash"] = receipt["afterHash"]!.DeepClone(),
        ["settledHash"] = StateDigest.Hash(current.Battle.Snapshot()),
        ["eventsSha256"] = EcmaJson.Sha256(receipt["events"]),
        ["keeperBefore"] = targetBefore.DeepClone(),
        ["keeperAfter"] = targetAfter.DeepClone(),
        ["hit"] = hit.DeepClone(),
        ["foregroundCellsUnoccupied"] = true,
        ["playerVisible"] = true,
        ["normalAiActionsFromNativeBefore"] = 2,
        ["nativeCameraFocusQueryVerified"] = true,
        ["cameraDestinationCell"] = new JsonObject { ["x"] = focus!.Value.X, ["y"] = focus.Value.Y },
        ["nativePixelAcceptance"] = "Not run; final-build CUA required."
    };
    Write(directory, "verification-result.json", manifest);
    if (Directory.Exists(Path.Combine(evidence, name)))
    {
        foreach (var file in new[] { "before.json", "after.json", "ui-trigger-before.json", "ui-settled.json" })
            CompareSave(Read(Path.Combine(directory, file)).AsObject(), Read(Path.Combine(evidence, name, file)).AsObject(), $"Preserved {name}/{file}");
        foreach (var file in new[] { "receipt.json", "derivation-actions.json", "ui-settled-extra-actions.json", "ui-settled-derivation-actions.json" })
            Require(EcmaJson.DeepEquals(Read(Path.Combine(directory, file)), Read(Path.Combine(evidence, name, file))), $"Preserved {name}/{file} changed.");
    }
    summaries.Add(manifest.DeepClone());
    Console.WriteLine($"PASS {name}: keeper {targetBefore["hp"]}→{targetAfter["hp"]}, {targetBefore["facing"]}; {history.Count} commands; before {receipt["beforeHash"]}, after {receipt["afterHash"]}, settled {StateDigest.Hash(current.Battle.Snapshot())}.");
}
Write(output, "verification-result.json", new JsonObject { ["passed"] = true, ["scenarios"] = summaries, ["noStatePatching"] = true, ["finalBuildAcceptance"] = "Only when rerun against final frozen new-hit assemblies." });

JsonObject Commit(GrayFlagCampaign current, JsonArray history, JsonObject? requested, int? step = null)
{
    var session = current.Battle!;
    Require(!session.IsConcluded, "Battle ended early.");
    var before = current.Save();
    var computer = session.IsComputerTurn;
    Require(computer == (requested is null), "Wrong controller for action.");
    var count = session.Events.Count;
    if (computer)
        Require(session.AdvanceComputerStep() is { Committed: true }, "Normal AI refused.");
    else
        Require(session.Dispatch(EcmaJson.ToElement(requested!), session.Revision).Accepted, "Human action refused: " + EcmaJson.Stringify(requested));
    var action = session.ActionLog.Last();
    if (requested is not null)
        Require(EcmaJson.DeepEquals(action, requested), "Canonical human action changed.");
    var core = engine.LoadSavedBattle(before["battle"]!);
    var independent = core.Dispatch(action);
    var events = new JsonArray(session.Events.Skip(count).Select(e => (JsonNode)e.DeepClone()).ToArray());
    Require(EcmaJson.DeepEquals(session.Snapshot(), core.Snapshot()), "Independent core full state changed.");
    Require(EcmaJson.DeepEquals(events, new JsonArray(independent.Events.Select(e => (JsonNode)e.DeepClone()).ToArray())), "Independent full events changed.");
    var receipt = new JsonObject { ["step"] = step ?? history.Count, ["computer"] = computer, ["action"] = action.DeepClone(), ["beforeHash"] = StateDigest.Hash(before["battle"]!["state"]!.AsObject()), ["afterHash"] = independent.StateHash, ["events"] = events };
    history.Add(receipt);
    return receipt;
}

void Move(GrayFlagCampaign current, JsonArray history, int unit, int x, int y, string ability = "wait", int? targetX = null, int? targetY = null)
{
    var state = current.Battle!.Snapshot();
    var actor = TacticalQueries.RequireUnit(state, unit);
    var field = engine.Space.ComputeMoveField(state, actor);
    var stops = field.Stops.Where(stop => { var at = engine.Space.Board(state).CoordOf(stop); return at.X == x && at.Y == y; }).ToArray();
    Require(stops.Length == 1, $"Unit{unit} cannot legally stop at ({x},{y}).");
    var stop = stops.Single();
    var at = engine.Space.Board(state).CoordOf(stop);
    var option = engine.CommandsAt(state, actor, at).Single(command => command.Ability == ability);
    var command = new JsonObject { ["ability"] = ability };
    if (targetX is { } tx && targetY is { } ty)
    {
        Require(option.Targets.Any(target => target.X == tx && target.Y == ty), $"Unit{unit} cannot legally {ability} ({tx},{ty}).");
        command["target"] = new JsonObject { ["x"] = tx, ["y"] = ty };
    }
    var action = new JsonObject { ["kind"] = "command", ["unit"] = unit, ["path"] = TacticalQueries.PathJson(field.TracePath(stop)!), ["command"] = command };
    Commit(current, history, action);
}

void VerifyRoundtrip(JsonObject save)
{
    var current = new GrayFlagCampaign(rules, definition, levels);
    current.Load(save);
    CompareSave(current.Save(), save, "Official roundtrip");
    Require(EcmaJson.DeepEquals(current.Battle!.Snapshot(), save["battle"]!["state"]), "Official state roundtrip changed.");
}
static void Write(string directory, string filename, JsonNode value) => SaveFile.WriteDocument(Path.Combine(directory, filename), value);
static JsonNode Read(string path) => EcmaJson.Parse(File.ReadAllText(path))!;
static void CompareSave(JsonObject actual, JsonObject expected, string label) => Require(EcmaJson.DeepEquals(WithoutTimestamps(actual), WithoutTimestamps(expected)), label + " differs beyond writer timestamps.");
static JsonObject WithoutTimestamps(JsonObject value)
{
    var result = (JsonObject)value.DeepClone();
    result["campaign"]!.AsObject().Remove("savedAt");
    result["battle"]!.AsObject().Remove("savedAt");
    return result;
}
static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
