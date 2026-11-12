#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Localization;
using Godot;

namespace Empire.Presentation;

public partial class LuojiaChecks
{
    private const string C07ExportPrefix = "--export-c07-gui-dir=";
    private const string C08ExportPrefix = "--export-c08-gui-dir=";
    private sealed record GuiSavePoint(string Name, JsonObject ApplicationSave);

    private string? GuiExportDirectory(string prefix)
    {
        var supplied = OS.GetCmdlineUserArgs();
        Require(supplied.Length <= 1 && supplied.All(argument =>
            argument.StartsWith(C07ExportPrefix, StringComparison.Ordinal)
            || argument.StartsWith(C08ExportPrefix, StringComparison.Ordinal)),
            "Only one explicit C07 or carried C08 evidence export option is supported.");
        var option = supplied.SingleOrDefault(argument => argument.StartsWith(prefix, StringComparison.Ordinal));
        if (option is null)
            return null;
        var path = option[prefix.Length..];
        Require(Path.IsPathFullyQualified(path), "GUI fixtures need an absolute caller-selected evidence directory.");
        var fullPath = Path.GetFullPath(path);
        var userPath = Path.GetFullPath(OS.GetUserDataDir()).TrimEnd(Path.DirectorySeparatorChar);
        Require(fullPath != userPath && !fullPath.StartsWith(userPath + Path.DirectorySeparatorChar, StringComparison.Ordinal),
            "GUI fixture export cannot write into the active user-save directory.");
        return fullPath;
    }

    private void ExportC08GuiFixtures(string path, BattleEngine engine, JsonArray levels, params GuiSavePoint[] points)
    {
        var definition = PresentationJson.Load<JsonObject>("res://data/grayflag-campaign.json");
        var rules = new CampaignApplicationRules(engine, BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese()));
        foreach (var point in points)
        {
            var production = new GrayFlagCampaign(rules, definition, levels);
            production.Load(point.ApplicationSave);
            var state = production.Snapshot();
            Require(!production.HasPendingBattle && state["battleHistory"]!.AsArray().Count == 1,
                "Carried C08 entry must contain only the actually replayed C07 victory, before a C08 battle begins.");
            var manualPath = Path.Combine(path, point.Name + "-application.json");
            var campaignPath = Path.Combine(path, point.Name + "-campaign.json");
            SaveFile.WriteDocument(manualPath, point.ApplicationSave);
            production.Load(SaveFile.ReadDocument(manualPath));
            Require(EcmaJson.DeepEquals(state, production.Snapshot()), "Exported manual fixture must round-trip through production admission.");
            CampaignSlotStorage.Save(definition, state, campaignPath);
            var loaded = CampaignSlotStorage.Load(engine.UnitContentIssues, definition, campaignPath);
            Require(loaded.Rejected is null && EcmaJson.DeepEquals(state, loaded.State),
                "Exported campaign fixture must round-trip through the formal menu's campaign-slot admission.");
        }
        GD.Print("C08_GUI_FIXTURES_OK " + path +
            ". Starts detached at C07, replays its verified normal-AI victory, then preserves C08 choices. No C01-C06 continuous-play claim; no user save changed.");
    }
}
