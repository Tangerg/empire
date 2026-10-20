using System.Text.Json.Nodes;
using Empire.Core.Engine;
using Empire.Core.Tactics;
using Empire.Core.State;
var input=JsonNode.Parse(File.ReadAllText(args[0]))!;
var engine=new GrayFlagEngine(input["packs"]!.AsArray());var vision=new BattleVision(engine.Movement);var count=0;
foreach(var item in input["cases"]!.AsArray().OfType<JsonObject>()){
 var state=item["state"]!.AsObject();var viewer=item["viewer"]!.GetValue<int>();
 var tiles=new JsonArray(vision.VisibleTiles(state,viewer).Select(i=>(JsonNode)JsonValue.Create(i)!).ToArray());
 var units=new JsonArray(vision.VisibleUnits(state,viewer).Select(u=>u["id"]!.DeepClone()).ToArray());
 var individual=new JsonArray(state["units"]!.AsArray().OfType<JsonObject>().Select(u=>(JsonNode)JsonValue.Create(vision.IsUnitVisible(state,viewer,u))!).ToArray());
 if(!JsonNode.DeepEquals(tiles,item["tiles"])||!JsonNode.DeepEquals(units,item["units"])||!JsonNode.DeepEquals(individual,item["individual"]))throw new Exception($"Vision mismatch {count}");count++;
}
Console.WriteLine($"PASS {count} native/source fog queries across three grids, allies, cover and empty viewers");

var castCount=0;
foreach(var item in input["casts"]!.AsArray().OfType<JsonObject>()){
 var actual=new JsonArray(engine.Combat.IncomingCasts(item["state"]!.AsObject()).Select(pair=>(JsonNode)new JsonObject { ["x"]=pair.Key.X,["y"]=pair.Key.Y,["remaining"]=pair.Value }).ToArray());
 if(!JsonNode.DeepEquals(actual,item["incoming"]))throw new Exception($"Cast telegraph mismatch {castCount}: {actual} / {item["incoming"]}");castCount++;
}
Console.WriteLine($"PASS {castCount} source/native public cast projections with overlapping areas and lost casters");
