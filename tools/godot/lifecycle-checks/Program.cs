using System.Text.Json.Nodes;
using System.IO.Compression;
using Empire.Core.Lifecycle;
using Empire.Core.Determinism;
var root = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.GetFullPath("godot");
var fixture = args.Length > 1 ? args[1] : "/tmp/empire-lifecycle-oracle.json";
var diagnostics = Path.GetDirectoryName(Path.GetFullPath(fixture))!;
var packs = JsonNode.Parse(File.ReadAllText(root + "/data/content-packs.json"))!.AsArray();
var casePath = root + "/tests/oracle/action-cases.json";
JsonArray cases;
if (File.Exists(casePath)) cases = JsonNode.Parse(File.ReadAllText(casePath))!.AsArray();
else { using var file = File.OpenRead(casePath + ".gz"); using var gzip = new GZipStream(file, CompressionMode.Decompress); cases = JsonNode.Parse(gzip)!.AsArray(); }
var passed = 0;
foreach (var item in cases)
{
    if (item!["action"]!["kind"]!.GetValue<string>() != "endTurn") continue;
    var state = (JsonObject)item["before"]!.DeepClone();
    var events = new JsonArray();
    void Emit(JsonObject e) { events.Add(e); var counts=state["scenario"]!["eventCounts"]!.AsObject(); var type=e["type"]!.GetValue<string>(); counts[type]=(counts[type]?.GetValue<int>() ?? 0)+1; }
    var lifecycle = new BattleLifecycle(state, packs, Emit);
    lifecycle.AdvanceTurn(); lifecycle.AfterAction();
    if (StateDigest.Hash(state) != StateDigest.Hash(item["after"]!))
    {
        File.WriteAllText(Path.Combine(diagnostics, "actual.json"),state.ToJsonString());
        File.WriteAllText(Path.Combine(diagnostics, "expected.json"),item["after"]!.ToJsonString());
        throw new Exception($"EndTurn state mismatch at case {passed}, player {item["before"]!["currentPlayer"]}, turn {item["before"]!["turn"]}");
    }
    if (StateDigest.Hash(events) != StateDigest.Hash(item["events"]!)) throw new Exception($"EndTurn event mismatch: {events} / {item["events"]}");
    passed++;
}
Console.WriteLine($"PASS {passed} exact source endTurn snapshots and event sequences");
var focused=JsonNode.Parse(File.ReadAllText(fixture))!.AsArray();
foreach(var item in focused)
{
    var state=(JsonObject)item!["before"]!.DeepClone();var events=new JsonArray();
    void Emit(JsonObject e) { events.Add(e);var counts=state["scenario"]!["eventCounts"]!.AsObject();var type=e["type"]!.GetValue<string>();counts[type]=(counts[type]?.GetValue<int>()??0)+1; }
    var life=new BattleLifecycle(state,packs,Emit);
    switch(item["operation"]!.GetValue<string>()){case "start":life.Start();break;case "advanceTurn":life.AdvanceTurn();break;case "afterAction":life.AfterAction();break;case "beginPlaying":life.BeginPlaying();break;}
    if(StateDigest.Hash(state)!=StateDigest.Hash(item["after"]!) || StateDigest.Hash(events)!=StateDigest.Hash(item["events"]!))
    { File.WriteAllText(Path.Combine(diagnostics, "actual.json"),state.ToJsonString());File.WriteAllText(Path.Combine(diagnostics, "expected.json"),item["after"]!.ToJsonString()); throw new Exception($"Focused mismatch {item["name"]}: events {events} / {item["events"]}"); }
}
Console.WriteLine($"PASS {focused.Count} exact source focused lifecycle snapshots and event sequences");
