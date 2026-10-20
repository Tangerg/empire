using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Campaign;

internal static class CampaignValidationChecks
{
 internal static void CheckCases(string path)
 {
var cases = JsonNode.Parse(File.ReadAllText(path))!.AsArray();
var failures = new List<string>();
foreach (var item in cases)
{
    var test = item!.AsObject();
    var definition = test["definition"]!.AsObject();
    var state = test["state"]!.AsObject();
    JsonObject actual;
    try
    {
        JsonNode? value = null;
        switch(test["op"]!.GetValue<string>())
        {
            case "definition": CampaignValidation.ValidateDefinition(definition); break;
            case "state": CampaignValidation.ValidateState(definition, state); break;
            case "createState": value = CampaignValidation.CreateState(definition); break;
            case "createSave": value = CampaignSave.Create(definition, state, test["savedAt"]!.GetValue<string>()); break;
            case "loadSave": value = CampaignSave.Load(test["raw"], definition); break;
        }
        actual = new JsonObject { ["ok"] = true, ["value"] = value };
    }
    catch(Exception error)
    {
        string Map(Exception e) => e switch
        {
            CampaignInvariantException => "CampaignInvariantError",
            CampaignActionException => "CampaignActionError",
            StoredDocumentException => "StoredDocumentError",
            _ => e.GetType().Name,
        };
        actual = new JsonObject { ["ok"] = false, ["error"] = Map(error), ["message"] = error.Message,
            ["cause"] = error.InnerException is {} inner ? Map(inner) : null };
    }
    if (!JsonNode.DeepEquals(test["expected"], actual)) failures.Add($"{test["name"]}: expected {test["expected"]!.ToJsonString()} actual {actual.ToJsonString()}");
}
var d = cases[0]!["definition"]!.AsObject();
var s = CampaignValidation.CreateState(d);
var saved = CampaignSave.Create(d, s, "2026-01-01T00:00:00.000Z");
var loaded = CampaignSave.Load(saved, d);
s["roster"]!["hero"]!["resources"]!["mana"]!["current"] = 8;
loaded["state"]!["flags"]!.AsArray().Add("changed");
if (saved["state"]!["roster"]!["hero"]!["resources"]!["mana"]!["current"]!.GetValue<int>() != 2 || saved["state"]!["flags"]!.AsArray().Count != 2)
    failures.Add("nested clone isolation failed");
var handBuilt = new JsonObject { ["schema"] = 1, ["id"] = "typed", ["version"] = 1, ["start"] = "end", ["contentPacks"] = new JsonObject { ["test"] = 1 },
    ["nodes"] = new JsonArray(new JsonObject { ["id"] = "end" }) };
CampaignSave.Load(CampaignSave.Create(handBuilt, CampaignValidation.CreateState(handBuilt)), handBuilt);
Console.WriteLine($"{cases.Count - failures.Count}/{cases.Count} exact TypeScript outcome/error comparisons passed; nested clone + CLR-number round-trip checks passed.");
foreach (var failure in failures) Console.WriteLine(failure);
if(failures.Count>0)throw new Exception("campaign validation parity failed");

 }
 public static void Run(string path)=>CheckCases(path);
}
