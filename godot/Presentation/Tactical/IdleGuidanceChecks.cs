#nullable enable
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Localization;
using Godot;

namespace Empire.Presentation.Tactical;

/// <summary>Actual C01 opening projection and presentation-only layout checks; no battle actions.</summary>
public partial class IdleGuidanceChecks : Node
{
    private int assertions;
    private void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        assertions++;
    }
    private async Task Settle()
    {
        for (var frame = 0; frame < 4; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    public override async void _Ready()
    {
        try
        {
            var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            var text = GodotTextCatalog.LoadSimplifiedChinese();
            var presentation = BattleTextChecks.Session(engine, text);
            var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
                .Single(value => value["id"]!.GetValue<string>() == "c01-01");
            var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
            var before = EcmaJson.Stringify(session.Snapshot());
            var opening = session.ProjectHud(new(0, 0), new(), null, null, false);
            GD.Print("C01 opening sections: " + string.Join(",", opening.Sections.Select(section => section.Key)));
            Require(!opening.Sections.Any(section => section.Key is "commands" or "target" or "deployment" or "recruit" or "tactics"),
                "The actual initial C01 projection has no primary action tray; do not manufacture a tactics section for this regression.");
            Require(opening.Hint.Contains("空格", StringComparison.Ordinal), "The canonical opening projection supplies the existing keyboard instructions.");
            foreach (var size in new[] { new Vector2I(960, 640), new Vector2I(1180, 812) })
            {
                GetWindow().Size = size;
                var hud = new TacticalHud { Theme = TacticalHudStyle.Create() };
                AddChild(hud);
                hud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                var intents = 0;
                hud.IntentRequested += _ => intents++;
                hud.Apply(opening);
                await Settle();
                var hint = (Label)hud.FindChild("SelectionHint", true, false);
                Require(hint.IsVisibleInTree(), "Opening guidance must remain visible when there are no affordable tactics or unit commands.");
                Require(hint.GetVisibleLineCount() == hint.GetLineCount() && hint.MaxLinesVisible == -1,
                    "Opening instructions must be fully readable without pointer-only tooltip access.");
                var tray = (Control)hud.FindChild("InstructionTray", false, false);
                Require(tray.GetGlobalRect().Encloses(hint.GetGlobalRect()), "The complete instruction stays inside its material backing.");
                Require(tray.GetGlobalRect().End.X <= size.X - 194 && tray.GetGlobalRect().Position.X >= 278
                    && tray.GetGlobalRect().Position.Y >= size.Y - 126 && tray.GetGlobalRect().End.Y <= size.Y - 8,
                    "Guidance respects existing portrait/dossier and global-order safe areas without adding a full-height action tray.");
                Require(!hud.FindChildren("*", "Button", true, false).OfType<Button>().Any(button => button.GetMeta("hud_key", "").AsString().StartsWith("tactics/", StringComparison.Ordinal)),
                    "Showing instructions cannot invent an unavailable gameplay action.");
                hud.Apply(opening with
                {
                    Hint = text.Format("ui.hud.selection.idle_recruit")
                });
                await Settle();
                hint = (Label)hud.FindChild("SelectionHint", true, false);
                tray = (Control)hud.FindChild("InstructionTray", false, false);
                Require(hint.IsVisibleInTree() && hint.GetVisibleLineCount() == hint.GetLineCount()
                    && tray.GetGlobalRect().Encloses(hint.GetGlobalRect()) && tray.Size.Y <= 126,
                    "The longer existing recruitment guidance wraps fully in the same bounded instruction tray.");
                var commandFrame = opening with
                {
                    Hint = text.Format("ui.hud.selection.unit"),
                    Sections = opening.Sections.Append(new TacticalHudSection("commands", "只读命令夹具", new[]
                    {
                        new TacticalHudRow("wait", "待机", Intent: new(opening.Stamp, TacticalHudIntentKind.Command, "wait")),
                        new TacticalHudRow("cancel", "取消 Esc", Intent: new(opening.Stamp, TacticalHudIntentKind.Back))
                    })).ToArray()
                };
                hud.Apply(commandFrame);
                await Settle();
                Require(hud.FindChild("InstructionTray", false, false) is null && hud.FindChild("OrderTray", false, false) is Control,
                    "A real action tray replaces instruction-only backing rather than accumulating both.");
                hint = (Label)hud.FindChild("SelectionHint", true, false);
                Require(hint.IsVisibleInTree() && hint.GetVisibleLineCount() == hint.GetLineCount(),
                    "Selected-unit guidance retains its complete existing wrapping.");
                hud.Apply(opening);
                await Settle();
                Require(hud.GetChildren().Count(child => child.Name == "InstructionTray") == 1 && hud.FindChild("OrderTray", false, false) is null,
                    "Returning idle restores exactly one instruction tray and no stale action tray.");
                hud.ShowHint = false;
                hud.Apply(opening with
                {
                    Hint = "Explicit hidden presentation fixture"
                });
                await Settle();
                Require(!((Label)hud.FindChild("SelectionHint", true, false)).Visible && hud.FindChild("InstructionTray", false, false) is null,
                    "An explicit hidden hint creates no empty backing.");
                hud.ShowHint = true;
                hud.Apply(opening with
                {
                    Hint = ""
                });
                await Settle();
                Require(!((Label)hud.FindChild("SelectionHint", true, false)).Visible && hud.FindChild("InstructionTray", false, false) is null,
                    "An empty instruction creates no empty backing.");
                Require(intents == 0 && session.Revision == 0 && session.Events.Count == 0 && EcmaJson.Stringify(session.Snapshot()) == before,
                    "Reading and layout cannot mutate the actual opening battle.");
                RemoveChild(hud);
                hud.Free();
                await Settle();
            }
            GD.Print($"Idle guidance checks passed: {assertions} assertions; C01 initial projection only.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
