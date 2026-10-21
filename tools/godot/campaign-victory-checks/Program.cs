using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;

if (args.Length is < 2 or > 3)
    throw new ArgumentException("Usage: CampaignVictoryChecks <repository> <trace.jsonl[.gz]> [native-opening-save.json]");
var root = args[0];
JsonNode Read(string path) => JsonNode.Parse(File.ReadAllText(Path.Combine(root, path)))!;
var packs = Read("godot/data/content-packs.json").AsArray();
var engine = new GrayFlagEngine(packs);
var campaign = new GrayFlagCampaign(Read("godot/data/grayflag-campaign.json").AsObject(),
    Read("godot/data/grayflag-campaign-levels.json").AsArray(), packs, engine);
var steps = 0;
var saves = 0;
var battles = 0;
var victories = 0;
var complete = false;

void Equal(string label, JsonNode? actual, JsonNode? expected)
{
    if (JsonNode.DeepEquals(actual, expected)) return;
    throw new InvalidOperationException($"Route mismatch {label} after {steps} actions\nactual: {actual}\nexpected: {expected}");
}

using var file = File.OpenRead(args[1]);
using Stream stream = args[1].EndsWith(".gz", StringComparison.Ordinal)
    ? new GZipStream(file, CompressionMode.Decompress) : file;
using var reader = new StreamReader(stream);
string? line;
while ((line = reader.ReadLine()) is not null)
{
    if (complete) throw new InvalidOperationException("Unexpected data after final route checkpoint.");
    var row = JsonNode.Parse(line)!.AsObject();
    switch (row["kind"]!.GetValue<string>())
    {
        case "header":
            if (row["humanDriver"]?.GetValue<string>() != "staged-refugees-v2" || row["humanAggression"] is not null)
                throw new InvalidOperationException("Unexpected test-player policy.");
            Equal("initial campaign", campaign.Runtime.Snapshot(), row["initial"]);
            break;
        case "advance":
            campaign.Runtime.Advance();
            Equal("advance", campaign.Runtime.Snapshot(), row["after"]);
            break;
        case "choose":
            Equal("choices", new JsonArray(campaign.Runtime.Choices().Select(value => value!.DeepClone()).ToArray()), row["choices"]);
            campaign.Runtime.Choose(row["choice"]!.GetValue<string>());
            Equal("choice", campaign.Runtime.Snapshot(), row["after"]);
            break;
        case "begin":
            campaign.BeginBattle();
            battles++;
            Equal("request", campaign.Request, row["request"]);
            Equal("begin campaign", campaign.Runtime.Snapshot(), row["campaign"]);
            Equal("initial battle", JsonValue.Create(StateDigest.Hash(campaign.Battle!.Snapshot())), row["initialHash"]);
            if (args.Length == 3 && row["request"]!["levelId"]!.GetValue<string>() == "c01-02")
            {
                File.WriteAllText(args[2], campaign.Save().ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"CAPTURE native opening c01-02 after {steps} verified actions and {saves} save/resumes: {args[2]}");
            }
            break;
        case "step":
            var session = campaign.Battle!;
            var action = row["action"]!.AsObject();
            if (row["human"]!.GetValue<bool>() == session.IsComputerTurn)
                throw new InvalidOperationException("Trace controller differs from the native battle.");
            var before = session.Events.Count;
            if (session.IsComputerTurn)
            {
                Equal("independently chosen opponent action", engine.Ai.ChooseAction(session.Snapshot()), action);
                if (!session.AdvanceComputer()) throw new InvalidOperationException("Computer did not act.");
            }
            else
            {
                // Recorded player input still passes through the native transactional legality boundary.
                var result = session.Dispatch(JsonSerializer.SerializeToElement(action), session.GetSnapshot().Revision);
                if (!result.Accepted) throw new InvalidOperationException(result.Message);
            }
            Equal("events", new JsonArray(session.Events.Skip(before).Select(value => (JsonNode)value.DeepClone()).ToArray()), row["events"]);
            Equal("state", JsonValue.Create(StateDigest.Hash(session.Snapshot())), row["hash"]);
            steps++;
            break;
        case "checkpoint":
            var own = campaign.Save();
            var source = row["document"]!.AsObject();
            // Writer timestamps are metadata; states, request and event history must match exactly.
            Equal("saved campaign", own["campaign"]!["state"], source["campaign"]!["state"]);
            Equal("saved request", own["request"], source["request"]);
            Equal("saved events", own["events"], source["events"]);
            if (own["battle"] is not null) Equal("saved battle", own["battle"]!["state"], source["battle"]!["state"]);
            campaign.Load(source);
            saves++;
            break;
        case "complete":
            if (campaign.BattleSummary().Outcome != "victory")
                throw new InvalidOperationException("The actual native battle did not end in victory.");
            victories++;
            campaign.FinishBattle();
            Equal("result campaign", campaign.Runtime.Snapshot(), row["after"]);
            break;
        case "final":
            Equal("final campaign", campaign.Runtime.Snapshot(), row["final"]);
            if (battles != 2 || victories != 2 || steps != 428 || saves != 14 ||
                campaign.Runtime.Snapshot()["currentNode"]?.GetValue<string>() != "aftermath-02")
                throw new InvalidOperationException("The required two-battle opening route was not completed.");
            complete = true;
            Console.WriteLine($"PASS two consecutive carried-state victories: {steps} legal actions, independent opponent decisions, {saves} source save/resumes. Campaign remains active at aftermath-02; battle 2 is elimination victory, not escort completion.");
            break;
        default:
            throw new InvalidOperationException($"Unexpected source row {row["kind"]}");
    }
}
if (!complete) throw new InvalidOperationException("Trace ended before the required final checkpoint.");
