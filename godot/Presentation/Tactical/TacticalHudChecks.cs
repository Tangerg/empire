#nullable enable
using System;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Engine;
using Empire.Core.Tactics;
using Empire.Localization;
using Empire.Core.Determinism;
using Godot;

namespace Empire.Presentation.Tactical;

public partial class TacticalHudChecks : Node
{
    private int assertions;
    private void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        assertions++;
    }
    private void CheckInspectionOrder()
    {
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var presentation = BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese());
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == "c01-03");
        var state = engine.CreateState(level);
        var structure = state["structures"]![0]!.AsObject();
        var at = new BoardCell(structure["x"]!.GetValue<int>(), structure["y"]!.GetValue<int>());
        foreach (var disabled in new[] { false, true })
        {
            structure["disabled"] = disabled;
            var session = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(state));
            var before = EcmaJson.Stringify(session.Snapshot());
            var hud = session.ProjectHud(new(0, 0), new(), TacticalHit.Object(at), null, false);
            var sections = hud.Sections.Select(section => section.Key).ToArray();
            Require(Array.IndexOf(sections, "structure") < Array.IndexOf(sections, "tile"), "Structure inspection precedes passive ground details.");
            var rows = hud.Sections.Single(section => section.Key == "structure").Rows;
            Require(rows.Take(2).Select(row => row.Key).SequenceEqual(new[] { "hp", "owner" }), "Health and owner precede structure capabilities.");
            Require(rows.Any(row => row.Key == "disabled") == disabled && (!disabled || rows[2].Key == "disabled"), "Current disabled state precedes structure capabilities.");
            Require(before == EcmaJson.Stringify(session.Snapshot()), "Inspection ordering cannot mutate canonical state.");
        }
        // A detached recruitment fixture: campaign sites need not offer recruits.
        var site = new BoardCell(0, 0);
        state["map"]!["tiles"]![0] = "castle";
        state["map"]!["owners"]![0] = state["currentPlayer"]!.DeepClone();
        var initial = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(state));
        Require(initial.GetTacticalQueries(0).CanOpenRecruitment(site), "Recruitment fixture is reachable through the existing query.");
        var recruit = initial.ProjectHud(new(0, 0), new()
        {
            Kind = TacticalSelectionKind.Recruit,
            Destination = site
        }, TacticalHit.Object(site), null, false);
        var keys = recruit.Sections.Select(section => section.Key).ToArray();
        Require(Array.IndexOf(keys, "recruit") < Array.IndexOf(keys, "tile"), "Recruitment actions precede passive site terrain details.");
        var cards = engine.Recruitment.Cards(initial.Snapshot(), new(site.X, site.Y));
        var choices = recruit.Sections.Single(section => section.Key == "recruit").Rows.Where(row => row.Intent?.Kind == TacticalHudIntentKind.Recruit).ToArray();
        Require(choices.Select(row => row.Intent!.Key).SequenceEqual(cards.Select(card => card.Unit))
            && choices.Select(row => row.Enabled).SequenceEqual(cards.Select(card => card.Affordable)), "Reordering preserves the authoritative recruitment cards and affordability.");
        state["map"]!["owners"]![0] = 0;
        var neutral = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(state));
        var unavailable = neutral.ProjectHud(new(0, 0), new()
        {
            Kind = TacticalSelectionKind.Site,
            Destination = site
        }, null, null, false);
        Require(engine.Recruitment.Cards(neutral.Snapshot(), new(0, 0)).Count == 0
            && !neutral.GetTacticalQueries(0).CanOpenRecruitment(site)
            && unavailable.Sections.Single(section => section.Key == "site").Rows.Single(row => row.Key == "recruit-use").Label
                == GodotTextCatalog.LoadSimplifiedChinese().Format("ui.hud.site.recruit_unavailable"),
            "A producing but unowned site cannot promise a recruitment panel that the canonical query will not open.");
    }

    private void CheckSiteInspection()
    {
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == "c01-03");
        var presentation = BattleTextChecks.Session(engine, text);
        var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        var control = new TacticalSelectionController(session.GetTacticalQueries(41));
        var home = new BoardCell(38, 23);
        var before = EcmaJson.Stringify(session.Snapshot());
        TacticalHudProjection Hud() => session.ProjectHud(control.Stamp, control.Selection, control.CursorHit, control.InspectId, control.Busy);
        Require(control.Click(control.Stamp, TacticalHit.Object(home)).Request is null && control.Selection.Kind == TacticalSelectionKind.Site,
            "The real non-recruiting Redstone home opens inspection, not a recruit panel or a domain command.");
        control.Leave(control.Stamp);
        var homeHud = Hud();
        var site = homeHud.Sections.Single(section => section.Key == "site");
        Require(control.Cursor is null && control.Overlay().Selected == home && site.Rows.Any(row => row.Key == "owner"),
            "Leaving the board for the HUD preserves the selected site, its highlight and owner.");
        Require(site.Rows.Single(row => row.Key == "heal").Label == text.Format("ui.hud.terrain_heal", 15)
            && site.Rows.Single(row => row.Key == "heal-use").Label == text.Format("ui.hud.site.heal_use", text.Format("ui.hud.site.heal_timing_side")),
            "The unchanged initial home explains the canonical 15 HP and owned-unit start-of-turn timing.");
        Require(site.Rows.Single(row => row.Key == "recruit-use").Label == text.Format("ui.hud.site.no_recruitment")
            && homeHud.Sections.All(section => section.Key != "recruit")
            && !session.GetTacticalQueries(41).CanOpenRecruitment(home),
            "Home stays non-recruiting in both data and UI.");
        var unit = session.Snapshot()["units"]!.AsArray().OfType<JsonObject>().First(value => value["owner"]!.GetValue<int>() == 1);
        var unitAt = new BoardCell(unit["x"]!.GetValue<int>(), unit["y"]!.GetValue<int>());
        control.Hover(control.Stamp, TacticalHit.Unit(unitAt, unit["id"]!.GetValue<int>()));
        Require(control.Selection.Destination == home && control.InspectId is null
            && Hud().Sections.Single(section => section.Key == "site").Title == site.Title,
            "Moving across units toward the HUD cannot replace a pinned site with incidental unit inspection.");
        control.Leave(control.Stamp);
        var hudWidget = new TacticalHud();
        AddChild(hudWidget);
        hudWidget.IntentRequested += intent => control.Back(intent.Stamp);
        hudWidget.Apply(Hud());
        var close = hudWidget.GetNode<VBoxContainer>("site").GetChildren().OfType<Button>().Single();
        close.EmitSignal(BaseButton.SignalName.Pressed);
        Require(control.Selection.Kind == TacticalSelectionKind.Idle && control.Overlay().Selected is null
            && Hud().Sections.All(section => section.Key != "site"),
            "The mounted close button clears selected site inspection after pointer leave.");
        RemoveChild(hudWidget);
        hudWidget.Free();
        var sites = new[] { home, new BoardCell(16, 17), new(23, 36), new(55, 40), new(85, 14), new(88, 21), new(78, 30), new(88, 39) };
        foreach (var at in sites)
        {
            Require(control.Click(control.Stamp, TacticalHit.Object(at)).Request is null && control.Selection.Kind == TacticalSelectionKind.Site
                && control.Selection.Destination == at && control.Overlay().Selected == at,
                $"Redstone site {at} must replace the previous inspection without advancing the battle; got {control.Selection.Kind}.");
            if (session.GetTacticalQueries(41).UnitAt(at) is { Visible: true } occupant)
            {
                control.Hover(control.Stamp, TacticalHit.Unit(unitAt, unit["id"]!.GetValue<int>()));
                control.Leave(control.Stamp);
                Require(control.InspectId is null && Hud().Sections.All(section => section.Key != "unit"),
                    "An explicit site hit must not inspect a same-cell garrison that the pointer did not hit.");
            }
            control.Leave(control.Stamp);
            var rows = Hud().Sections.Single(section => section.Key == "site").Rows;
            var index = at.Y * level["width"]!.GetValue<int>() + at.X;
            var owner = session.Snapshot()["map"]!["owners"]![index]!.GetValue<int>();
            Require(rows.Any(row => row.Key == "capture-use") == (owner != 1),
                "Only a site not owned by the viewer explains capture; no capture command is invented.");
        }
        control.Key(control.Stamp, "escape");
        Require(control.Selection.Kind == TacticalSelectionKind.Idle && control.Overlay().Selected is null,
            "Escape closes the pinned site through the existing back transition.");
        control.Click(control.Stamp, TacticalHit.Object(home));
        control.Click(control.Stamp, TacticalHit.Unit(unitAt, unit["id"]!.GetValue<int>()));
        Require(control.Selection.Kind == TacticalSelectionKind.Unit && control.Selection.UnitId == unit["id"]!.GetValue<int>(),
            "A friendly actor remains directly selectable from site inspection.");
        Require(before == EcmaJson.Stringify(session.Snapshot()) && session.Events.Count == 0,
            "Site selection, hover, switching and closing do not mutate battle facts or emit events.");
    }

    private async System.Threading.Tasks.Task CheckSitePointerBoundary()
    {
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var presentation = BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese());
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == "c01-03");
        var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        var scene = AuthoredScene.Load("res://data/scenes/c01-03.json");
        var rendering = BoardSceneRendering.Load(scene, level, session.Snapshot()["map"]!.AsObject());
        var screen = new BattleScreen();
        AddChild(screen);
        screen.BindSession(new(presentation.Messages, presentation.Battle, PortraitCatalogResources.Load(), session,
            new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, rendering.Plane)), level, scene);
        screen.ConfigureGround(rendering.Ground);
        screen.ConfigureDynamicState();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var map = (Control)screen.FindChild("MapArea", true, false);
        var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
        var inside = map.Size / 2;
        var outside = new Vector2(map.Size.X + 1, inside.Y);
        void Motion(Vector2 at) => map.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseMotion { Position = at });
        void Down(Vector2 at, MouseButton button = MouseButton.Left) => map.EmitSignal(Control.SignalName.GuiInput,
            new InputEventMouseButton { ButtonIndex = button, Pressed = true, Position = at });
        void Release(Vector2 at, MouseButton button = MouseButton.Left) => screen._Input(new InputEventMouseButton
        {
            ButtonIndex = button,
            Position = map.GetGlobalTransformWithCanvas() * at
        });
        bool HasTile() => hud.Projection!.Sections.Any(section => section.Key == "tile");

        screen.SelectHit(TacticalHit.Object(new(38, 23)));
        Motion(outside);
        Require(screen.Selection?.Kind == TacticalSelectionKind.Site
            && hud.Projection!.Sections.Single(section => section.Key == "tile").Rows.First().Label == "38, 23",
            "A native outside-map motion preserves only the pinned site, not an offscreen map projection.");
        hud.GetNode<VBoxContainer>("site").GetChildren().OfType<Button>().Single().EmitSignal(BaseButton.SignalName.Pressed);
        Require(screen.Selection?.Kind == TacticalSelectionKind.Idle && !HasTile(),
            "Closing from the HUD cannot expose a fictitious offscreen hover tile.");
        Motion(inside);
        Require(HasTile(), "The native viewport center remains inspectable.");
        screen.SetApplicationBusy(true);
        Motion(outside);
        Require(!HasTile(), "Busy read-only hover obeys the same viewport boundary.");
        screen.SetApplicationBusy(false);

        // A release just beyond the edge is below the six-pixel drag threshold.
        Motion(inside);
        Down(new(map.Size.X - 1, inside.Y));
        map.EmitSignal(Control.SignalName.MouseExited);
        Release(outside);
        Require(!HasTile() && screen.Selection?.Kind == TacticalSelectionKind.Idle,
            "A short boundary crossing clears hover without dispatching a click into the HUD.");
        Motion(inside);
        Require(HasTile(), "An outside release clears pointer capture for the next hover.");
        Down(new(map.Size.X - 8, inside.Y));
        screen._Input(new InputEventMouseMotion { Position = map.GetGlobalTransformWithCanvas() * outside });
        Release(outside);
        Motion(inside);
        Require(HasTile() && screen.Selection?.Kind == TacticalSelectionKind.Idle && screen.DisplayedRevision == 0,
            $"A drag released over the HUD clears capture and cannot advance battle state; tile={HasTile()}, selection={screen.Selection?.Kind}, revision={screen.DisplayedRevision}.");
        Down(new(map.Size.X - 1, inside.Y), MouseButton.Middle);
        map.EmitSignal(Control.SignalName.MouseExited);
        Release(outside, MouseButton.Middle);
        Require(!HasTile(), "Middle-button boundary release clears hover through the same captured gesture policy.");
        RemoveChild(screen);
        screen.Free();
    }

    public override async void _Ready()
    {
        try
        {
            CheckInspectionOrder();
            CheckSiteInspection();
            await CheckSitePointerBoundary();
            var stamp = new TacticalStamp(17, 9);
            var hud = new TacticalHud();
            AddChild(hud);
            var fired = 0;
            hud.IntentRequested += intent => { Require(intent.Stamp == stamp, "Exact stamp emitted"); fired++; };
            TacticalHudProjection Projection(bool enabled = true) => new(stamp, "下一步", new[] { new TacticalHudSection("orders", "指令", new[] { new TacticalHudRow("wait", "待命", "原始提示", enabled, Intent: new(stamp, TacticalHudIntentKind.Command, "wait")) }) });
            Button Button() => hud.GetChildren().OfType<VBoxContainer>().Single().GetChildren().OfType<Button>().Single();
            hud.Apply(Projection());
            var old = Button();
            Require(old.TooltipText == "原始提示", "Native reason tooltip");
            old.EmitSignal(BaseButton.SignalName.Pressed);
            old.EmitSignal(BaseButton.SignalName.Pressed);
            Require(fired == 1, "One-shot duplicate signal suppression");
            hud.Apply(Projection());
            old.EmitSignal(BaseButton.SignalName.Pressed);
            Require(fired == 1, "Replaced row stale callback");
            var mounted = Button();
            RemoveChild(hud);
            mounted.EmitSignal(BaseButton.SignalName.Pressed);
            Require(fired == 1, "Detached widget inert");
            AddChild(hud);
            mounted.EmitSignal(BaseButton.SignalName.Pressed);
            Require(fired == 1, "Remount without Apply inert");
            hud.Apply(Projection(false));
            var disabled = Button();
            Require(disabled.Disabled, "Native disabled row");
            disabled.EmitSignal(BaseButton.SignalName.Pressed);
            Require(fired == 1, "Disabled emitted signal inert");
            hud.Apply(Projection() with
            {
                Busy = true
            });
            var busy = Button();
            busy.EmitSignal(BaseButton.SignalName.Pressed);
            Require(busy.Disabled && fired == 1, "Busy projection cannot emit tactical input");
            hud.Apply(Projection());
            var detached = Button();
            detached.GetParent().RemoveChild(detached);
            detached.EmitSignal(BaseButton.SignalName.Pressed);
            Require(fired == 1, "Detached child inert");
            detached.QueueFree();
            hud.Apply(Projection());
            Button().GrabFocus();
            hud.Apply(Projection());
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(Button().HasFocus(), "Stable row focus restored");
            var portraits = PortraitCatalogResources.Load();
            var metadata = PresentationJson.Load<TacticalHudArtDocument>("res://data/tactical-hud-art.json");
            foreach (var image in metadata.Icons.Values.Concat(metadata.Portraits.Values.Where(value => value.PortraitId is not null).Select(value => portraits.Resolve(value.PortraitId!))))
            {
                var bytes = Godot.FileAccess.GetFileAsBytes(image.ResourcePath!);
                Require(bytes.Length > 0 && Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant() == image.Sha256, "Original HUD resource hash: " + image.ResourcePath);
            }
            var assets = AssetCatalog.Load();
            var explicitDocument = new TacticalHudArtDocument
            {
                Schema = 2,
                Portraits = new() { ["c01.mirelle"] = new() { UnitType = "c01.mirelle", PortraitId = "explicit-card" } }
            };
            var explicitCatalog = new PortraitCatalog(new Dictionary<string, PortraitAsset> { ["explicit-card"] = portraits.Portraits["mirelle"] });
            var explicitArt = new TacticalHudArt(assets, explicitCatalog, explicitDocument);
            Require(explicitArt.Portrait(new("c01.mirelle", null, "#224466")).ResourcePath == explicitCatalog.Resolve("explicit-card").ResourcePath,
                "HUD uses the injected shared catalog and explicit ID, not unit-name inference");
            try
            {
                _ = new TacticalHudArt(assets, new PortraitCatalog(new Dictionary<string, PortraitAsset> { ["different-card"] = portraits.Portraits["mirelle"] }), explicitDocument);
                throw new Exception("Unknown portrait silently fell back to a frame");
            }
            catch (InvalidOperationException error) when (error.Message.Contains("Unknown portrait ID", StringComparison.Ordinal)) { assertions++; }
            try
            {
                System.Text.Json.JsonSerializer.Deserialize<TacticalPortraitBinding>("{\"unitType\":\"c01.mirelle\"}",
                    new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
                throw new Exception("Omitted portrait ID silently became frame semantics");
            }
            catch (System.Text.Json.JsonException) { assertions++; }
            var art = TacticalHudArt.Load(assets, portraits);
            hud.ConfigureArt(art);
            TextureRect MountedPortrait() => ((PanelContainer)hud.FindChild("InspectedPortrait", true, false)).GetChild<TextureRect>(0);
            foreach (var binding in metadata.Portraits.Values.Where(value => value.PortraitId is not null))
            {
                var namedPortrait = new TacticalHudPortrait(binding.UnitType, binding.ActorKey, "#224466");
                var source = ResourceLoader.Load<Texture2D>(portraits.Resolve(binding.PortraitId!).ResourcePath!);
                var whole = art.Portrait(namedPortrait);
                Require(whole == source && whole.GetSize() == source.GetSize(),
                    "Every named portrait uses the complete catalog image without cropping or padding: " + binding.PortraitId);
                hud.Apply(new(stamp, "", new[] { new TacticalHudSection("unit", "角色", Array.Empty<TacticalHudRow>()) }, namedPortrait));
                var mountedPortrait = MountedPortrait();
                Require(mountedPortrait.Texture == source && mountedPortrait.StretchMode == TextureRect.StretchModeEnum.KeepAspectCentered,
                    "Every named HUD portrait preserves the full image with one aspect-fit policy: " + binding.PortraitId);
            }
            var named = new TacticalHudPortrait("c01.swordsman", "campaign-torren", "#224466");
            var portrait = art.Portrait(named);
            var genericPortrait = new TacticalHudPortrait("c01.swordsman", null, "#224466");
            var generic = art.Portrait(genericPortrait) as AtlasTexture;
            var unit = assets.ResolveUnit(genericPortrait.UnitType, genericPortrait.ActorKey);
            Require(generic is not null && generic == assets.CreateFrameTexture(unit, AssetCatalog.FrameIndex(unit, "idle")),
                "An explicit null portrait retains the canonical generic idle-frame atlas.");
            Require(generic!.Atlas.ResourcePath != portrait.ResourcePath && art.UnitIcon(named) is AtlasTexture,
                "Generic portraits and unit icons remain sprite frames, distinct from whole named portraits.");
            hud.Apply(new(stamp, "", new[] { new TacticalHudSection("unit", "角色", Array.Empty<TacticalHudRow>()) }, genericPortrait));
            var oldPortrait = MountedPortrait();
            for (var index = 0; index < 100; index++)
                hud.Apply(new(stamp, "", new[] { new TacticalHudSection("unit", "角色", Array.Empty<TacticalHudRow>()) }, named));
            Require(oldPortrait.Texture is null && MountedPortrait().Texture == portrait && art.Portrait(named) == portrait,
                "Repeated projections release old control references and reuse the shared whole-image resource.");
            oldPortrait = MountedPortrait();
            var replacement = TacticalHudArt.Load(assets, portraits);
            hud.ConfigureArt(replacement);
            Require(oldPortrait.Texture is null && GodotObject.IsInstanceValid(portrait) && GodotObject.IsInstanceValid(generic),
                "Rebinding releases control references, not shared catalog textures or idle-frame atlases.");
            hud.Apply(new(stamp, "", new[] { new TacticalHudSection("unit", "角色", Array.Empty<TacticalHudRow>()) }, named));
            var lastPortrait = MountedPortrait();
            RemoveChild(hud);
            Require(lastPortrait.Texture is null && replacement.Portrait(named) == portrait && art.Portrait(genericPortrait) == generic,
                "HUD teardown leaves shared named and generic resources usable by their owners.");
            hud.QueueFree();
            var missing = PresentationJson.Load<TacticalHudArtDocument>("res://data/tactical-hud-art.json");
            var required = missing.SourceIconKeys[0];
            missing.Icons.Remove(required);
            var broken = new TacticalHudArt(assets, portraits, missing);
            try
            {
                broken.Icon(required);
                throw new Exception("Missing declared icon silently disappeared");
            }
            catch (MissingArtResourceException) { assertions++; }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print($"TACTICAL_HUD_CHECKS_OK {assertions} assertions (headless native controls, not visual acceptance)");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
