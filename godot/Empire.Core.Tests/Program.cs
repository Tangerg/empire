using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
using Empire.Core.Engine;
using Empire.Core.State;
using Empire.Core.Transactions;
var root = Path.GetFullPath(args.Length > 0 ? args[0] : "godot/tests/oracle");
var passed = 0; var failed = 0;
void Check(string name, Action test) { try { test(); passed++; Console.WriteLine($"PASS {name}"); } catch(Exception e) { failed++; Console.Error.WriteLine($"FAIL {name}: {e}"); } }
void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, received {actual}"); }
void Throws<T>(Action action) where T : Exception { try { action(); } catch(T) { return; } throw new Exception($"Expected {typeof(T).Name}"); }
JsonNode Read(string path) => JsonNode.Parse(File.ReadAllText(Path.Combine(root, path)))!;
Check("oracle state digests", () => { foreach(var a in new[]{"0.35","0.58","0.8"}) { var f=Read($"grayflag-{a}.json"); Equal(f["replay"]!["initialStateHash"]!.GetValue<string>(), StateDigest.Hash(f["initialState"]!)); Equal(f["replay"]!["finalStateHash"]!.GetValue<string>(),StateDigest.Hash(f["finalSave"]!["state"]!)); } });
Check("UTF-16 and numeric digest vectors", () => { foreach(var v in Read("digest-vectors.json").AsArray()) Equal(v!["hash"]!.GetValue<string>(),StateDigest.Hash(v["value"]!)); });
Check("independent RNG streams", () => {
 var rng = new SplitMixRandom();
 foreach(var v in Read("random-vectors.json").AsArray()) { var r=RandomState.Create(v!["seed"]!.GetValue<uint>()); var stream=v["stream"]!.GetValue<string>(); foreach(var n in v["draws"]!.AsArray()) { Equal(n!.GetValue<double>(),rng.Peek(r,stream));Equal(n.GetValue<double>(),rng.Next(r,stream)); } }
});
Check("initial state factory", () => {
 var f=Read("grayflag-initial-raw.json");var packs=JsonNode.Parse(File.ReadAllText(Path.Combine(root,"../../data/content-packs.json")))!.AsArray();
 var engine = new GrayFlagEngine(packs);
 var actual=BattleStateFactory.Create(new BattleStateRules(engine.Catalog,engine.Grids,engine.Reactions),f["level"]!.AsObject());
 Equal(StateDigest.Hash(f["state"]!),StateDigest.Hash(actual));
});
Check("three tactical grids", () => {
 var grids=new Dictionary<string,ITacticalGrid>{{"square4",new Square4Grid()},{"square8",new Square8Grid()},{"hex",new OddRowHexGrid()}};
 Coord Point(JsonNode n)=>new(n["x"]!.GetValue<int>(),n["y"]!.GetValue<int>());
 void Points(JsonNode expected,IReadOnlyList<Coord> actual) { var a=expected.AsArray();Equal(a.Count,actual.Count);for(var i=0;i<a.Count;i++)Equal(Point(a[i]!),actual[i]); }
 foreach(var v in Read("geometry-vectors.json").AsArray()) { var grid=grids[v!["id"]!.GetValue<string>()];var at=Point(v["at"]!);var to=Point(v["to"]!);Equal(v["distance"]!.GetValue<int>(),grid.Distance(at,to));Equal(v["toward"]!.GetValue<string>(),grid.Toward(at,to));Points(v["adjacent"]!,grid.Adjacent(at));Points(v["within"]!,grid.Within(at,1,2));Points(v["line"]!,grid.Line(at,to));var c=grid.Center(at);Equal(v["center"]!["x"]!.GetValue<double>(),c.X);Equal(v["center"]!["y"]!.GetValue<double>(),c.Y);Equal(at,grid.CellAt(c)); }
});
Check("tile art floating products",()=>{Equal(0.19186,TileArtVariation.Hash(1,1));Equal(0.67636,TileArtVariation.Hash(-1,-1));Equal(0.39852,TileArtVariation.Hash(7,9,13));Equal(0.22755,TileArtVariation.Hash(2147483647,-2147483648,17));Equal(0.83593,TileArtVariation.Hash(0.5,-0.5,2));});
Check("transaction rollback",()=>{
 var r=new BattleActionRegistry();r.Register("fail",(s,_,emit)=>{s["x"]=2;emit(new JsonObject{["type"]="partial"});throw new IllegalActionException(new ActionRefusal("test.refused"));});var b=new Battle(new JsonObject{["x"]=1},r);var before=b.StateHash;Throws<IllegalActionException>(()=>b.Dispatch(new JsonObject{["kind"]="fail"}));Equal(before,b.StateHash);
});
Check("detached committed state and events",()=>{
 JsonObject? retained=null;var emitted=new JsonObject{["n"]=2};var r=new BattleActionRegistry();r.Register("go",(s,a,emit)=>{retained=s;s["n"]=2;a["kind"]="changed";emit(emitted);});var b=new Battle(new JsonObject{["n"]=1},r);var action=new JsonObject{["kind"]="go"};var receipt=b.Dispatch(action);retained!["n"]=3;emitted["n"]=4;receipt.Before["n"]=5;var snapshot=b.Snapshot();snapshot["n"]=6;Equal(2,b.Snapshot()["n"]!.GetValue<int>());Equal(2,receipt.Events[0]["n"]!.GetValue<int>());Equal("go",action["kind"]!.GetValue<string>());
});
Check("six TS tactic action snapshots", () => SupportChecks.Tactics(root));
Check("resource clamping and atomic costs", SupportChecks.Resources);
foreach (var aggression in new[] { "0.35", "0.58", "0.8" })
    Check($"full native replay {aggression}", () => ReplayChecks.Run(root, aggression));
Check("level normalization and canonical spawn", () => StateContractChecks.Run(root));
Console.WriteLine($"{passed} passed; {failed} failed");return failed==0?0:1;
