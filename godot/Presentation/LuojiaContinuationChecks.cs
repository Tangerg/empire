#nullable enable
using System;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Campaign;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Localization;
using Godot;

namespace Empire.Presentation;

public partial class LuojiaChecks
{
    private void CheckNormalAiContinuation(BattleEngine engine, JsonArray levels)
    {
        var exportDirectory = GuiExportDirectory(C08ExportPrefix);
        var fixture = PresentationJson.Load<JsonObject>("res://tests/native/c01-07-normal-ai-continuation.json");
        var definition = PresentationJson.Load<JsonObject>("res://data/grayflag-campaign.json");
        var detached = definition.DeepClone().AsObject();
        detached["start"] = "brief-07";
        var rules = new CampaignApplicationRules(engine, BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese()));
        var campaign = new GrayFlagCampaign(rules, detached, levels);
        campaign.Advance();
        campaign.BeginBattle();
        var session = campaign.Battle!;
        Require(campaign.Snapshot()["battleHistory"]!.AsArray().Count == 0
            && StateDigest.Hash(session.Snapshot()) == Text(fixture, "initialStateHash"),
            "The normal-AI witness starts from its actual detached campaign request, not an independent-level state or invented earlier victories.");
        var beforeSettlement = campaign.Snapshot();
        var actions = fixture["actions"]!.AsArray();
        var hashes = fixture["stateHashes"]!.AsArray();
        Require(actions.Count == hashes.Count && actions.Count == 263, "The C07 continuation fixture must retain every committed order and state hash.");
        var computerOrders = 0;
        for (var index = 0; index < actions.Count; index++)
        {
            var action = actions[index]!.AsObject();
            var revision = session.Revision;
            if (session.IsComputerTurn)
            {
                Require(EcmaJson.DeepEquals(engine.Ai.ChooseAction(session.Snapshot()), action),
                    $"C07 enemy order {index} must be chosen by the unchanged normal AI, not replaced with a handoff.");
                Require(session.AdvanceComputer(), $"C07 normal AI must commit order {index}.");
                computerOrders++;
            }
            else
                Require(session.Dispatch(EcmaJson.ToElement(action), revision).Accepted,
                    $"Recorded player order {index} must pass the production session's current legality checks.");
            Require(session.Revision == revision + 1 && EcmaJson.DeepEquals(session.ActionLog.Last(), action)
                && StateDigest.Hash(session.Snapshot()) == hashes[index]!.GetValue<string>(),
                $"C07 receipt {index} must preserve its actual action, single revision and canonical state.");
        }
        var terminal = session.Snapshot();
        var events = new JsonArray(session.Events.Select(value => (JsonNode)value.DeepClone()).ToArray());
        Require(computerOrders > 0 && session.IsConcluded && campaign.BattleSummary().Outcome == "victory"
            && Text(terminal["conclusion"]!, "kind") == "objectiveCompleted"
            && Text(terminal["conclusion"]!, "objective") == "primary"
            && Integer(terminal, "turn") == Integer(fixture, "terminalTurn")
            && StateDigest.Hash(terminal) == Text(fixture, "finalStateHash")
            && events.Count == Integer(fixture, "eventCount") && EcmaJson.Sha256(events) == Text(fixture, "eventsSha256"),
            "Normal opposition must reach the actual C07 primary-objective victory and retain all ordered events.");
        Require(EcmaJson.DeepEquals(campaign.Snapshot(), beforeSettlement),
            "A concluded C07 battle cannot grant rewards or advance the campaign before explicit settlement.");
        var restored = new GrayFlagCampaign(rules, definition, levels);
        restored.Load(campaign.Save());
        Require(StateDigest.Hash(restored.Battle!.Snapshot()) == StateDigest.Hash(terminal)
            && Events(restored.Battle) == Events(session), "The real terminal C07 session must load through unchanged production admission.");
        campaign.FinishBattle();
        var settled = campaign.Snapshot();
        var settledSave = campaign.Save();
        Require(Text(campaign.Node(), "id") == "aftermath-07" && settled["battleHistory"]!.AsArray().Count == 1,
            "Exactly this detached C07 victory must settle to its authored aftermath.");
        var duplicateRejected = false;
        try
        {
            campaign.FinishBattle();
        }
        catch (CampaignActionException) { duplicateRejected = true; }
        Require(duplicateRejected && EcmaJson.DeepEquals(campaign.Snapshot(), settled),
            "Repeated settlement must reject without duplicating C07 rewards, history or roster changes.");
        Require(Text(settled["roster"]!["torren"]!, "disposition") == "fallen"
            && Text(settled["roster"]!["bran"]!, "disposition") == "available"
            && settled["roster"]!["bran"]!["hpRatio"]!.GetValue<double>() == .3
            && settled["roster"]!["elin"]!["rankProgress"]!.GetValue<double>() == 40,
            "This played victory must retain Torren's casualty, Bran's authored hero recovery floor and Elin's actual advancement.");
        campaign.Advance();
        Require(Text(campaign.Node(), "id") == "brief-08", "C07 aftermath must lead to the real C08 briefing.");
        var briefingSave = campaign.Save();
        campaign.Advance();
        Require(Text(campaign.Node(), "id") == "choice-mercenary-contract", "C08 must retain its authored contract choice.");
        var choiceSave = campaign.Save();
        campaign.Choose("create-pension-ledger");
        Require(Text(campaign.Node(), "id") == "battle-08", "The authored choice must lead to C08 staging.");
        restored = new GrayFlagCampaign(rules, definition, levels);
        restored.Load(campaign.Save());
        Require(!restored.HasPendingBattle && EcmaJson.DeepEquals(restored.Snapshot(), campaign.Snapshot()),
            "C08 staging must round-trip with the actually settled roster and choice.");
        var ledgerStagingSave = campaign.Save();
        campaign.BeginBattle();
        var next = campaign.Battle!.Snapshot();
        Require(Text(next, "levelId") == "c01-08" && StateDigest.Hash(next) == Text(fixture, "c08OpeningHash")
            && campaign.Snapshot()["battleHistory"]!.AsArray().Count == 1
            && Units(next).All(unit => Text(unit, "key") != "campaign-torren"),
            "The actual C08 opening must inherit this one victory without resurrecting a fallen retainer.");
        CheckContinuationRoster(engine, campaign, next);
        restored = new GrayFlagCampaign(rules, definition, levels);
        restored.Load(campaign.Save());
        Require(StateDigest.Hash(restored.Battle!.Snapshot()) == StateDigest.Hash(next)
            && EcmaJson.DeepEquals(restored.Snapshot(), campaign.Snapshot()),
            "The real carried C08 opening must survive production save/load without health or progression reset.");
        CheckContinuationProjection(engine, levels.OfType<JsonObject>().Single(value => Text(value, "id") == "c01-08"),
            campaign.RequestSnapshot()!["level"]!.AsObject(), next);
        var paid = new GrayFlagCampaign(rules, definition, levels);
        paid.Load(settledSave);
        paid.Advance();
        paid.Advance();
        paid.Choose("pay-what-is-owed");
        Require(Text(paid.Node(), "id") == "battle-08"
            && paid.Snapshot()["resources"]!["treasury"]!.GetValue<double>() == settled["resources"]!["treasury"]!.GetValue<double>() - 2
            && paid.Snapshot()["flags"]!.AsArray().Any(flag => flag!.GetValue<string>() == "mercenary_debt_paid")
            && !paid.Snapshot()["flags"]!.AsArray().Any(flag => flag!.GetValue<string>() == "pension_ledger_created")
            && campaign.Snapshot()["flags"]!.AsArray().Any(flag => flag!.GetValue<string>() == "pension_ledger_created")
            && !campaign.Snapshot()["flags"]!.AsArray().Any(flag => flag!.GetValue<string>() == "mercenary_debt_paid"),
            "The two authored C08 choices must retain their separate flags and the actual debt payment without crossing branches.");
        Require(paid.Snapshot()["relations"]!["tasha"]!.GetValue<double>() == settled["relations"]!["tasha"]!.GetValue<double>() + 2
            && campaign.Snapshot()["relations"]!["tasha"]!.GetValue<double>() == settled["relations"]!["tasha"]!.GetValue<double>() + 1
            && campaign.Snapshot()["variables"]!["institutions"]!.GetValue<double>() == settled["variables"]!["institutions"]!.GetValue<double>() + 1
            && paid.Snapshot()["variables"]!["institutions"]!.GetValue<double>() == settled["variables"]!["institutions"]!.GetValue<double>(),
            "Both authored contracts must apply their own relation and institution effects exactly once.");
        var paidStagingSave = paid.Save();
        paid.BeginBattle();
        var ledgerFunds = next["players"]!.AsArray().OfType<JsonObject>().Single(player => Integer(player, "id") == 1)["resources"]!["funds"]!["current"]!.GetValue<double>();
        var paidFunds = paid.Battle!.Snapshot()["players"]!.AsArray().OfType<JsonObject>().Single(player => Integer(player, "id") == 1)["resources"]!["funds"]!["current"]!.GetValue<double>();
        Require(paidFunds == ledgerFunds - 40, "The actual paid-debt treasury cost must reduce C08 starting funds by the authored 20 funds per treasury.");
        CheckContinuationRoster(engine, paid, paid.Battle!.Snapshot());
        restored = new GrayFlagCampaign(rules, definition, levels);
        restored.Load(paid.Save());
        Require(Text(paid.Battle.Snapshot(), "levelId") == "c01-08"
            && StateDigest.Hash(restored.Battle!.Snapshot()) == StateDigest.Hash(paid.Battle.Snapshot())
            && paid.Snapshot()["battleHistory"]!.AsArray().Count == 1
            && EcmaJson.DeepEquals(restored.Snapshot(), paid.Snapshot()),
            "The alternative paid-debt choice must also enter and round-trip the carried C08 battle.");
        if (exportDirectory is not null)
            ExportC08GuiFixtures(exportDirectory, engine, levels,
                new("c08-carried-briefing", briefingSave), new("c08-carried-choice", choiceSave),
                new("c08-ledger-staging", ledgerStagingSave), new("c08-paid-staging", paidStagingSave));
        GD.Print($"C08_BOTH_CHOICES_OK ledger={StateDigest.Hash(next)}; paid={StateDigest.Hash(paid.Battle.Snapshot())}; no branch leakage.");
        GD.Print($"C07_NORMAL_AI_CONTINUATION_OK orders={actions.Count}; computer={computerOrders}; turn={terminal["turn"]}; events={events.Count}; terminal={StateDigest.Hash(terminal)}; C08={StateDigest.Hash(next)}. Detached C07 start; no C01-C06, GUI or balance acceptance claim.");
    }

    private void CheckContinuationRoster(BattleEngine engine, GrayFlagCampaign campaign, JsonObject opening)
    {
        var roster = campaign.Snapshot()["roster"]!;
        var bindings = campaign.RequestSnapshot()!["rosterBindings"]!.AsArray().OfType<JsonObject>().ToArray();
        Require(bindings.Length == 5 && bindings.Select(binding => Text(binding, "campaignUnit")).ToHashSet()
            .SetEquals(new[] { "laiya", "elin", "mirelle", "bran", "tasha" }),
            "The C08 request must include every expected survivor and new companion, excluding only the fallen retainer.");
        foreach (var binding in bindings)
        {
            var id = Text(binding, "campaignUnit");
            var saved = roster[id]!;
            var unit = Unit(opening, Text(binding, "levelUnitKey"));
            var content = engine.Catalog.Definition("units", Text(saved, "unitType"))!.Value;
            var maximumMorale = content.TryGetProperty("morale", out var morale) && morale.TryGetProperty("maximum", out var maximum)
                ? maximum.GetDouble() : 100;
            Require(unit["hp"]!.GetValue<double>() == Math.Max(1, Math.Floor(content.GetProperty("maxHp").GetDouble() * saved["hpRatio"]!.GetValue<double>() + .5))
                && unit["morale"]!["current"]!.GetValue<double>() == Math.Max(1, Math.Floor(maximumMorale * saved["moraleRatio"]!.GetValue<double>() + .5))
                && EcmaJson.DeepEquals(unit["rank"], saved["rank"])
                && EcmaJson.DeepEquals(unit["rankProgress"], saved["rankProgress"] ?? JsonValue.Create(0))
                && EcmaJson.DeepEquals(unit["resources"], saved["resources"] ?? new JsonObject()),
                $"C08 must reconstruct {id}'s actual carried injury, morale, rank and resources.");
        }
    }

    private void CheckContinuationProjection(BattleEngine engine, JsonObject registeredLevel, JsonObject level, JsonObject state)
    {
        var scene = AuthoredScene.Load("res://data/scenes/c01-08.json");
        var rendering = BoardSceneRendering.Load(scene, registeredLevel, engine.CreateState(registeredLevel)["map"]!.AsObject());
        var board = new BoardView();
        AddChild(board);
        var before = EcmaJson.Stringify(state);
        try
        {
            board.Configure(new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get(Text(state["rules"]!, "grid")),
                engine.Catalog, rendering.Plane), level, scene);
            board.ConfigureGround(rendering.Ground);
            board.ConfigureDynamicState();
            board.RenderState(state);
            Require(board.RenderedUnitCount == Units(state).Count() && EcmaJson.Stringify(state) == before,
                "The carried C08 snapshot must project every actual unit without becoming another state writer.");
            GD.Print($"C08_CONTINUATION_PROJECTION units={board.RenderedUnitCount}; sourceReady={scene.RuntimeReady}; diagnostics={string.Join(" | ", board.Diagnostics)}. Headless projection, not completed scene art or GUI acceptance.");
        }
        finally { RemoveChild(board); board.Free(); }
    }
}
