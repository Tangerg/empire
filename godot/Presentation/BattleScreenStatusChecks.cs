#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Localization;
using Empire.Presentation.Feedback;
using Empire.Presentation.Tactical;
using Godot;

namespace Empire.Presentation;

public partial class BattleScreenChecks
{
    private async Task CheckRecordedStatusFeedback()
    {
        var originalSize = GetWindow().Size;
        GetWindow().Size = new Vector2I(1180, 760);
        try
        {
            foreach (var fixture in PresentationJson.Load<JsonArray>("res://tests/native/recorded-status-feedback.json").OfType<JsonObject>())
            {
                var levelId = fixture["levelId"]!.GetValue<string>();
                var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
                    .Single(value => value["id"]!.GetValue<string>() == levelId);
                var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
                var battle = engine.CreateBattle(level);
                Require(battle.StateHash == fixture["initialStateHash"]!.GetValue<string>(), "Status witness starts from unchanged formal campaign content.");
                var checkpoints = fixture["checkpoints"]!.AsArray().OfType<JsonObject>().ToDictionary(value => value["index"]!.GetValue<int>());
                var actions = fixture["actions"]!.AsArray().OfType<JsonObject>().ToArray();
                for (var index = 0; index < actions.Length; index++)
                {
                    if (checkpoints.TryGetValue(index, out var checkpoint))
                    {
                        Require(battle.StateHash == checkpoint["beforeStateHash"]!.GetValue<string>(), "The original legal prefix reaches its exact status checkpoint.");
                        var text = GodotTextCatalog.LoadSimplifiedChinese();
                        var presentation = BattleTextChecks.Session(engine, text);
                        var session = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(battle.Snapshot()));
                        var screen = new BattleScreen();
                        AddChild(screen);
                        try
                        {
                            screen.Board.AnimationsEnabled = false;
                            var scene = AuthoredScene.Load($"res://data/scenes/{levelId}.json");
                            var rendering = BoardSceneRendering.Load(scene, level, session.Snapshot()["map"]!.AsObject());
                            screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), session,
                                new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, rendering.Plane)), level, scene);
                            screen.ConfigureGround(rendering.Ground);
                            screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
                            screen.Size = new Vector2(1180, 760);
                            var completed = new List<int>();
                            screen.Board.EventPresented += (_, eventIndex) => completed.Add(eventIndex);
                            var action = actions[index];
                            var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
                            async Task Press(string sectionKey, TacticalHudRow row)
                            {
                                var button = hud.FindChildren("*", "Button", true, false).OfType<Button>()
                                    .Single(value => value.GetMeta("hud_key", "").AsString() == sectionKey + "/" + row.Key);
                                Require(button.IsVisibleInTree() && !button.Disabled, "The formal status witness uses an available native order control.");
                                button.EmitSignal(BaseButton.SignalName.Pressed);
                                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                            }
                            BoardCell Cell(JsonNode value) => new(value["x"]!.GetValue<int>(), value["y"]!.GetValue<int>());
                            if (action["kind"]!.GetValue<string>() == "tactic")
                            {
                                var key = action["commander"]!.GetValue<string>() + ":" + action["tactic"]!.GetValue<string>();
                                var row = hud.Projection!.Sections.Single(section => section.Key == "tactics").Rows
                                    .Single(value => value.Intent?.Key == key);
                                await Press("tactics", row);
                                Require(session.Revision == 0 && screen.Selection?.Kind == TacticalSelectionKind.Tactic,
                                    "Choosing the original rally only composes its target, without changing statuses.");
                                screen.SelectHit(TacticalHit.Ground(Cell(action["target"]!)));
                            }
                            else if (action["kind"]!.GetValue<string>() == "command")
                            {
                                var id = action["unit"]!.GetValue<int>();
                                var path = action["path"]!.AsArray();
                                screen.SelectHit(TacticalHit.Unit(Cell(path[0]!), id));
                                screen.SelectHit(TacticalHit.Ground(Cell(path[^1]!)));
                                var weapon = action["command"]!["weapon"]!.GetValue<string>();
                                var option = session.GetTacticalQueries(0).CommandsAt(id, Cell(path[^1]!)).Single(value => value.Weapon == weapon);
                                var row = hud.Projection!.Sections.Single(section => section.Key == "commands").Rows
                                    .Single(value => value.Intent?.Key == option.Key);
                                await Press("commands", row);
                                if (screen.Selection?.Kind == TacticalSelectionKind.Target)
                                {
                                    var at = Cell(action["command"]!["target"]!);
                                    var target = session.GetTacticalQueries(0).UnitAt(at)!;
                                    screen.SelectHit(TacticalHit.Unit(at, target.Id));
                                }
                            }
                            else
                            {
                                Require(action["kind"]!.GetValue<string>() == "endTurn" && session.IsComputerTurn && session.AdvanceComputer(),
                                    "The exhausted real AI turn advances through its normal application entry point.");
                                screen.Refresh();
                            }
                            var transition = session.GetSnapshot().Transition!;
                            Require(session.Revision == 1 && EcmaJson.DeepEquals(session.ActionLog.Single(), action)
                                && StateDigest.Hash(session.Snapshot()) == checkpoint["afterStateHash"]!.GetValue<string>()
                                && EcmaJson.Sha256(new JsonArray(session.Events.Select(value => (JsonNode)value.DeepClone()).ToArray())) == checkpoint["eventsSha256"]!.GetValue<string>(),
                                "Status feedback preserves the original legal command, exact canonical state and every ordered event.");
                            var statusEvents = transition.Events.Select((value, eventIndex) => (value, eventIndex))
                                .Where(pair => pair.value.GetProperty("type").GetString() is "statusApplied" or "statusRemoved").ToArray();
                            Require(statusEvents.Length > 0, "Each witness actually emits a status transition.");
                            var expected = statusEvents.Select(pair => StatusDescription(session, presentation.Content, transition, pair.value)).ToArray();
                            var actual = statusEvents.Select(pair => transition.EventDescriptions[pair.eventIndex]).ToArray();
                            GD.Print($"STATUS_FEEDBACK {levelId}:{index}: expected=[{string.Join(" | ", expected)}]; actual=[{string.Join(" | ", actual)}]");
                            Require(actual.SequenceEqual(expected) && expected.All(line => screen.RecentMessages.Contains(line)),
                                "Visible applied/refreshed and removed statuses must be described with their committed duration/stacks and retained in the real battle journal.");
                            Require(!screen.IsPresenting && completed.SequenceEqual(Enumerable.Range(0, transition.Events.Count)),
                                "Every event is presented once and in its original order, without adding a gameplay or animation transition.");
                            if (levelId == "c01-01")
                                CheckStatusDisclosureBoundaries(engine, session, presentation, transition, statusEvents[0].eventIndex);
                            if (levelId == "c01-03")
                                Require(transition.Before.GetProperty("units").EnumerateArray().Single(unit => unit.GetProperty("id").GetInt32() == 1)
                                    .GetProperty("statuses").EnumerateArray().Any(status => status.GetProperty("id").ValueEquals("inspired")),
                                    "The formal refresh witness already carries inspiration; the receipt must not claim a new status or add duration.");
                            if (levelId == "c01-14" && index == 129)
                                Require(transition.Before.GetProperty("units").EnumerateArray().Single(unit => unit.GetProperty("id").GetInt32() == 10)
                                        .GetProperty("statuses").EnumerateArray().Any(status => status.GetProperty("id").ValueEquals("armor_down"))
                                    && !transition.After.GetProperty("units").EnumerateArray().Single(unit => unit.GetProperty("id").GetInt32() == 10)
                                        .GetProperty("statuses").EnumerateArray().Any(status => status.GetProperty("id").ValueEquals("armor_down")),
                                    "The real later owner turn ends the previously applied armor-down effect.");
                            var messages = screen.RecentMessages.ToArray();
                            screen.Refresh();
                            screen.Refresh();
                            Require(screen.RecentMessages.SequenceEqual(messages) && completed.Count == transition.Events.Count,
                                "Refreshing a settled revision cannot duplicate status receipts.");
                            ((Button)screen.FindChild("BattleLogButton", true, false)).EmitSignal(BaseButton.SignalName.Pressed);
                            var journal = (RichTextLabel)screen.FindChild("BattleLogText", true, false);
                            Require(journal.IsVisibleInTree() && expected.All(line => journal.Text.Contains(line, StringComparison.Ordinal)),
                                "The actual native journal exposes every retained status receipt for reading, not only an internal event array.");
                            journal.GetWindow().Hide();
                        }
                        finally
                        {
                            RemoveChild(screen);
                            screen.Free();
                        }
                    }
                    battle.Dispatch(actions[index]);
                }
            }
        }
        finally
        {
            GetWindow().Size = originalSize;
        }
    }

    private static void CheckStatusDisclosureBoundaries(BattleEngine engine, GrayFlagSession session,
        SessionPresentationText presentation, BattlePresentationTransition transition, int eventIndex)
    {
        var value = transition.Events[eventIndex];
        var id = value.GetProperty("unit").GetInt32();
        var names = new BattleLogNames(session.UnitName, presentation.Content.UnitName, presentation.Content.CareerName,
            presentation.Content.ResourceName, player => presentation.Battle.PlayerName("c01-01", player));
        var visible = new HashSet<int> { id };
        var hidden = new HashSet<int>();
        var removal = EcmaJson.ToElement(new JsonObject { ["type"] = "statusRemoved", ["unit"] = id, ["status"] = "inspired" });
        foreach (var before in new IReadOnlySet<int>?[] { null, hidden, visible })
            foreach (var after in new IReadOnlySet<int>?[] { null, hidden, visible })
            {
                var context = new BattleLogContext(names, transition.Before, transition.After, before, after);
                var expected = (before?.Contains(id) ?? true) && (after?.Contains(id) ?? true);
                foreach (var item in new[] { value, removal })
                    Require((presentation.Events.Describe(context, item).Length > 0) == expected,
                        "Both status applications and removals obey the exact before/after visibility boundary; null is unrestricted and empty is concealed.");
                var descriptions = presentation.Events.DescribeAll(context, transition.Events);
                Require(descriptions.Count == transition.Events.Count && (descriptions[eventIndex].Length > 0) == expected,
                    "Hidden status receipts retain their original event slot without leaking a description.");
            }
        var hiddenContext = new BattleLogContext(names, transition.Before, transition.After, hidden, hidden);
        var unavailable = EcmaJson.ToElement(new JsonObject
        {
            ["type"] = "statusApplied",
            ["unit"] = int.MaxValue,
            ["status"] = "unavailable.status",
            ["remaining"] = 2,
            ["stacks"] = 1
        });
        Require(presentation.Events.Describe(hiddenContext, unavailable).Length == 0,
            "Concealed status events are filtered before resolving any unit or content identity.");
        var frozen = new BattleLogContext(names, transition.Before, transition.After, visible, visible);
        visible.Clear();
        Require(presentation.Events.Describe(frozen, value) == transition.EventDescriptions[eventIndex],
            "A later caller visibility mutation cannot change a committed status description.");

        // Explicit formatter boundary: an earlier application remains a receipt even if
        // the status is absent from the final projection. This is not a campaign action.
        var afterRemoval = EcmaJson.Parse(transition.After.GetRawText())!.AsObject();
        afterRemoval["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["id"]!.GetValue<int>() == id)["statuses"] = new JsonArray();
        var absent = new BattleLogContext(names, transition.Before, EcmaJson.ToElement(afterRemoval));
        Require(presentation.Events.Describe(absent, value) == transition.EventDescriptions[eventIndex],
            "Application wording uses the emitted remaining duration and stacks, never final-state status reconstruction.");

        var translation = ResourceLoader.Load<Translation>(GodotTextCatalog.ResourcePath);
        var available = translation.GetMessageList().ToHashSet(StringComparer.Ordinal);
        var alternate = new GameText(key => key switch
        {
            "content.statuses.inspired.name" => "Composed inspiration",
            "battle.event.status_applied" => "{0}|{1}|remaining={2}|stacks={3}",
            _ => available.Contains(key) ? translation.GetMessage(key).ToString() : null
        }, CultureInfo.GetCultureInfo("en-US"));
        var other = BattleTextChecks.Session(engine, alternate);
        Require(other.Events.Describe(absent, value).Contains("|Composed inspiration|remaining=2|stacks=1", StringComparison.Ordinal)
            && presentation.Events.Describe(absent, value) == transition.EventDescriptions[eventIndex],
            "The composed content/catalog owns status names and prose; another presentation composition cannot alter this session's receipts.");
    }

    private static string StatusDescription(GrayFlagSession session, ContentText content, BattlePresentationTransition transition, JsonElement value)
    {
        var id = value.GetProperty("unit").GetInt32();
        var unit = transition.Before.GetProperty("units").EnumerateArray().Single(unit => unit.GetProperty("id").GetInt32() == id);
        var name = session.UnitName(unit);
        var status = content.Name(ContentTextKind.Status, value.GetProperty("status").GetString()!);
        return value.GetProperty("type").ValueEquals("statusRemoved")
            ? $"{name}：{status}效果结束"
            : $"{name}：{status} · 剩余 {value.GetProperty("remaining").GetDouble().ToString(CultureInfo.InvariantCulture)} 回合 · {value.GetProperty("stacks").GetDouble().ToString(CultureInfo.InvariantCulture)} 层";
    }
}
