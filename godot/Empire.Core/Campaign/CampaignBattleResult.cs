using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.Lifecycle;
using static Empire.Core.State.StateJson;

namespace Empire.Core.Campaign;

/// <summary>Owns the current battle-result wire contract admitted by the campaign.</summary>
public sealed class CampaignBattleResult
{
    private readonly JsonObject document;
    public string RequestId { get; }
    public string Outcome { get; }
    public BattleConclusion? Conclusion { get; }
    public int Turns { get; }

    private CampaignBattleResult(JsonObject document)
    {
        var fields = new[] { "requestId", "outcome", "conclusion", "turns", "units", "signals", "eventCounts" };
        if (document.Count != fields.Length || fields.Any(field => !document.ContainsKey(field)))
            throw new CampaignInvariantException("campaign battle result fields do not match the current native format");
        try
        {
            RequestId = Text(document, "requestId");
            Outcome = Text(document, "outcome");
            Turns = Integer(document, "turns");
            if (EcmaText.IsBlank(RequestId))
                throw new CampaignInvariantException("campaign battle result request id cannot be empty");
            if (Outcome is not ("victory" or "defeat" or "retreat"))
                throw new CampaignInvariantException("campaign battle result outcome is invalid");
            if (Turns < 1)
                throw new CampaignInvariantException("campaign battle result turns must be a positive integer");
            if (Array(document["units"], "battle result units").Any(unit => unit is not JsonObject))
                throw new CampaignInvariantException("campaign battle result units must contain objects");
            if (Array(document["signals"], "battle result signals").Any(signal => EcmaText.IsBlank(Text(signal, "battle result signal"))))
                throw new CampaignInvariantException("campaign battle result signals must contain non-empty strings");
            _ = Object(document["eventCounts"], "battle result event counts");
            Conclusion = document["conclusion"] is { } value ? BattleConclusion.Parse(value) : null;
        }
        catch (DomainInvariantException error)
        { throw new CampaignInvariantException("campaign battle result data is invalid", error); }
        if ((Outcome == "retreat") != (Conclusion is null))
            throw new CampaignInvariantException("campaign battle result retreat and conclusion disagree");
        if (Outcome == "victory" && Conclusion?.Team is null)
            throw new CampaignInvariantException("campaign battle result victory requires a winning team");
        this.document = document;
    }

    public static CampaignBattleResult Create(string requestId, string outcome, BattleConclusion? conclusion,
        int turns, JsonArray units, JsonArray signals, JsonObject eventCounts) => new(new JsonObject
        {
            ["requestId"] = requestId,
            ["outcome"] = outcome,
            ["conclusion"] = conclusion?.ToJson(),
            ["turns"] = turns,
            ["units"] = units?.DeepClone(),
            ["signals"] = signals?.DeepClone(),
            ["eventCounts"] = eventCounts?.DeepClone(),
        });

    public static CampaignBattleResult Parse(JsonObject document) => new((JsonObject)document.DeepClone());
    public JsonObject ToJson() => (JsonObject)document.DeepClone();
}
