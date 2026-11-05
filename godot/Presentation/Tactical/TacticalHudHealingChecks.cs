#nullable enable
using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Localization;
using Godot;

namespace Empire.Presentation.Tactical;

public partial class TacticalHudChecks
{
    private async Task CheckHealingTarget()
    {
        var fixture = PresentationJson.Load<JsonObject>("res://tests/native/c01-12-healing-target.json");
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == "c01-12");
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var battle = engine.CreateBattle(level);
        Require(battle.StateHash == fixture["initialStateHash"]!.GetValue<string>(), "Healing witness starts from the unchanged formal C01-12.");
        foreach (var action in fixture["actions"]!.AsArray().Take(fixture["index"]!.GetValue<int>()).OfType<JsonObject>())
            battle.Dispatch(action);
        Require(battle.StateHash == fixture["beforeStateHash"]!.GetValue<string>(), "Original legal actions reach the exact wounded ally and healer.");
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var session = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(battle.Snapshot()));
        CheckRecipientBoundaries(engine, presentation, session);
        var originalWindowSize = GetWindow().Size;
        var screen = new BattleScreen();
        AddChild(screen);
        try
        {
            screen.Board.AnimationsEnabled = false;
            screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), session,
                new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)),
                level, AuthoredScene.Load("res://data/scenes/c01-12.json"));
            screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
            screen.Size = new Vector2(1180, 812);
            var before = EcmaJson.Stringify(session.Snapshot());
            screen.SelectHit(TacticalHit.Unit(new(7, 4), 4));
            var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
            var healer = hud.Projection!.Sections.Single(section => section.Key == "unit").Title;
            var heal = ((GridContainer)hud.FindChild("Orders", true, false)).GetChildren().OfType<Button>()
                .Single(button => button.GetMeta("hud_key", "").AsString() == "commands/heal");
            heal.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetViewport().GuiReleaseFocus();
            foreach (var pressed in new[] { true, false })
            {
                using var input = new InputEventKey { Keycode = Key.Right, PhysicalKeycode = Key.Right, Pressed = pressed };
                GetViewport().PushInput(input, true);
            }
            Require(screen.Selection is { Kind: TacticalSelectionKind.Target, UnitId: 4, Ability: "heal", Weapon: null }
                && screen.Selection.PreviewTarget == TacticalHit.Unit(new(8, 4), 6), "The native heal command and keyboard navigation preview the real wounded ally.");
            var target = hud.Projection!.Sections.SingleOrDefault(section => section.Key == "forecast");
            GD.Print($"Healing target witness: actor={healer}; target card={target?.Title ?? "missing"}; state={StateDigest.Hash(session.Snapshot())}");
            Require(target is not null && target.Rows.Any(row => row.Key == "recipient" && row.UnitIcon?.ActorKey == "campaign-tasha")
                && target.Rows.Any(row => row.Key == "hp" && row.Gauge == new TacticalHudGauge(65, 92)),
                "Native healing target card must show Tasha and her current 65/92 HP before confirmation.");
            Require(hud.Projection.Sections.Single(section => section.Key == "unit").Title == healer
                && before == EcmaJson.Stringify(session.Snapshot()) && session.Revision == 0 && session.Events.Count == 0,
                "Recipient inspection keeps the acting healer pinned and does not advance battle state.");
            async Task Layout()
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            foreach (var width in new[] { 960, 1180 })
            {
                GetWindow().Size = new Vector2I(width, 640);
                screen.Size = new Vector2(width, 640);
                await Layout();
                var plate = (Control)hud.FindChild("forecastPlate", true, false);
                var group = (VBoxContainer)hud.FindChild("forecast", true, false);
                var labels = group.GetChildren().OfType<HBoxContainer>().SelectMany(row => row.GetChildren().OfType<Label>()).ToArray();
                Require(labels.Any(label => label.Text == target!.Rows.Single(row => row.Key == "recipient").Label)
                    && labels.Any(label => label.Text == text.Format("ui.hud.health", 65, 92))
                    && labels.All(label => label.IsVisibleInTree() && plate.GetGlobalRect().Encloses(label.GetGlobalRect())),
                    "Actual recipient name and current HP stay visible inside the native plate at narrow and wide widths.");
                Require(group.GetChildren().OfType<ProgressBar>().Single() is { Value: 65, MaxValue: 92 }
                    && group.GetChildren().OfType<HBoxContainer>().SelectMany(row => row.GetChildren().OfType<TextureRect>()).Any(image => image.Texture is not null),
                    "The mounted recipient card renders its actual health bar and existing unit artwork.");
                Require(plate.GetGlobalRect().Encloses(((Control)hud.FindChild("SelectionHint", true, false)).GetGlobalRect()),
                    "Target confirmation guidance fits with recipient information.");
            }
            var viewport = GetViewport();
            void Motion(Vector2 point) => viewport.PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
            var sprite = screen.Board.GetNode<Node2D>("CanonicalProjection/Unit_6").GetNode<Sprite2D>("AuthoredSprite");
            Vector2 RecipientPixel()
            {
                var rect = sprite.GetRect();
                for (var y = rect.Position.Y + .5f; y < rect.End.Y; y++)
                    for (var x = rect.Position.X + .5f; x < rect.End.X; x++)
                    {
                        var point = new Vector2(x, y);
                        if (sprite.IsPixelOpaque(point) && screen.Board.Pick(screen.Board.ToLocal(sprite.ToGlobal(point))) == TacticalHit.Unit(new(8, 4), 6))
                            return sprite.ToGlobal(point);
                    }
                throw new InvalidOperationException("Real healing recipient needs an opaque selectable pixel.");
            }
            async Task HoverRecipient()
            {
                Motion(RecipientPixel());
                await Layout();
                Require(screen.Selection?.PreviewTarget == TacticalHit.Unit(new(8, 4), 6), $"Real pointer hit reacquires the canonical healing recipient: kind={screen.Selection?.Kind}, preview={screen.Selection?.PreviewTarget}, point={RecipientPixel()}, hovered={viewport.GuiGetHoveredControl()?.Name}, revision={session.Revision}.");
            }
            Button Cancel() => ((VBoxContainer)hud.FindChild("forecast", true, false)).GetNode<HBoxContainer>("Heading")
                .GetChildren().OfType<Button>().Single(button => button.GetMeta("hud_key", "").AsString() == "target/cancel");
            await HoverRecipient();
            var cancel = Cancel();
            Motion(RecipientPixel());
            await Layout();
            Require(ReferenceEquals(Cancel(), cancel), "Repeated identical target hover keeps the existing native controls mounted.");
            var cancelPoint = cancel.GetGlobalRect().GetCenter();
            Motion(cancelPoint);
            await Layout();
            Require(viewport.GuiGetHoveredControl() is { } hovered && (hovered == hud || hud.IsAncestorOf(hovered))
                && screen.Selection?.PreviewTarget == TacticalHit.Unit(new(8, 4), 6)
                && GodotObject.IsInstanceValid(cancel) && cancel.IsInsideTree()
                && hud.Projection!.Sections.Any(section => section.Key == "forecast"),
                "Real map-to-HUD pointer transfer must preserve the healing recipient and live cancel control.");
            foreach (var pressed in new[] { true, false })
                viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = pressed, Position = cancelPoint, GlobalPosition = cancelPoint }, true);
            await Layout();
            Require(screen.Selection is { Kind: TacticalSelectionKind.Destination, PreviewTarget: null }
                && hud.Projection!.Sections.All(section => section.Key != "forecast")
                && before == EcmaJson.Stringify(session.Snapshot()) && session.Revision == 0,
                "Actual cancel returns to destination composition without healing or stale recipient information.");
            viewport.GuiReleaseFocus();
            screen._UnhandledKeyInput(new InputEventKey { Keycode = Key.Escape, Pressed = true });
            Require(screen.Selection?.Kind == TacticalSelectionKind.Unit, "Return to the original actor before rearming the singleton target preview.");
            ((GridContainer)hud.FindChild("Orders", true, false)).GetChildren().OfType<Button>()
                .Single(button => button.GetMeta("hud_key", "").AsString() == "commands/heal").EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(screen.Selection?.Kind == TacticalSelectionKind.Target && session.Revision == 0, "Rearming from the actor only composes the order.");
            await HoverRecipient();
            Motion(Cancel().GetGlobalRect().GetCenter());
            await Layout();
            GetWindow().EmitSignal(Window.SignalName.MouseExited);
            await Layout();
            Require(screen.Selection?.PreviewTarget is null && hud.Projection!.Sections.All(section => section.Key != "forecast"),
                "Leaving the actual window still clears the healing preview after HUD handoff.");
            await HoverRecipient();
            screen.SelectHit(TacticalHit.Unit(new(8, 4), 6));
            screen.Refresh();
            Require(session.Revision == 1 && StateDigest.Hash(session.Snapshot()) == fixture["afterStateHash"]!.GetValue<string>()
                && EcmaJson.Sha256(new JsonArray(session.Events.Select(value => (JsonNode)value.DeepClone()).ToArray())) == fixture["eventsSha256"]!.GetValue<string>()
                && session.Events.Single(value => value["type"]!.GetValue<string>() == "heal")["amount"]!.GetValue<double>() == 27,
                "Only explicit target confirmation heals the original 27 HP with the exact canonical receipt and state.");
            Require(hud.Projection!.Sections.All(section => section.Key != "forecast"), "Committed healing cannot leave a stale recipient card.");
        }
        finally
        {
            RemoveChild(screen);
            screen.Free();
            GetWindow().Size = originalWindowSize;
        }
    }

    private void CheckRecipientBoundaries(BattleEngine engine, SessionPresentationText presentation, GrayFlagSession session)
    {
        var control = new TacticalSelectionController(session.GetTacticalQueries(61));
        var healer = TacticalHit.Unit(new(7, 4), 4);
        var recipient = TacticalHit.Unit(new(8, 4), 6);
        var before = EcmaJson.Stringify(session.Snapshot());
        TacticalHudProjection Hud() => session.ProjectHud(control.Stamp, control.Selection, control.CursorHit, control.InspectId, control.Busy);
        void Arm()
        {
            control.Bind(session.GetTacticalQueries(61));
            control.Click(control.Stamp, healer);
            control.ChooseCommand(control.Stamp, "heal");
        }
        Arm();
        foreach (var hit in new[] { TacticalHit.Ground(recipient.Cell), TacticalHit.Object(recipient.Cell),
            TacticalHit.Unit(recipient.Cell, 4), TacticalHit.Unit(new(7, 4), 6), TacticalHit.Unit(new(8, 4), int.MaxValue) })
        {
            control.Hover(control.Stamp, hit);
            Require(control.Selection.PreviewTarget is null && Hud().Sections.All(section => section.Key != "forecast"),
                "Ground, object, missing identity and mismatched unit coordinates cannot create a recipient card.");
        }
        control.Hover(control.Stamp, recipient);
        Require(Hud().Sections.Any(section => section.Key == "forecast"), "Admitted recipient creates a card.");
        var held = Hud().Sections.Single(section => section.Key == "forecast");
        control.Hover(control.Stamp, TacticalHit.Ground(recipient.Cell));
        Require(Hud().Sections.Single(section => section.Key == "forecast").Rows.SequenceEqual(held.Rows)
            && control.Key(control.Stamp, "enter").Request is null && control.Click(control.Stamp, TacticalHit.Ground(recipient.Cell)).Request is null,
            "Held recipient information never authorizes confirmation on an invalid live hit.");
        control.Leave(control.Stamp);
        Require(Hud().Sections.All(section => section.Key != "forecast"), "True pointer leave clears held recipient information.");
        control.Hover(control.Stamp, recipient);
        control.Back(control.Stamp);
        Require(Hud().Sections.All(section => section.Key != "forecast"), "Back removes the recipient card.");
        Arm();
        control.Hover(control.Stamp, recipient);
        control.Bind(session.GetTacticalQueries(62));
        Require(Hud().Sections.All(section => section.Key != "forecast"), "Binding reset removes the recipient card.");
        Require(before == EcmaJson.Stringify(session.Snapshot()) && session.Revision == 0 && session.Events.Count == 0,
            "Recipient interaction cannot change state, events or revision.");

        // Detached projection switch: the formal healing order has only one legal target.
        var candidates = session.Snapshot()["units"]!.AsArray().OfType<JsonObject>()
            .Where(unit => unit["id"]!.GetValue<int>() is 6 or 2).ToArray();
        var subjects = candidates.Select(unit => TacticalTarget.Unit(new(unit["x"]!.GetValue<int>(), unit["y"]!.GetValue<int>()), unit["id"]!.GetValue<int>())).ToArray();
        var switchedHud = new TacticalHud();
        AddChild(switchedHud);
        try
        {
            foreach (var candidate in candidates.Concat(candidates.Reverse()))
            {
                var hit = TacticalHit.Unit(new(candidate["x"]!.GetValue<int>(), candidate["y"]!.GetValue<int>()), candidate["id"]!.GetValue<int>());
                var selection = new TacticalSelection
                {
                    Kind = TacticalSelectionKind.Target,
                    UnitId = 4,
                    Destination = healer.Cell,
                    TargetSubjects = subjects,
                    PreviewTarget = hit
                };
                switchedHud.Apply(session.ProjectHud(new(0, 0), selection, hit, 4, false));
                var rows = switchedHud.Projection!.Sections.Single(section => section.Key == "forecast").Rows;
                Require(rows.Single(row => row.Key == "recipient").Label == session.UnitName(EcmaJson.ToElement(candidate))
                    && rows.Single(row => row.Key == "recipient").UnitIcon?.ActorKey == candidate["key"]!.GetValue<string>()
                    && rows.Single(row => row.Key == "hp").Gauge?.Value == candidate["hp"]!.GetValue<double>(),
                    "Switching visible generic recipients replaces name, art identity and HP together without an ability-specific branch.");
            }
        }
        finally
        {
            RemoveChild(switchedHud);
            switchedHud.Free();
        }

        // Detached visibility/identity projection boundary, not a playable healing order.
        var fogState = session.Snapshot();
        fogState["rules"]!["fog"] = true;
        var fog = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(fogState));
        var hidden = fogState["units"]!.AsArray().OfType<JsonObject>().First(unit => fog.GetTacticalQueries(0).Unit(unit["id"]!.GetValue<int>()) is null);
        var hiddenId = hidden["id"]!.GetValue<int>();
        var hiddenHit = TacticalHit.Unit(new(hidden["x"]!.GetValue<int>(), hidden["y"]!.GetValue<int>()), hiddenId);
        var forged = new TacticalSelection
        {
            Kind = TacticalSelectionKind.Target,
            UnitId = 4,
            Ability = "heal",
            Destination = healer.Cell,
            TargetSubjects = new[] { TacticalTarget.Unit(hiddenHit.Cell, hiddenId) },
            PreviewTarget = hiddenHit
        };
        Require(fog.ProjectHud(new(0, 0), forged, hiddenHit, hiddenId, false).Sections.All(section => section.Key != "forecast"),
            "Even a supplied target subject and preview cannot disclose a canonically hidden unit's identity or HP.");
    }
}
