#nullable enable
using System;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Presentation.Feedback;
using Empire.Localization;
using Empire.Core.Engine;
using Empire.Core.Combat;
using Empire.Core.Composition;
using Empire.Core.State;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
using Empire.Presentation.Tactical;
using System.Collections.Generic;
using Godot;

namespace Empire.Presentation;

/// <summary>Native end-to-end UI binding checks using the real application session, never a demo session.</summary>
public partial class BattleScreenChecks : Node
{
    public override void _Ready()
    {
        try
        {
            CheckRecordedStatusFeedback();
            CheckStructureKeyboardTarget();
            CheckFormationInspection();
            CheckCollisionAftermathDisclosure();
            HitEffectProjectionChecks.Check();
            BattleScreenMovementQueryChecks.Check();
            BattleScreenRecruitmentQueryChecks.Check();
            CheckMovedAttackForecast();
            CheckContextualKeyRepeat();
            CheckUndoKeyRepeat();
            Check();
            CheckPresentationTransitions();
            CheckInputContracts();
            CheckCanonicalCellProjection();
            CheckRegisteredReactions();
            CheckLocalizedInteraction();
            BattleIdentityChecks.Check(this);
            CheckInjectedEventPresenter();
            CheckEventReceiptDetails();
            GD.Print("Native BattleScreen real-session integration passed.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void CheckStructureKeyboardTarget()
    {
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == "c01-09");
        var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        var original = EcmaJson.Stringify(session.Snapshot());
        Require(StateDigest.Hash(session.Snapshot()) == "6fefe68e", "The keyboard witness uses the unchanged shipped C01-09 opening.");
        var screen = new BattleScreen();
        AddChild(screen);
        try
        {
            screen.Board.AnimationsEnabled = false;
            screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), session,
                new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)),
                level, AuthoredScene.Load("res://data/scenes/c01-09.json"));
            GetViewport().GuiReleaseFocus();
            void Press(Key key)
            {
                foreach (var pressed in new[] { true, false })
                {
                    using var input = new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed };
                    GetViewport().PushInput(input, true);
                }
            }
            for (var index = 0; index < 4; index++)
                Press(Key.Space);
            Require(screen.Selection is { Kind: TacticalSelectionKind.Unit, UnitId: 4 }, "Native actor cycling reaches Mirelle without pointer input.");
            foreach (var key in new[] { Key.Up, Key.Up, Key.Enter, Key.A, Key.Right, Key.Right })
                Press(key);
            Require(screen.Selection is { Kind: TacticalSelectionKind.Target, Weapon: "c01.lantern-staff" } selection
                && selection.Destination == new BoardCell(3, 6)
                && selection.TargetSubjects.Any(target => target == TacticalTarget.Object(new(5, 6)))
                && session.Revision == 0 && session.Events.Count == 0 && EcmaJson.Stringify(session.Snapshot()) == original,
                "Keyboard navigation must reach an admitted structure target without advancing battle state.");
            var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
            var strike = hud.Projection!.Sections.SingleOrDefault(section => section.Key == "forecast")?.Rows.Single(row => row.Key == "strike");
            var expected = text.Format("ui.hud.strike", 22, 100, 78);
            var shown = Descendants(hud).OfType<Label>().Any(label => label.IsVisibleInTree() && label.Text == expected);
            var preview = screen.Selection!.PreviewTarget;
            Press(Key.Enter);
            GD.Print($"STRUCTURE_KEYBOARD c01-09: target={preview}; forecast={strike?.Label}; visible={shown}; revision={session.Revision}");
            Require(preview == TacticalHit.Object(new(5, 6)) && strike?.Label == expected && strike.Gauge?.Value == 78 && shown
                && session.Revision == 1 && session.ActionLog.Count == 1,
                "The real keyboard path must show the same 22-damage structure forecast and commit once on Enter.");
            var action = session.ActionLog.Single();
            var pointerSession = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
            var pointer = new TacticalSelectionController(pointerSession.GetTacticalQueries(0));
            pointer.Click(pointer.Stamp, TacticalHit.Unit(new(3, 8), 4));
            pointer.Click(pointer.Stamp, TacticalHit.Ground(new(3, 6)));
            pointer.ChooseCommand(pointer.Stamp, "attack:c01.lantern-staff");
            var ground = pointer.Click(pointer.Stamp, TacticalHit.Ground(new(5, 6)));
            Require(ground.Status == TacticalInputStatus.InvalidTarget && ground.Request is null,
                "A pointer ground hit must not be promoted to a structure through an occluder.");
            var request = pointer.Click(pointer.Stamp, TacticalHit.Object(new(5, 6))).Request!;
            Require(EcmaJson.DeepEquals(action, EcmaJson.Parse(request.Action!.Value.GetRawText()))
                && pointerSession.Dispatch(request.Action.Value, 0).Accepted
                && EcmaJson.DeepEquals(session.Snapshot(), pointerSession.Snapshot())
                && EcmaJson.DeepEquals(new JsonArray(session.Events.Select(value => (JsonNode)value.DeepClone()).ToArray()),
                    new JsonArray(pointerSession.Events.Select(value => (JsonNode)value.DeepClone()).ToArray())),
                "Keyboard and actual pointer subjects must produce identical commands, ordered events and final battle state.");
            Require(session.Events.Single(value => value["type"]!.GetValue<string>() == "structureDamaged")["amount"]!.GetValue<double>() == 22
                && session.Snapshot()["structures"]!.AsArray().OfType<JsonObject>().Single(value => value["id"]!.GetValue<string>() == "chain-west")["hp"]!.GetValue<double>() == 78,
                "The real command damages the unchanged west chain by exactly 22 HP.");
        }
        finally
        {
            RemoveChild(screen);
            screen.Free();
        }
    }

    private void CheckFormationInspection()
    {
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == "c01-04");
        var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        Require(StateDigest.Hash(session.Snapshot()) == "c1005293", "Formation inspection starts from the unchanged shipped C01-04 opening.");
        var screen = new BattleScreen();
        AddChild(screen);
        try
        {
            screen.Board.AnimationsEnabled = false;
            screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), session,
                new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)),
                level, AuthoredScene.Load("res://data/scenes/c01-04.json"));
            var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
            void Press(Key key)
            {
                foreach (var pressed in new[] { true, false })
                {
                    using var input = new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed };
                    GetViewport().PushInput(input, true);
                }
            }
            string Formation() => hud.Projection!.Sections.SelectMany(section => section.Rows).Single(row => row.Key == "formation").Label;
            var hit = TacticalHit.Unit(new(17, 20), 1);
            GetViewport().GuiReleaseFocus();
            Press(Key.Space);
            Require(screen.Selection?.UnitId == 1, "Native cycling selects Laiya and establishes the keyboard cursor.");
            var selected = Formation();
            Press(Key.Escape);
            Press(Key.Right);
            Press(Key.Left);
            var inspected = Formation();
            var state = session.Snapshot();
            var unit = state["units"]!.AsArray().OfType<JsonObject>().Single(value => value["id"]!.GetValue<int>() == 1);
            Require(screen.Selection?.Kind == TacticalSelectionKind.Idle && engine.Progression.Formations.Active(state, unit) is not null
                && session.Revision == 0 && StateDigest.Hash(state) == "c1005293",
                "Inspection cannot change Laiya's genuinely active defensive formation or opening state.");
            GD.Print($"FORMATION_INSPECTION c01-04: selected={selected}; inspected={inspected}");
            Require(inspected == selected && !inspected.Contains(text.Format("ui.hud.formation_inactive"), StringComparison.Ordinal),
                "Inspecting the same active formation must not claim missing adjacent allies when no actor is selected.");
            screen.SelectHit(hit);
            screen.SelectHit(hit);
            Press(Key.W);
            Require(session.Revision == 1 && StateDigest.Hash(session.Snapshot()) == "69d77f60", "The unchanged explicit wait commits normally.");
            screen.SelectHit(hit);
            Require(Formation() == selected, "An acted unit's formation remains active and truthful even though it can no longer receive orders.");

            session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
            screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), session,
                new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)),
                level, AuthoredScene.Load("res://data/scenes/c01-04.json"));
            screen.SelectHit(hit);
            screen.SelectHit(TacticalHit.Ground(new(17, 17)));
            Press(Key.W);
            screen.SelectHit(TacticalHit.Unit(new(17, 17), 1));
            state = session.Snapshot();
            unit = state["units"]!.AsArray().OfType<JsonObject>().Single(value => value["id"]!.GetValue<int>() == 1);
            Require(session.Revision == 1 && StateDigest.Hash(state) == "20d93d97"
                && engine.Progression.Formations.Active(state, unit) is null
                && Formation() == text.Format("ui.hud.formation", presentation.Content.Name(ContentTextKind.Formation, EcmaJson.ReadString(unit["formation"])),
                    text.Format("ui.hud.formation_inactive")),
                "Moving away from allies genuinely breaks formation and must retain the inactive warning on inspection.");
        }
        finally
        {
            RemoveChild(screen);
            screen.Free();
        }
    }

    private void CheckCollisionAftermathDisclosure()
    {
        var fixture = PresentationJson.Load<JsonObject>("res://tests/native/c01-14-collision-forecast.json");
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == fixture["levelId"]!.GetValue<string>());
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var battle = engine.CreateBattle(level);
        Require(battle.StateHash == fixture["initialStateHash"]!.GetValue<string>(),
            "The collision witness starts from the unchanged shipped c01-14 level and content.");
        foreach (var action in fixture["prefix"]!.AsArray().OfType<JsonObject>())
            battle.Dispatch(action);
        Require(battle.StateHash == fixture["beforeStateHash"]!.GetValue<string>(),
            "The legal recorded command prefix reaches the original collision witness exactly.");
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var session = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(battle.Snapshot()));
        var original = EcmaJson.Stringify(session.Snapshot());
        var path = new[] { new BoardCell(7, 7), new BoardCell(7, 8), new BoardCell(8, 8) };
        var target = TacticalHit.Unit(new(9, 8), 12);
        var screen = new BattleScreen();
        AddChild(screen);
        try
        {
            screen.Board.AnimationsEnabled = false;
            screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), session,
                new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)),
                level, AuthoredScene.Load("res://data/scenes/c01-14.json"));
            screen.SelectHit(TacticalHit.Unit(path[0], 2));
            screen.SelectHit(target);
            Require(screen.Selection is { Kind: TacticalSelectionKind.Target, Weapon: "c01.border-blade" } preview
                && preview.Path.SequenceEqual(path) && preview.PreviewTarget == target
                && session.Revision == 0 && session.ActionLog.Count == 0 && session.Events.Count == 0
                && EcmaJson.Stringify(session.Snapshot()) == original,
                "The real Torren target preview preserves the exact path and original battle without dispatching.");
            var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
            var forecast = hud.Projection!.Sections.Single(section => section.Key == "forecast");
            var counter = forecast.Rows.Single(row => row.Key == "counter");
            var labels = Descendants(hud).OfType<Label>().Where(label => label.IsVisibleInTree()).Select(label => label.Text).ToArray();
            Require(counter.Gauge?.Value == 70 && counter.Label == text.Format("ui.hud.counter", "28", 98, 70)
                && labels.Contains(counter.Label),
                "The mounted forecast retains the canonical 28 direct counter damage and 70 direct-hit HP.");
            var disclosed = labels.Any(label => label.Contains("反击", StringComparison.Ordinal)
                && label.Contains("10", StringComparison.Ordinal)
                && (label.Contains("碰撞", StringComparison.Ordinal) || label.Contains("受阻", StringComparison.Ordinal)));

            screen.SelectHit(target);
            var settled = session.GetSnapshot();
            Require(session.Revision == 1 && session.ActionLog.Count == 1 && settled.Transition is not null
                && screen.DisplayedRevision == 1 && !screen.IsPresenting
                && EcmaJson.DeepEquals(session.ActionLog.Single(), fixture["action"])
                && StateDigest.Hash(session.Snapshot()) == fixture["afterStateHash"]!.GetValue<string>(),
                "One target confirmation commits exactly the original action and settles the original resulting state.");
            var events = new JsonArray(session.Events.Select(value => (JsonNode)value.DeepClone()).ToArray());
            Require(EcmaJson.Sha256(events) == fixture["eventsSha256"]!.GetValue<string>()
                && session.Events.Single(value => value["type"]!.GetValue<string>() == "counter")["damage"]!.GetValue<double>() == 28
                && session.Events.Single(value => value["type"]!.GetValue<string>() == "forcedMove")["collided"]!.GetValue<bool>()
                && session.Events.Single(value => value["type"]!.GetValue<string>() == "collisionDamage")["amount"]!.GetValue<double>() == 10
                && settled.State.GetProperty("units").EnumerateArray().Single(unit => unit.GetProperty("id").GetInt32() == 2)
                    .GetProperty("hp").GetDouble() == 60,
                "The unchanged receipt adds 10 collision damage after the 28 counter and leaves Torren at 60 HP.");
            GD.Print($"COLLISION_FORECAST c01-14 Torren: preview={counter.Label}; gauge={counter.Gauge?.Value}; receipt counter=28 collision=10 final HP=60; disclosed={disclosed}");
            Require(disclosed,
                "Before confirmation, a visible mounted forecast label must disclose the counter's conditional 10 collision damage, not only the 70 direct-hit HP.");
        }
        finally
        {
            RemoveChild(screen);
            screen.Free();
        }
    }

    private void CheckMovedAttackForecast()
    {
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == "c01-11");
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        var before = session.Snapshot();
        var original = EcmaJson.Stringify(before);
        var actor = before["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["key"]?.GetValue<string>() == "campaign-tasha");
        var defender = before["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["key"]?.GetValue<string>() == "west-ghost");
        var actorId = actor["id"]!.GetValue<int>();
        var defenderId = defender["id"]!.GetValue<int>();
        var target = TacticalHit.Unit(new(6, 10), defenderId);
        var path = new[] { new BoardCell(4, 8), new BoardCell(4, 9), new BoardCell(4, 10) };
        var screen = new BattleScreen();
        AddChild(screen);
        try
        {
            screen.Board.AnimationsEnabled = false;
            screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), session,
                new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)),
                level, AuthoredScene.Load("res://data/scenes/c01-11.json"));
            screen.SelectHit(TacticalHit.Unit(path[0], actorId));
            screen.SelectHit(TacticalHit.Ground(path[^1]));
            Require(screen.Selection is { Kind: TacticalSelectionKind.Destination } destination && destination.Path.SequenceEqual(path),
                "The unchanged c01-11 opening must preview Tasha's legal path from (4,8) to (4,10).");
            // A singleton command button commits immediately; selecting the enemy arms the native forecast first.
            screen.SelectHit(target);
            Require(screen.Selection is { Kind: TacticalSelectionKind.Target, Weapon: "c01.satchel-charge" } preview
                && preview.Path.SequenceEqual(path) && preview.PreviewTarget == target
                && session.Revision == 0 && session.ActionLog.Count == 0 && session.Events.Count == 0
                && EcmaJson.Stringify(session.Snapshot()) == original,
                "Arming the actual moved attack must retain its path and target without changing the battle.");
            var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
            var forecast = hud.Projection!.Sections.Single(section => section.Key == "forecast");
            var strike = forecast.Rows.Single(row => row.Key == "strike");
            var counter = forecast.Rows.Single(row => row.Key == "counter");
            Require(Descendants(hud).OfType<Label>().Any(label => label.Text == strike.Label)
                && Descendants(hud).OfType<Label>().Any(label => label.Text == counter.Label),
                "The mounted native forecast must display both projected damage rows before confirmation.");

            screen.SelectHit(target);
            var settled = session.GetSnapshot();
            Require(session.Revision == 1 && session.ActionLog.Count == 1 && settled.Transition is not null
                && screen.DisplayedRevision == session.Revision && !screen.IsPresenting,
                "One target confirmation must commit exactly one real command and settle its presentation.");
            var receipt = settled.Transition!;
            var action = receipt.Action;
            Require(action.GetProperty("kind").GetString() == "command" && action.GetProperty("unit").GetInt32() == actorId
                && action.GetProperty("path").EnumerateArray().Select(cell => new BoardCell(cell.GetProperty("x").GetInt32(), cell.GetProperty("y").GetInt32())).SequenceEqual(path)
                && action.GetProperty("command").GetProperty("weapon").GetString() == "c01.satchel-charge"
                && action.GetProperty("command").GetProperty("target").GetProperty("x").GetInt32() == target.Cell.X
                && action.GetProperty("command").GetProperty("target").GetProperty("y").GetInt32() == target.Cell.Y,
                "The native preview and dispatched command must describe the same actor, path, weapon and target.");
            var attackEvent = receipt.Events.Single(value => value.GetProperty("type").GetString() == "attack"
                && value.GetProperty("attacker").GetInt32() == actorId && value.GetProperty("defender").GetInt32() == defenderId);
            var counterEvent = receipt.Events.Single(value => value.GetProperty("type").GetString() == "counter"
                && value.GetProperty("attacker").GetInt32() == defenderId && value.GetProperty("defender").GetInt32() == actorId);
            var settledActor = settled.State.GetProperty("units").EnumerateArray().Single(unit => unit.GetProperty("id").GetInt32() == actorId);
            var settledDefender = settled.State.GetProperty("units").EnumerateArray().Single(unit => unit.GetProperty("id").GetInt32() == defenderId);
            var actualStrike = attackEvent.GetProperty("damage").GetDouble();
            var actualCounter = counterEvent.GetProperty("damage").GetDouble();
            var actorHp = settledActor.GetProperty("hp").GetDouble();
            var defenderHp = settledDefender.GetProperty("hp").GetDouble();
            var expectedStrike = text.Format("ui.hud.strike", actualStrike, defender["hp"]!.GetValue<double>(), defenderHp);
            var expectedCounter = text.Format("ui.hud.counter", EcmaJson.Stringify(JsonValue.Create(actualCounter)), actor["hp"]!.GetValue<double>(), actorHp);
            var failures = new List<string>();
            if (strike.Label != expectedStrike)
                failures.Add($"Moved attack preview '{strike.Label}' differs from committed receipt '{expectedStrike}'.");
            if (counter.Label != expectedCounter)
                failures.Add($"Moved counter preview '{counter.Label}' differs from committed receipt '{expectedCounter}'.");
            if (strike.Gauge?.Value != defenderHp)
                failures.Add($"Moved attack forecast HP {strike.Gauge?.Value} differs from committed defender HP {defenderHp}.");
            if (counter.Gauge?.Value != actorHp)
                failures.Add($"Moved counter forecast HP {counter.Gauge?.Value} differs from committed actor HP {actorHp}.");
            Require(settledActor.GetProperty("x").GetInt32() == path[^1].X && settledActor.GetProperty("y").GetInt32() == path[^1].Y
                && settledActor.GetProperty("done").GetBoolean() && settledActor.GetProperty("facing").GetString() == "east",
                "The actual moved attack must finish at the chosen cell facing its target.");
            GD.Print($"MOVED_ATTACK_FORECAST c01-11 Tasha {actorId}: preview={strike.Label}; {counter.Label}; receipt={actualStrike}/{actualCounter}; final HP={defenderHp}/{actorHp}; revision={session.Revision}");
            Require(failures.Count == 0, string.Join("\n", failures));
        }
        finally
        {
            RemoveChild(screen);
            screen.Free();
        }
    }

    private void CheckEventReceiptDetails()
    {
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), BattleTextChecks.OpeningLevel());
        var state = EcmaJson.ToElement(session.Snapshot());
        var context = new BattleLogContext(new(session.UnitName, presentation.Content.UnitName,
            presentation.Content.CareerName, presentation.Content.ResourceName,
            player => presentation.Battle.PlayerName("c01-01", player)), state, state);
        foreach (var component in new[] { "terrain", "unit" })
        {
            var defense = presentation.Modifiers.Describe(new("fixture", "core.defense", component, "mitigation", "add", .3,
                new JsonObject { ["component"] = component }));
            var source = text.Format("ui.hud.modifier_source." + component);
            Require(defense.Split(source, StringSplitOptions.None).Length == 2 && defense.Split("防御", StringSplitOptions.None).Length == 2
                && defense.Contains("+30%", StringComparison.Ordinal), "Defense rows identify their source and amount once, without repeated source/defense labels.");
        }
        var defender = state.GetProperty("units").EnumerateArray().First(unit => unit.GetProperty("owner").GetInt32() == 2).GetProperty("id").GetInt32();
        foreach (var kind in new[] { "attack", "counter", "supportAttack", "partingShot" })
        {
            var value = new JsonObject { ["type"] = kind, ["attacker"] = 2, ["defender"] = defender, ["damage"] = 17, ["killed"] = false };
            var line = presentation.Events.Describe(context, EcmaJson.ToElement(value));
            Require(line.Contains(context.UnitName(2), StringComparison.Ordinal) && line.Contains(context.UnitName(defender), StringComparison.Ordinal)
                && line.Contains("17", StringComparison.Ordinal), "Every direct combat receipt identifies both participants and the committed damage.");
        }
        var heal = presentation.Events.Describe(context, EcmaJson.ToElement(new JsonObject { ["type"] = "heal", ["source"] = 2, ["target"] = defender, ["amount"] = 9 }));
        Require(heal.Contains(context.UnitName(2), StringComparison.Ordinal) && heal.Contains(context.UnitName(defender), StringComparison.Ordinal),
            "Healing receipts identify source and recipient instead of making the patient ambiguous.");
        var collision = presentation.Events.Describe(context, EcmaJson.ToElement(new JsonObject { ["type"] = "collisionDamage", ["unit"] = defender, ["amount"] = 11, ["hpAfter"] = 7, ["killed"] = false }));
        Require(collision.Contains(context.UnitName(defender), StringComparison.Ordinal) && collision.Contains("11", StringComparison.Ordinal)
            && collision.Contains("7", StringComparison.Ordinal), "Collision feedback uses its committed hpAfter, not a guessed strike total.");
        var moved = presentation.Events.Describe(context, EcmaJson.ToElement(new JsonObject
        {
            ["type"] = "forcedMove",
            ["unit"] = defender,
            ["from"] = new JsonObject { ["x"] = 3, ["y"] = 4 },
            ["to"] = new JsonObject { ["x"] = 5, ["y"] = 6 }
        }));
        Require(moved.Contains("(3, 4)", StringComparison.Ordinal) && moved.Contains("(5, 6)", StringComparison.Ordinal), "Forced displacement remains visible as a distinct committed event.");
    }

    private void CheckCanonicalCellProjection()
    {
        foreach (var canonicalOutside in new[] { false, true })
        {
            var level = BattleTextChecks.OpeningLevel();
            level["rules"]!["fog"] = true;
            var packs = PresentationJson.Load<JsonArray>("res://data/content-packs.json");
            var source = new BattleEngine(packs).CreateState(level);
            var sourceActor = source["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["id"]!.GetValue<int>() == 2);
            var actorIndex = sourceActor["y"]!.GetValue<int>() * level["width"]!.GetValue<int>() + sourceActor["x"]!.GetValue<int>();
            var actorTerrain = source["map"]!["tiles"]![actorIndex]!.GetValue<string>();
            var terrain = packs.OfType<JsonObject>().SelectMany(pack => pack["terrains"]?.AsArray().OfType<JsonObject>() ?? [])
                .Single(definition => EcmaJson.ReadString(definition["id"]) == actorTerrain);
            if (canonicalOutside)
                terrain["tags"]!.AsArray().Add("outside");
            var engine = new BattleEngine(packs);
            var text = GodotTextCatalog.LoadSimplifiedChinese();
            var presentation = BattleTextChecks.Session(engine, text);
            var battleText = presentation.Battle;
            var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
            var stateBefore = EcmaJson.Stringify(session.Snapshot());
            var queries = session.GetTacticalQueries(0);
            var actor = queries.Unit(2)!;
            var stops = queries.MoveStops(2).ToArray();
            var screen = new BattleScreen();
            AddChild(screen);
            screen.Board.AnimationsEnabled = false;
            var scene = new AuthoredScene
            {
                LevelId = level["id"]!.GetValue<string>(),
                MapSize = [level["width"]!.GetValue<int>(), level["height"]!.GetValue<int>()],
                FramePlacements = []
            };
            screen.BindSession(new(text, battleText, PortraitCatalogResources.Load(), session, new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)), level, scene);
            var materials = WorldMaterialsManifest.Load();
            materials.TerrainTags[actorTerrain] = canonicalOutside ? [] : ["outside"];
            screen.ConfigureGround(TerrainView.Factory(materials));

            screen.SelectHit(TacticalHit.Unit(actor.At, actor.Id));
            Require((screen.SelectedUnitId == actor.Id) == !canonicalOutside,
                "Canonical terrain must govern selection when material tags disagree.");
            screen._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = Key.Escape });
            screen._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = Key.Space });
            Require(screen.SelectedUnitId is not { } selected || queries.InteractiveCells.Contains(queries.Unit(selected)!.At),
                "Native actor cycling cannot select a canonical outside cell.");

            var overlay = (BoardOverlay)screen.FindChild("LegalTargets", true, false);
            var visible = session.GetSnapshot().VisibleCells ?? throw new InvalidOperationException("Fog fixture has no visibility projection.");
            var fog = overlay.RangeOrder().Where(entry => entry.Role == "fog").Select(entry => entry.Cell).ToHashSet();
            Require(fog.SetEquals(queries.InteractiveCells.Where(cell => !visible.Contains(cell))),
                "Rendered fog must consume the exact canonical interactive-cell projection.");
            var state = session.Snapshot();
            var width = state["map"]!["width"]!.GetValue<int>();
            var unseenTerrain = state["map"]!["tiles"]!.AsArray().Select((tile, index) => (tile, at: new BoardCell(index % width, index / width)))
                .First(entry => EcmaJson.ReadString(entry.tile) == actorTerrain && !visible.Contains(entry.at)).at;
            Require(fog.Contains(unseenTerrain) == !canonicalOutside,
                "Material outside metadata cannot add or remove fog from a canonical cell.");
            Require(queries.MoveStops(2).SequenceEqual(stops) && EcmaJson.Stringify(session.Snapshot()) == stateBefore,
                "Changing presentation metadata cannot change legality or battle state.");
            RemoveChild(screen);
            screen.Free();
        }
    }

    private void CheckInjectedEventPresenter()
    {
        var level = BattleTextChecks.OpeningLevel();
        level["rules"]!["baseResourceGrants"] = new JsonArray(new JsonObject
        {
            ["resource"] = "funds",
            ["amount"] = 3,
        });
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var defaults = BattleTextChecks.Session(engine, text);
        const string customLine = "custom resource presenter reached the battle log";
        var presentation = defaults with
        {
            Events = defaults.Events.Clone().Replace("resourceChanged", (_, _) => customLine).Seal(),
        };
        var fresh = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        var restored = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), fresh.Save());
        foreach (var session in new[] { fresh, restored })
        {
            var screen = new BattleScreen();
            AddChild(screen);
            screen.Board.AnimationsEnabled = false;
            screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), session, new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)), level, AuthoredScene.Load("res://data/scenes/c01-01.json"));
            Buttons(screen).Single(button => button.Name == "EndTurn").EmitSignal(Button.SignalName.Pressed);
            Require(session.GetSnapshot().Transition!.Events.Any(value => value.GetProperty("type").GetString() == "resourceChanged"),
                "The real end-turn action must emit a resource event for the custom presenter.");
            Require(screen.RecentMessages.Contains(customLine),
                "Fresh and loaded sessions must render their injected event presenter, not rebuilt defaults.");
            RemoveChild(screen);
            screen.Free();
        }
        Require(EcmaJson.DeepEquals(fresh.Snapshot(), restored.Snapshot()),
            "Injecting event wording cannot change gameplay across fresh and loaded sessions.");
    }

    private void CheckPresentationTransitions()
    {
        var level = BattleTextChecks.OpeningLevel();
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var battleText = presentation.Battle;
        var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        var screen = new BattleScreen();
        AddChild(screen);
        screen.Board.AdvanceAnimationsAutomatically = false;
        void Bind(GrayFlagSession value) => screen.BindSession(new(text, battleText, PortraitCatalogResources.Load(), value, new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)), level, AuthoredScene.Load("res://data/scenes/c01-01.json"));
        Bind(session);
        var projectionMounts = 0;
        screen.Board.ChildEnteredTree += child =>
        {
            if (child.Name == "CanonicalProjection")
                projectionMounts++;
        };
        Require(screen.RecentMessages.SequenceEqual(new[] { session.GetSnapshot().InitialLogMessage }),
            "New battle log must contain only the original opening line.");
        var destination = TacticalSceneInputChecks.Move(screen, session);
        Require(screen.RecentMessages.Count == 1, "Event text must wait until its animation completes.");
        Require(screen.IsPresenting && screen.Board.IsPresenting, "Accepted move must hold the presentation gate.");
        var revision = session.GetSnapshot().Revision;
        screen.SelectHit(TacticalHit.Ground(destination));
        screen.SelectHit(TacticalHit.Unit(session.GetTacticalQueries(0).Unit(2)!.At, 2));
        screen.Refresh();
        Require(session.GetSnapshot().Revision == revision && screen.SelectedUnitId is null,
            "Repeated clicks and refresh must not dispatch or change selection during playback.");
        for (var step = 0; step < 100 && screen.IsPresenting; step++)
            screen.Board.AdvancePresentation(0.1);
        Require(!screen.IsPresenting && !screen.Board.IsPresenting && screen.DisplayedRevision == revision,
            "Timeline completion must settle the committed revision and release input.");
        Require(projectionMounts == 2,
            "A successful move mounts its before projection once and its settled projection once; screen synchronization must not mount after a second time.");
        var expectedLog = new Feedback.BattleLog();
        expectedLog.Add(session.GetSnapshot().InitialLogMessage!);
        foreach (var line in session.GetSnapshot().Transition!.EventDescriptions)
            expectedLog.Add(line);
        Require(screen.RecentMessages.SequenceEqual(expectedLog.Recent()), "Descriptions must append once in receipt order after presentation.");
        screen.Refresh();
        Require(!screen.IsPresenting && screen.RecentMessages.SequenceEqual(expectedLog.Recent()), "Refreshing a settled revision must not replay it.");
        var superseded = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        var replaceable = new ReplaceableSession(superseded);
        screen.BindSession(new(text, battleText, PortraitCatalogResources.Load(), replaceable, new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)), level, AuthoredScene.Load("res://data/scenes/c01-01.json"));
        TacticalSceneInputChecks.Move(screen, replaceable);
        var replacement = superseded.GetSnapshot() with
        {
            Revision = 9,
            Transition = null
        };
        replaceable.Replacement = replacement;
        // Let the earlier playback finish before any external Refresh. A newer live
        // revision must still render, not adopt the obsolete settled projection.
        var mountsBeforeCompletion = projectionMounts;
        screen.Board.AdvancePresentation(100);
        Require(projectionMounts == mountsBeforeCompletion + 2,
            "A superseded successful playback renders its settlement and then the newer live snapshot.");
        Require(!screen.IsPresenting && screen.DisplayedRevision == 9,
            "A newer transition-free snapshot must invalidate older async completion.");
        screen.Refresh();
        Require(!screen.IsPresenting, "A transition-free revision must remain consumed.");
        var canceled = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        Bind(canceled);
        TacticalSceneInputChecks.Move(screen, canceled);
        var mountsBeforeCancel = projectionMounts;
        screen.Board.CancelPresentation();
        Require(projectionMounts > mountsBeforeCancel,
            "Canceled playback still renders committed state rather than taking the successful-settlement adoption path.");
        Require(!screen.IsPresenting && screen.DisplayedRevision == canceled.GetSnapshot().Revision,
            "Same-session cancellation must settle both board and controls to committed state.");
        var fresh = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        Bind(fresh);
        TacticalSceneInputChecks.Move(screen, fresh);
        Require(screen.IsPresenting, "Second timeline did not start.");
        var loaded = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), fresh.Save());
        Bind(loaded);
        Require(!screen.IsPresenting && !screen.Board.IsPresenting && screen.DisplayedRevision == 0,
            "Rebinding a loaded session must cancel old playback without replaying its history.");
        screen.BindSession(new(text, battleText, PortraitCatalogResources.Load(), loaded, new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)), level, AuthoredScene.Load("res://data/scenes/c01-01.json"), initialLogMessage: "已读取第 1 回合的存档");
        Require(screen.RecentMessages.SequenceEqual(new[] { "已读取第 1 回合的存档" }), "Load must clear history and keep one load line.");
        screen.Board.AdvancePresentation(100);
        Require(screen.DisplayedRevision == 0, "Stale timeline completion overwrote the rebound view.");
        var last = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        Bind(last);
        TacticalSceneInputChecks.Move(screen, last);
        var saves = 0;
        screen.BindApplicationActions(() => saves++);
        var staleSave = Buttons(screen).Single(button => button.Text == "保存");
        var lastRevision = last.GetSnapshot().Revision;
        RemoveChild(screen);
        staleSave.EmitSignal(Button.SignalName.Pressed);
        Require(saves == 0, "Unmounted application callbacks must not save or load.");
        screen.SelectHit(TacticalHit.Unit(session.GetTacticalQueries(0).Unit(2)!.At, 2));
        screen.SelectHit(TacticalHit.Ground(destination));
        Require(last.GetSnapshot().Revision == lastRevision, "Unmounted callbacks must not dispatch.");
        Require(!screen.IsPresenting && !screen.Board.IsPresenting, "Unmount must release all presentation waiters.");
        screen.QueueFree();
    }

    private sealed class ReplaceableSession(ITacticalBattleSession inner) : ITacticalBattleSession
    {
        public long Revision => Replacement?.Revision ?? inner.Revision;
        public BattleSnapshot? Replacement
        {
            get; set;
        }
        public ITacticalSelectionQueries GetTacticalQueries(long binding) => new StampedQueries(inner.GetTacticalQueries(binding), new(binding, Revision));
        public TacticalHudProjection ProjectHud(TacticalStamp stamp, TacticalSelection selection, TacticalHit? cursor, int? inspectId, bool busy)
        {
            var source = inner.ProjectHud(stamp with
            {
                Revision = inner.Revision
            }, selection, cursor, inspectId, busy);
            return source with
            {
                Stamp = stamp,
                Sections = source.Sections.Select(section => section with
                {
                    Rows = section.Rows.Select(row => row with { Intent = row.Intent is { } intent ? intent with { Stamp = stamp } : null }).ToArray()
                }).ToArray()
            };
        }
        public BattleDispatchResult Undo(long expectedRevision) => inner.Undo(expectedRevision);

        public BattleSnapshot GetSnapshot() => Replacement ?? inner.GetSnapshot();
        public BattleDispatchResult Dispatch(System.Text.Json.JsonElement action, long expectedRevision) => inner.Dispatch(action, expectedRevision);
    }

    private void Check()
    {
        Require(typeof(BattleScreen).GetMethod(nameof(BattleScreen.BindSession))!.GetParameters()[0].ParameterType == typeof(BattleScreenRules) && typeof(BattleScreenRules).GetProperty(nameof(BattleScreenRules.Session))!.PropertyType == typeof(ITacticalBattleSession),
            "BattleScreen must declare its complete consumer port rather than discover a hidden capability at runtime.");
        var level = BattleTextChecks.OpeningLevel();
        var packs = PresentationJson.Load<JsonArray>("res://data/content-packs.json");
        var engine = new BattleEngine(packs);
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var battleText = presentation.Battle;
        var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        var screen = new BattleScreen();
        screen.Board.AnimationsEnabled = false; // This suite isolates synchronous binding, not timeline behavior.
        AddChild(screen);
        var scene = AuthoredScene.Load("res://data/scenes/c01-01.json");
        var rendering = BoardSceneRendering.Load(scene, level, engine.CreateState(level)["map"]!.AsObject());
        screen.BindSession(new(text, battleText, PortraitCatalogResources.Load(), session, new(AssetCatalog.Load(), EnvironmentCatalog.Load(),
            engine.Grids.Get("square4"), engine.Catalog, rendering.Plane)), level, scene);
        screen.ConfigureGround(rendering.Ground);
        Require(screen.GetNode<ColorRect>("Backdrop").ZIndex < -300,
            "The screen backdrop must be below all negative-depth terrain layers.");
        var before = session.GetSnapshot();
        var beforeState = before.State.GetRawText();
        screen.SelectHit(TacticalHit.Unit(session.GetTacticalQueries(0).Unit(2)!.At, 2));
        Require(screen.SelectedUnitId == 2 && session.GetSnapshot().Revision == before.Revision,
            "Selecting Torren must not advance canonical state.");
        var queries = session.GetTacticalQueries(0);
        var destination = queries.MoveStops(2).First(cell => cell != session.GetTacticalQueries(0).Unit(2)!.At
            && queries.CommandsAt(2, cell) is { Count: 1 } commands && commands[0].SelfTargeted);
        var staleTurnButton = Buttons(screen).Single(button => button.Text == "结束回合");
        screen.SelectHit(TacticalHit.Ground(destination));
        Require(session.Revision == before.Revision && screen.Selection?.Kind == TacticalSelectionKind.Destination,
            "Even a sole self-targeted command first opens a reversible destination preview.");
        screen._UnhandledKeyInput(new InputEventKey { Keycode = Key.W, Pressed = true });
        var after = session.GetSnapshot();
        Require(after.Revision == before.Revision + 1 && screen.DisplayedRevision == after.Revision,
            "An explicit wait commits the preview through the canonical controller command.");
        var actor = after.State.GetProperty("units").EnumerateArray()
            .Single(unit => unit.GetProperty("id").GetInt32() == 2);
        Require(actor.GetProperty("x").GetInt32() == destination.X
            && actor.GetProperty("y").GetInt32() == destination.Y
            && actor.GetProperty("done").GetBoolean(),
            "The real destination selection must commit the complete wait-at-destination command.");
        staleTurnButton.EmitSignal(Button.SignalName.Pressed);
        Require(session.GetSnapshot().Revision == after.Revision, "A callback rendered before dispatch cannot end the newer turn.");
        var stale = session.Dispatch(after.Transition!.Action, before.Revision);
        Require(!stale.Accepted && session.GetSnapshot().Revision == after.Revision,
            "An obsolete UI revision must not replay a command.");
        Require(before.State.GetRawText() == beforeState,
            "Earlier detached snapshots must remain immutable.");
        var turnButton = Buttons(screen).Single(button => button.Name == "EndTurn");
        turnButton.EmitSignal(Button.SignalName.Pressed);
        var nextTurn = session.GetSnapshot();
        Require(nextTurn.Revision == after.Revision + 1
            && nextTurn.State.GetProperty("currentPlayer").GetInt32() != after.State.GetProperty("currentPlayer").GetInt32()
            && screen.DisplayedRevision == nextTurn.Revision,
            "The sole turn button must commit through the real session and refresh current player.");
        screen.QueueFree();
    }
    private void CheckContextualKeyRepeat()
    {
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var level = BattleTextChecks.OpeningLevel();
        var attackLevel = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json")
            .OfType<JsonObject>().Single(value => value["id"]!.GetValue<string>() == "c01-08");
        var failures = new List<string>();
        BattleScreen Mount(GrayFlagSession session, JsonObject battleLevel)
        {
            var screen = new BattleScreen();
            AddChild(screen);
            screen.Board.AnimationsEnabled = false;
            screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), session,
                new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)),
                battleLevel, AuthoredScene.Load($"res://data/scenes/{battleLevel["id"]!.GetValue<string>()}.json"));
            GetViewport().GuiReleaseFocus();
            return screen;
        }
        void KeyEvent(Key key, bool pressed = true, bool echo = false)
        {
            using var input = new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed, Echo = echo };
            GetViewport().PushInput(input, true);
        }
        (TacticalUnit Actor, TacticalHit Target) Attack(ITacticalSelectionQueries queries)
        {
            foreach (var id in queries.Actors())
            {
                var actor = queries.Unit(id)!;
                foreach (var at in queries.MoveStops(id))
                    foreach (var command in queries.CommandsAt(id, at).Where(command => command.Weapon is not null && command.Hostile))
                        foreach (var target in command.TargetSubjects)
                            if (target is { Kind: TacticalTargetKind.Unit, UnitId: { } targetId }
                                && queries.Unit(targetId) is not null && queries.AreEnemies(id, targetId))
                                return (actor, TacticalHit.Unit(target.Cell, targetId));
            }
            throw new InvalidOperationException("The unchanged C01-08 opening needs a real reachable enemy for the held-confirmation probe.");
        }
        void FocusTarget(ITacticalSelectionQueries queries, BoardCell start, BoardCell target)
        {
            var previous = new Dictionary<BoardCell, BoardCell> { [start] = start };
            var queue = new Queue<BoardCell>();
            queue.Enqueue(start);
            while (queue.TryDequeue(out var cell) && !previous.ContainsKey(target))
                foreach (var next in queries.DirectionSteps(cell).Where(queries.InteractiveCells.Contains))
                    if (previous.TryAdd(next, cell))
                        queue.Enqueue(next);
            Require(previous.ContainsKey(target), "The real enemy must be reachable by the board's canonical keyboard cursor.");
            var route = new Stack<BoardCell>();
            for (var cell = target; cell != start; cell = previous[cell])
                route.Push(cell);
            var from = start;
            foreach (var cell in route)
            {
                var key = cell.X > from.X ? Key.Right : cell.X < from.X ? Key.Left : cell.Y > from.Y ? Key.Down : Key.Up;
                KeyEvent(key);
                KeyEvent(key, false);
                from = cell;
            }
        }

        foreach (var confirmation in new[] { Key.Enter, Key.KpEnter })
        {
            var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), attackLevel);
            var screen = Mount(session, attackLevel);
            var stage = "setup";
            try
            {
                var queries = session.GetTacticalQueries(0);
                var attack = Attack(queries);
                var before = EcmaJson.Stringify(session.Snapshot());
                screen.SelectHit(TacticalHit.Unit(attack.Actor.At, attack.Actor.Id));
                FocusTarget(queries, attack.Actor.At, attack.Target.Cell);
                Require(GetViewport().GuiGetFocusOwner() is null && screen.Selection?.Kind == TacticalSelectionKind.Unit
                    && session.Revision == 0 && session.Events.Count == 0,
                    "Native arrows focus the real enemy without a native button taking ownership or issuing an order.");
                KeyEvent(confirmation);
                var preview = screen.Selection;
                Require(preview is { Kind: TacticalSelectionKind.Target } && preview.PreviewTarget == attack.Target
                    && session.Revision == 0 && session.Events.Count == 0 && EcmaJson.Stringify(session.Snapshot()) == before,
                    $"The first fresh {confirmation} press must arm an actual legal attack without committing it.");
                stage = "held confirmation";
                for (var repeat = 0; repeat < 3; repeat++)
                    KeyEvent(confirmation, echo: true);
                var unchanged = session.Revision == 0 && session.Events.Count == 0 && session.ActionLog.Count == 0
                    && EcmaJson.Stringify(session.Snapshot()) == before && screen.Selection == preview;
                GD.Print($"KEY_REPEAT_PROBE {confirmation}: level=c01-08, actor={attack.Actor.Id}, target={attack.Target.UnitId}, revision={session.Revision}, selection={screen.Selection?.Kind}, unchanged={unchanged}");
                if (!unchanged)
                {
                    failures.Add($"Held {confirmation} crossed attack preview into a command: revision {session.Revision}, expected 0.");
                    KeyEvent(confirmation, false);
                    continue;
                }
                stage = "fresh confirmation";
                KeyEvent(confirmation, false);
                KeyEvent(confirmation);
                KeyEvent(confirmation, false);
                var action = session.ActionLog.Single();
                Require(session.Revision == 1 && session.Events.Count > 0 && action["kind"]!.GetValue<string>() == "command"
                    && action["unit"]!.GetValue<int>() == attack.Actor.Id
                    && action["command"]!["ability"]!.GetValue<string>() == "attack"
                    && action["command"]!["weapon"]!.GetValue<string>() == preview!.Weapon
                    && action["command"]!["target"]!["x"]!.GetValue<int>() == attack.Target.Cell.X
                    && action["command"]!["target"]!["y"]!.GetValue<int>() == attack.Target.Cell.Y,
                    $"Releasing and freshly pressing {confirmation} still commits exactly one canonical attack.");
            }
            catch (Exception error)
            {
                failures.Add($"{confirmation} probe {stage} failed: {error.Message}");
            }
            finally
            {
                RemoveChild(screen);
                screen.Free();
            }
        }

        var movementSession = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        var movementScreen = Mount(movementSession, level);
        var movementStage = "setup";
        try
        {
            var queries = movementSession.GetTacticalQueries(0);
            var actor = queries.Actors().Select(id => queries.Unit(id)!).First(unit =>
            {
                var stops = queries.MoveStops(unit.Id);
                return new[] { new BoardCell(unit.At.X, unit.At.Y - 1), new BoardCell(unit.At.X, unit.At.Y - 2) }
                    .All(at => queries.InteractiveCells.Contains(at) && stops.Contains(at) && queries.UnitAt(at) is null);
            });
            var destination = new BoardCell(actor.At.X, actor.At.Y - 2);
            var before = EcmaJson.Stringify(movementSession.Snapshot());
            movementScreen.SelectHit(TacticalHit.Unit(actor.At, actor.Id));
            KeyEvent(Key.W);
            KeyEvent(Key.W, echo: true);
            Require(movementScreen.Selection?.Kind == TacticalSelectionKind.Unit && movementSession.Revision == 0,
                "A fresh W and a held W repeat remain ordinary cursor navigation before a destination is selected.");
            KeyEvent(Key.Enter);
            KeyEvent(Key.Enter, false);
            var preview = movementScreen.Selection;
            Require(preview is { Kind: TacticalSelectionKind.Destination } && preview.Destination == destination
                && movementSession.Revision == 0 && movementSession.Events.Count == 0
                && EcmaJson.Stringify(movementSession.Snapshot()) == before,
                "Fresh Enter chooses the real reachable destination while W remains held, without moving the actor.");
            movementStage = "held navigation after destination selection";
            KeyEvent(Key.W, echo: true);
            var unchanged = movementSession.Revision == 0 && movementSession.Events.Count == 0 && movementSession.ActionLog.Count == 0
                && EcmaJson.Stringify(movementSession.Snapshot()) == before && movementScreen.Selection == preview;
            GD.Print($"KEY_REPEAT_PROBE W: actor={actor.Id}, destination={destination}, revision={movementSession.Revision}, selection={movementScreen.Selection?.Kind}, unchanged={unchanged}");
            if (!unchanged)
                failures.Add($"Held navigation W became Wait after destination selection: revision {movementSession.Revision}, expected 0.");
            KeyEvent(Key.W, false);
            if (unchanged)
            {
                movementStage = "fresh Wait command";
                KeyEvent(Key.W);
                KeyEvent(Key.W, false);
                var action = movementSession.ActionLog.Single();
                var moved = movementSession.Snapshot()["units"]!.AsArray().OfType<JsonObject>()
                    .Single(unit => unit["id"]!.GetValue<int>() == actor.Id);
                Require(movementSession.Revision == 1 && action["kind"]!.GetValue<string>() == "command"
                    && action["command"]!["ability"]!.GetValue<string>() == "wait"
                    && moved["x"]!.GetValue<int>() == destination.X && moved["y"]!.GetValue<int>() == destination.Y
                    && moved["done"]!.GetValue<bool>(),
                    "A new physical W press still commits exactly one move-and-wait at the previewed destination.");
            }
        }
        catch (Exception error)
        {
            failures.Add($"W probe {movementStage} failed: {error.Message}");
        }
        finally
        {
            RemoveChild(movementScreen);
            movementScreen.Free();
        }
        Require(failures.Count == 0, string.Join("\n", failures));
    }

    private void CheckUndoKeyRepeat()
    {
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var level = BattleTextChecks.OpeningLevel();
        var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        var screen = new BattleScreen();
        AddChild(screen);
        screen.Board.AnimationsEnabled = false;
        var rules = new BattleScreenRules(text, presentation.Battle, PortraitCatalogResources.Load(), session,
            new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric));
        var scene = AuthoredScene.Load("res://data/scenes/c01-01.json");
        screen.BindSession(rules, level, scene);
        var original = EcmaJson.Stringify(session.Snapshot());
        TacticalSceneInputChecks.Move(screen, session, 2);
        var first = EcmaJson.Stringify(session.Snapshot());
        var firstEvents = session.Events.Count;
        TacticalSceneInputChecks.Move(screen, session, 3);
        Require(session.Revision == 2 && session.CanUndo && EcmaJson.Stringify(session.Snapshot()) != first,
            "The held-Undo fixture commits two distinct reversible orders through the actual battle screen.");
        var second = EcmaJson.Stringify(session.Snapshot());
        screen.SetApplicationBusy(true);
        screen._Input(new InputEventKey { Keycode = Key.U, Pressed = true });
        screen._Input(new InputEventKey { Keycode = Key.U, Pressed = true, Echo = true });
        Require(session.Revision == 2 && EcmaJson.Stringify(session.Snapshot()) == second && session.CanUndo,
            "Busy input cannot consume either available undo checkpoint.");
        screen.SetApplicationBusy(false);
        screen._Input(new InputEventKey { Keycode = Key.U, Pressed = true, Echo = true });
        Require(session.Revision == 2 && EcmaJson.Stringify(session.Snapshot()) == second && session.CanUndo,
            "A held U cannot turn into a new undo when the busy gate reopens.");
        screen._Input(new InputEventKey { Keycode = Key.U, Pressed = true });
        Require(session.Revision == 3 && EcmaJson.Stringify(session.Snapshot()) == first && session.Events.Count == firstEvents && session.CanUndo,
            "One initial U press restores exactly the most recent checkpoint and leaves the older order undoable.");
        var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
        var projection = hud.Projection;
        var messages = screen.RecentMessages.ToArray();
        foreach (var windowInput in new[] { true, false })
            for (var repeat = 0; repeat < 3; repeat++)
            {
                using var input = new InputEventKey { Keycode = Key.U, Pressed = true, Echo = true };
                if (windowInput)
                    screen._Input(input);
                else
                    screen._UnhandledKeyInput(input);
                Require(session.Revision == 3 && EcmaJson.Stringify(session.Snapshot()) == first && session.Events.Count == firstEvents
                    && session.CanUndo && ReferenceEquals(hud.Projection, projection) && screen.RecentMessages.SequenceEqual(messages),
                    $"Held U must not consume an older checkpoint through {(windowInput ? "window" : "unhandled")} input: revision {session.Revision}, expected 3.");
            }
        screen._Input(new InputEventKey { Keycode = Key.U, Pressed = false });
        Require(session.Revision == 3 && session.CanUndo, "Releasing U cannot dispatch an undo.");
        screen._Input(new InputEventKey { Keycode = Key.U, Pressed = true });
        Require(session.Revision == 4 && EcmaJson.Stringify(session.Snapshot()) == original && !session.CanUndo,
            "A second distinct U press still restores the older checkpoint exactly.");
        screen._Input(new InputEventKey { Keycode = Key.U, Pressed = true, Echo = true });
        screen._Input(new InputEventKey { Keycode = Key.U, Pressed = false });
        screen._Input(new InputEventKey { Keycode = Key.U, Pressed = true });
        Require(session.Revision == 4 && EcmaJson.Stringify(session.Snapshot()) == original,
            "Repeats, releases and fresh presses remain harmless when the undo history is empty.");
        var queries = session.GetTacticalQueries(0);
        var origin = queries.Unit(2)!.At;
        var stops = queries.MoveStops(2).ToHashSet();
        var destination = queries.DirectionSteps(origin).First(at => stops.Contains(at) && queries.UnitAt(at) is null);
        var arrow = destination.X > origin.X ? Key.Right : destination.X < origin.X ? Key.Left
            : destination.Y > origin.Y ? Key.Down : Key.Up;
        var letter = arrow == Key.Right ? Key.D : arrow == Key.Left ? Key.A : arrow == Key.Down ? Key.S : Key.W;
        foreach (var navigation in new[] { arrow, letter })
        {
            screen.BindSession(rules, level, scene);
            screen.SelectHit(TacticalHit.Unit(origin, 2));
            Require(screen.Selection?.Kind == TacticalSelectionKind.Unit,
                "Each held-navigation case begins with a fresh binding and ordinary unit selection.");
            screen._Input(new InputEventKey { Keycode = navigation, Pressed = true, Echo = true });
            screen._Input(new InputEventKey { Keycode = Key.Enter, Pressed = true });
            Require(screen.Selection is { Kind: TacticalSelectionKind.Destination } selection && selection.Destination == destination
                && session.Revision == 4 && EcmaJson.Stringify(session.Snapshot()) == original,
                "Held arrows and WASD still move the cursor into an ordinary reversible destination preview.");
        }
        RemoveChild(screen);
        screen.Free();
    }

    private void CheckInputContracts()
    {
        var level = BattleTextChecks.OpeningLevel();
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var battleText = presentation.Battle;
        var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        var screen = new BattleScreen();
        AddChild(screen);
        screen.Board.AnimationsEnabled = false;
        screen.BindSession(new(text, battleText, PortraitCatalogResources.Load(), session, new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)), level, AuthoredScene.Load("res://data/scenes/c01-01.json"));
        void Keypress(Key key) => screen._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = key });
        void ModifiedKeys(params Key[] keys)
        {
            var before = EcmaJson.Stringify(session.Snapshot());
            var revision = session.Revision;
            var selection = screen.Selection;
            var focus = GetViewport().GuiGetFocusOwner();
            var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
            var projection = hud.Projection;
            foreach (var modifier in new[] { "Control", "Alt", "Meta" })
                foreach (var key in keys)
                    foreach (var windowInput in new[] { true, false })
                    {
                        using var input = new InputEventKey
                        {
                            Pressed = true,
                            Keycode = key,
                            PhysicalKeycode = key,
                            CtrlPressed = modifier == "Control",
                            AltPressed = modifier == "Alt",
                            MetaPressed = modifier == "Meta"
                        };
                        if (windowInput)
                            screen._Input(input);
                        else
                            screen._UnhandledKeyInput(input);
                        Require(session.Revision == revision && EcmaJson.Stringify(session.Snapshot()) == before
                            && ReferenceEquals(screen.Selection, selection) && ReferenceEquals(hud.Projection, projection)
                            && GetViewport().GuiGetFocusOwner() == focus,
                            $"{modifier}+{key} must not become a bare battlefield shortcut or change focus through {(windowInput ? "window" : "unhandled")} input.");
                    }
        }
        var snapshot = session.GetSnapshot();
        var stableSameRevision = Buttons(screen).Single(button => button.Text == "结束回合");
        Keypress(Key.Space);
        Require(screen.SelectedUnitId == session.GetTacticalQueries(0).Actors()[0], "Space selects first source-ordered actor through native keyboard input.");
        screen._Input(new InputEventKey { Pressed = true, Keycode = Key.Space, ShiftPressed = true });
        Require(screen.SelectedUnitId == session.GetTacticalQueries(0).Actors()[1] && session.Revision == snapshot.Revision,
            "Shift alone preserves existing actor cycling without committing a battle order.");
        var keyboardSelection = screen.Selection;
        var systemButton = (Button)screen.FindChild("SystemMenuButton", true, false);
        systemButton.GrabFocus();
        Keypress(Key.Enter);
        Keypress(Key.Space);
        Keypress(Key.Right);
        Require(screen.Selection == keyboardSelection && session.Revision == snapshot.Revision && systemButton.HasFocus(),
            "Focused native controls retain Enter, Space and directional input without issuing battlefield orders.");
        Keypress(Key.Escape);
        Require(!systemButton.HasFocus() && screen.Selection == keyboardSelection,
            "Escape returns native control focus to the map without discarding the pending selection.");
        Keypress(Key.Tab);
        Require(screen.Selection == keyboardSelection, "Tab is reserved for native focus traversal, not actor cycling.");
        Require(ReferenceEquals(stableSameRevision, Buttons(screen).Single(button => button.Text == "结束回合")) && !stableSameRevision.Disabled,
            "Same-revision selection refresh preserves the enabled global button and its native input capture.");
        Keypress(Key.Escape);
        Require(screen.Selection?.Kind == TacticalSelectionKind.Idle, "Native Escape follows the source back operation.");
        screen.SelectHit(TacticalHit.Unit(session.GetTacticalQueries(0).Unit(2)!.At, 2));
        var selected = screen.SelectedUnitId;
        var queries = session.GetTacticalQueries(0);
        var destination = queries.MoveStops(2).First(at => at != session.GetTacticalQueries(0).Unit(2)!.At && queries.CommandsAt(2, at) is { Count: 1 } commands && commands[0].SelfTargeted);
        screen.SetApplicationBusy(true);
        var busyReaction = Buttons(screen).First(button => button.GetMeta("hud_key", "").AsString().StartsWith("stances/reaction:", StringComparison.Ordinal));
        screen.SelectHit(TacticalHit.Ground(destination));
        busyReaction.EmitSignal(Button.SignalName.Pressed);
        Keypress(Key.E);
        Keypress(Key.Escape);
        Require(session.GetSnapshot().Revision == snapshot.Revision && screen.SelectedUnitId == selected, "Application busy blocks board, HUD actions, end-turn and back input.");
        screen.SetApplicationBusy(false);
        Require(screen.SelectedUnitId == selected, "Clearing application busy preserves the valid selection.");
        stableSameRevision.EmitSignal(Button.SignalName.Pressed);
        Require(session.Revision == snapshot.Revision, "A global control detached by a changed busy binding cannot dispatch.");
        screen.SelectHit(TacticalHit.Ground(destination));
        Require(session.Revision == snapshot.Revision && screen.Selection?.Kind == TacticalSelectionKind.Destination,
            "Busy release re-enables destination preview without spending the actor's turn.");
        ModifiedKeys(Key.W, Key.E, Key.Enter, Key.Escape, Key.Space, Key.Left, Key.A, Key.C, Key.H, Key.S, Key.D);
        Keypress(Key.W);
        Require(session.Revision == snapshot.Revision + 1, "Explicit wait commits after busy release.");
        var undo = Buttons(screen).Single(button => button.Text == "撤销");
        Require(!undo.Disabled, "Real undo button becomes enabled after a reversible move.");
        ModifiedKeys(Key.U, Key.E, Key.W, Key.Enter, Key.Space);
        systemButton = (Button)screen.FindChild("SystemMenuButton", true, false);
        systemButton.GrabFocus();
        ModifiedKeys(Key.Escape, Key.Enter, Key.Space, Key.Right, Key.E);
        systemButton.ReleaseFocus();
        undo.EmitSignal(Button.SignalName.Pressed);
        var undone = session.GetSnapshot();
        Require(session.Revision == undone.Revision && undone.Revision == snapshot.Revision + 2 && EcmaJson.DeepEquals(EcmaJson.Parse(undone.State.GetRawText()), EcmaJson.Parse(snapshot.State.GetRawText())), "Mounted undo restores exact canonical state and advances revision.");
        Require(screen.Selection?.Kind == TacticalSelectionKind.Idle && screen.RecentMessages.Contains("已撤销上一步"), "Undo resets selection and appends the source cosmetic message.");
        var turn = Buttons(screen).Single(button => button.Text == "结束回合");
        screen.SelectHit(TacticalHit.Unit(session.GetTacticalQueries(0).Unit(2)!.At, 2));
        Require(ReferenceEquals(turn, Buttons(screen).Single(button => button.Text == "结束回合")),
            "A current-binding global control remains the same instance after another selection.");
        turn.EmitSignal(Button.SignalName.Pressed);
        Require(session.GetSnapshot().Revision == undone.Revision + 1, "Mounted global end-turn commits once after undo.");

        // Fixture: original campaign map and units with an explicit deployment zone, not naturally reached campaign state.
        var deploymentLevel = EcmaJson.Parse(EcmaJson.Stringify(level))!.AsObject();
        var own = deploymentLevel["units"]!.AsArray().OfType<JsonObject>().Where(unit => unit["owner"]!.GetValue<int>() == 1).ToArray();
        var cells = new JsonArray(own.Select(unit => (JsonNode)new JsonObject { ["x"] = unit["x"]!.GetValue<int>(), ["y"] = unit["y"]!.GetValue<int>() }).ToArray());
        deploymentLevel["scenario"]!.AsObject()["zones"] = new JsonArray(new JsonObject { ["id"] = "selection-check-zone", ["cells"] = cells });
        deploymentLevel["deployment"] = new JsonObject { ["order"] = new JsonArray(1), ["zones"] = new JsonArray(new JsonObject { ["player"] = 1, ["zone"] = "selection-check-zone" }) };
        var deployment = new GrayFlagSession(new(engine, presentation, new JsonArray()), deploymentLevel);
        screen.BindSession(new(text, battleText, PortraitCatalogResources.Load(), deployment, new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)), deploymentLevel, AuthoredScene.Load("res://data/scenes/c01-01.json"));
        var roster = deployment.GetTacticalQueries(0).DeploymentUnits();
        var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
        Button RosterButton(int id)
        {
            var row = hud.Projection!.Sections.SelectMany(section => section.Rows.Select(row => (section, row)))
                .Single(pair => pair.row.Intent is { Kind: TacticalHudIntentKind.DeployPick, Unit: var unit } && unit == id);
            return Buttons(screen).Single(button => button.GetMeta("hud_key", "").AsString() == row.section.Key + "/" + row.row.Key);
        }
        RosterButton(roster[0]).EmitSignal(Button.SignalName.Pressed);
        RosterButton(roster[1]).EmitSignal(Button.SignalName.Pressed);
        Require(screen.SelectedUnitId == roster[1] && screen.Selection?.Kind == TacticalSelectionKind.DeploymentUnit && deployment.GetSnapshot().Revision == 0,
            "Two actual deployment roster clicks change the picked unit without dispatching a swap.");
        var confirm = Buttons(screen).Single(button => button.Text == "确认部署");
        confirm.EmitSignal(Button.SignalName.Pressed);
        Require(deployment.GetSnapshot().Revision == 1 && deployment.GetSnapshot().State.GetProperty("phase").GetString() == "playing", "Mounted deployment confirmation opens battle through session dispatch.");
        screen.QueueFree();
    }

    private void CheckRegisteredReactions()
    {
        var level = BattleTextChecks.OpeningLevel();
        var reactions = ReactionRegistry.Core();
        reactions.Register(new("plugin-dodge", false, 0.4, false, false));
        var engine = BattleEngineFactory.Create(new ReadOnlyContent(PresentationJson.Load<JsonArray>("res://data/content-packs.json")),
            [new EnginePlugin("test.native-reactions", 1, context => context.Replace(BattleCapabilities.Reactions, reactions), overrides: [BattleCapabilities.Reactions])]);
        var translation = ResourceLoader.Load<Translation>(GodotTextCatalog.ResourcePath);
        var messages = translation.GetMessageList().ToDictionary(key => key, key => translation.GetMessage(key).ToString(), StringComparer.Ordinal);
        messages["test.reactions.guard.name"] = "替换防御菜单";
        messages["test.reactions.guard.hint"] = "来自显式表现元数据";
        messages["test.reactions.plugin-dodge.name"] = "自定义闪避菜单";
        messages["test.reactions.plugin-dodge.hint"] = "自定义提示";
        var text = new GameText(key => messages.GetValueOrDefault(key), System.Globalization.CultureInfo.GetCultureInfo("zh-CN"));
        var metadata = PresentationJson.Load<JsonObject>("res://data/grayflag-content-text.json");
        metadata["reactions"]!["guard"] = new JsonObject
        {
            ["name"] = "test.reactions.guard.name",
            ["hint"] = "test.reactions.guard.hint"
        };
        metadata["reactions"]!["plugin-dodge"] = new JsonObject
        {
            ["name"] = "test.reactions.plugin-dodge.name",
            ["hint"] = "test.reactions.plugin-dodge.hint"
        };
        var presentation = SessionPresentationText.Compose(new(engine, text, StoryDocument.Load(text).IdentityNameKeys),
            PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json"),
            Godot.FileAccess.GetFileAsString("res://data/grayflag-battle-text.json"), EcmaJson.Stringify(metadata));
        var battleText = presentation.Battle;
        var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        var screen = new BattleScreen();
        AddChild(screen);
        screen.Board.AnimationsEnabled = false;
        screen.BindSession(new(text, battleText, PortraitCatalogResources.Load(), session, new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)), level, AuthoredScene.Load("res://data/scenes/c01-01.json"));
        var origin = session.GetTacticalQueries(0).Unit(2)!.At;
        screen.SelectHit(TacticalHit.Unit(origin, 2));
        Button Reaction(string id) => Buttons(screen).Single(button => button.GetMeta("hud_key", "").AsString() == "stances/reaction:" + id);
        var buttons = Buttons(screen).Where(button => button.GetMeta("hud_key", "").AsString().StartsWith("stances/reaction:", StringComparison.Ordinal)).ToArray();
        Require(buttons.Select(button => button.Text).SequenceEqual(reactions.All().Select(stance => presentation.Content.ReactionName(stance.Id)))
            && buttons.Select(button => button.TooltipText).SequenceEqual(reactions.All().Select(stance => presentation.Content.ReactionHint(stance.Id)))
            && Reaction("guard").Text == messages["test.reactions.guard.name"]
            && Reaction("plugin-dodge").TooltipText == messages["test.reactions.plugin-dodge.hint"],
            "Mounted reaction controls retain resolved registry order and explicitly supplied names and hints.");
        var staleGuard = Reaction("guard");
        Reaction("plugin-dodge").EmitSignal(Button.SignalName.Pressed);
        Require(session.GetSnapshot().Revision == 1 && session.Snapshot()["units"]!.AsArray().OfType<JsonObject>()
            .Single(unit => unit["id"]!.GetValue<int>() == 2)["reaction"]!.GetValue<string>() == "plugin-dodge",
            "The actual HUD button must commit a registered reaction through the tactical session port.");
        staleGuard.EmitSignal(Button.SignalName.Pressed);
        Require(session.GetSnapshot().Revision == 1, "Removed reaction controls cannot replay after a HUD dispatch.");
        screen.SelectHit(TacticalHit.Unit(origin, 2));
        var staleCustom = Reaction("plugin-dodge");
        var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
        var guard = hud.Projection!.Sections.Single(section => section.Key == "stances").Rows.Single(row => row.Key == "reaction:guard").Intent!;
        Require(session.Dispatch(guard.Action!.Value, guard.Stamp.Revision).Accepted, "External revision fixture did not commit.");
        staleCustom.EmitSignal(Button.SignalName.Pressed);
        Require(session.GetSnapshot().Revision == 2 && screen.DisplayedRevision == 1 && screen.Selection?.Kind == TacticalSelectionKind.Unit
            && session.Snapshot()["units"]!.AsArray().OfType<JsonObject>()
            .Single(unit => unit["id"]!.GetValue<int>() == 2)["reaction"]!.GetValue<string>() == "guard",
            "Still-mounted HUD controls cannot use stale choices after the live session revision changes.");
        screen.Refresh();
        screen.SelectHit(TacticalHit.Unit(origin, 2));
        Reaction("plugin-dodge").EmitSignal(Button.SignalName.Pressed);
        Require(session.Revision == 3, "Refreshing the stale view must restore the actual HUD action path.");
        screen.SelectHit(TacticalHit.Unit(origin, 2));
        var beforeUndo = Reaction("guard");
        Require(session.Undo(session.Revision).Accepted && session.Revision == 4, "External undo fixture did not advance revision.");
        beforeUndo.EmitSignal(Button.SignalName.Pressed);
        Require(session.Revision == 4 && screen.DisplayedRevision == 3, "Still-mounted HUD controls cannot use stale choices after undo.");
        screen.QueueFree();
    }

    private void CheckLocalizedInteraction()
    {
        var sourceLevel = BattleTextChecks.OpeningLevel();
        var level = sourceLevel.DeepClone().AsObject();
        // Bring one authored enemy into preview range without dispatching a setup action.
        var defender = level["units"]!.AsArray().OfType<JsonObject>().First(unit => unit["owner"]!.GetValue<int>() == 2);
        var attacker = level["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["key"]?.GetValue<string>() == "campaign-torren");
        var defenderAt = new BoardCell(attacker["x"]!.GetValue<int>() + 1, attacker["y"]!.GetValue<int>());
        defender["x"] = defenderAt.X;
        defender["y"] = defenderAt.Y;
        var packs = PresentationJson.Load<JsonArray>("res://data/content-packs.json");
        var engine = new BattleEngine(packs);
        var registeredMap = engine.CreateState(sourceLevel)["map"]!.AsObject();
        var translation = ResourceLoader.Load<Translation>(GodotTextCatalog.ResourcePath);
        var keys = translation.GetMessageList().ToHashSet(StringComparer.Ordinal);
        string? baselineState = null, baselineEvents = null, baselineSave = null;
        foreach (var variant in new[] { "A", "B" })
        {
            var replacements = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ui.hud.selection.unit"] = variant + " select destination",
                ["ui.hud.health"] = variant + " health {0}/{1}",
                ["ui.battle.undo"] = variant + " undo",
                ["feedback.session.undone"] = variant + " restored",
                ["ui.battle.artwork_issues"] = variant + " artwork unavailable: {0}",
                ["content.units.c01.swordsman.name"] = variant + " swordsman",
                ["content.units.c01.swordsman.blurb"] = variant + " swordsman description",
                ["content.reactions.guard.name"] = variant + " guard",
                ["content.reactions.guard.hint"] = variant + " guard hint",
                ["content.abilities.attack.name"] = variant + " attack",
                ["content.abilities.attack.hint"] = variant + " attack hint",
                ["content.weapons.c01.border-blade.name"] = variant + " border blade",
                ["combat.modifier.row"] = variant + " modifier {0} {1}",
                ["combat.modifier.matchup"] = variant + " matchup",
            };
            var text = new GameText(key => replacements.TryGetValue(key, out var replacement) ? replacement
                : keys.Contains(key) ? translation.GetMessage(key).ToString() : null,
                System.Globalization.CultureInfo.GetCultureInfo("zh-CN"));
            var presentation = BattleTextChecks.Session(engine, text);
            var battleText = presentation.Battle;
            var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
            var initial = EcmaJson.Stringify(session.Snapshot());
            var bindings = PresentationJson.Load<ArtBindings>("res://data/art-bindings.json");
            bindings.Actors.Clear();
            // Named portrait remains valid while this unselected actor lacks its map art.
            // Generic HUD crops intentionally validate their frame source during binding.
            bindings.Units.Remove("c01.roderick");
            var assets = new AssetCatalog(PresentationJson.Load<AssetManifest>("res://data/selected-assets.json"), bindings);
            var screen = new BattleScreen();
            AddChild(screen);
            screen.Board.AnimationsEnabled = false;
            var scene = AuthoredScene.Load("res://data/scenes/c01-01.json");
            var rendering = BoardSceneRendering.Load(scene, sourceLevel, registeredMap);
            screen.BindSession(new(text, battleText, PortraitCatalogResources.Load(), session, new(assets, EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, rendering.Plane)), level, scene);
            screen.ConfigureGround(rendering.Ground);
            Require(screen.Board.RenderingIssueCount > 0 && screen.Board.Diagnostics.Any(value => value.Contains("c01.roderick", StringComparison.Ordinal)),
                "Unavailable actor art remains explicitly diagnosed without changing canonical units.");
            Require(Descendants(screen).OfType<Label>().Any(label => label.Text == text.Format("ui.battle.artwork_issues", screen.Board.RenderingIssueCount))
                && !Descendants(screen).OfType<Control>().Any(control => control.TooltipText.Contains("c01.roderick", StringComparison.Ordinal)),
                "The actual board notice uses injected wording and never exposes raw resource diagnostics in a tooltip.");
            var origin = session.GetTacticalQueries(0).Unit(2)!.At;
            screen.SelectHit(TacticalHit.Unit(origin, 2));
            var nativeHud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
            var hud = nativeHud.Projection!;
            var unitRows = hud.Sections.Where(section => section.Key is "unit" or "unit-details").SelectMany(section => section.Rows).ToArray();
            Require(hud.Hint == replacements["ui.hud.selection.unit"] && unitRows.Single(row => row.Key == "hp").Label.StartsWith(variant + " health", StringComparison.Ordinal),
                "Mounted selection help and numeric unit facts use the injected catalog.");
            var guard = Buttons(screen).Single(button => button.GetMeta("hud_key", "").AsString() == "stances/reaction:guard");
            Require(hud.Sections.Single(section => section.Key == "unit").Title == replacements["content.units.c01.swordsman.name"]
                && unitRows.Single(row => row.Key == "blurb").Label == replacements["content.units.c01.swordsman.blurb"]
                && guard.Text == replacements["content.reactions.guard.name"] && guard.TooltipText == replacements["content.reactions.guard.hint"],
                "Mounted unit content and reaction labels and hints use supplied presentation text, not canonical definitions.");
            screen.SelectHit(TacticalHit.Unit(origin, 2));
            var attack = Buttons(screen).Single(button => button.GetMeta("hud_key", "").AsString() == "commands/attack:c01.border-blade");
            Require(attack.Text.Contains(replacements["content.abilities.attack.name"], StringComparison.Ordinal)
                && attack.Text.Contains(replacements["content.weapons.c01.border-blade.name"], StringComparison.Ordinal)
                && attack.TooltipText.Contains(replacements["content.abilities.attack.hint"], StringComparison.Ordinal),
                "Mounted weapon command names and range hints compose supplied ability and weapon text.");
            screen.SelectHit(TacticalHit.Unit(defenderAt, session.GetTacticalQueries(0).UnitAt(defenderAt)!.Id));
            Require(screen.Selection?.Kind == TacticalSelectionKind.Target,
                "Selecting the adjacent authored enemy must arm a real attack preview without dispatching.");
            using var hoverTarget = new InputEventKey { Keycode = Key.Right, Pressed = true };
            screen._UnhandledKeyInput(hoverTarget);
            var modifierRows = nativeHud.Projection!.Sections.Where(section => section.Key is "forecast" or "forecast-details").SelectMany(section => section.Rows)
                .Where(row => row.Key.StartsWith("strike-mod:", StringComparison.Ordinal)).ToArray();
            Require(modifierRows.Length > 0 && modifierRows.All(row => row.Label.StartsWith(variant + " modifier ", StringComparison.Ordinal))
                && modifierRows.Any(row => row.Label.Contains(replacements["combat.modifier.matchup"], StringComparison.Ordinal))
                && modifierRows.All(row => Descendants(nativeHud).OfType<Label>().Any(label => label.Text == row.Label)),
                "Real attack-preview modifiers render through supplied formatter text in the mounted native HUD.");
            using var cancel = new InputEventKey { Keycode = Key.Escape, Pressed = true };
            screen._UnhandledKeyInput(cancel);
            screen._UnhandledKeyInput(cancel);
            screen._UnhandledKeyInput(cancel);
            Require(screen.Selection?.Kind == TacticalSelectionKind.Idle && session.GetSnapshot().Revision == 0
                && EcmaJson.Stringify(session.Snapshot()) == initial && session.Events.Count == 0,
                "Content inspection and attack preview cancel without changing domain state or emitting events.");
            TacticalSceneInputChecks.Move(screen, session);
            var state = EcmaJson.Stringify(session.Snapshot());
            var events = EcmaJson.Stringify(new JsonArray(session.Events.Select(value => (JsonNode?)value).ToArray()));
            var actualSave = session.Save();
            Require(EcmaJson.DeepEquals(actualSave, engine.SaveBattle(session.Snapshot(), actualSave["savedAt"]!.GetValue<string>())),
                "The real session save uses the canonical complete document contract.");
            var save = EcmaJson.Stringify(engine.SaveBattle(session.Snapshot(), "2026-10-21T00:00:00.000Z"));
            Require(session.GetSnapshot().Revision == 1, "Localized controls still commit the real movement command.");
            if (baselineState is null)
            {
                baselineState = state;
                baselineEvents = events;
                baselineSave = save;
            }
            else
                Require(state == baselineState && events == baselineEvents && save == baselineSave,
                "Changing only catalog wording cannot change the battle state, event stream or saved document.");
            var undo = Buttons(screen).Single(button => button.Name == "Undo");
            Require(undo.Text == replacements["ui.battle.undo"], "The mounted undo button uses injected wording.");
            undo.EmitSignal(Button.SignalName.Pressed);
            Require(EcmaJson.Stringify(session.Snapshot()) == initial && session.GetSnapshot().Revision == 2
                && screen.RecentMessages.Contains(replacements["feedback.session.undone"]),
                "The real localized undo restores the canonical state and formats its feedback independently.");
            RemoveChild(screen);
            screen.Free();
        }

        level = BattleTextChecks.OpeningLevel();
        foreach (var expectedArtFailure in new[] { true, false })
        {
            var text = GodotTextCatalog.LoadSimplifiedChinese();
            var presentation = BattleTextChecks.Session(engine, text);
            var battleText = presentation.Battle;
            var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
            var screen = new BattleScreen();
            AddChild(screen);
            screen.Board.AnimationsEnabled = false;
            screen.BindSession(new(text, battleText, PortraitCatalogResources.Load(), session, new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)), level, AuthoredScene.Load("res://data/scenes/c01-01.json"));
            Exception fault = expectedArtFailure ? new MissingArtResourceException("res://test-missing-art", "test missing art diagnostic")
                : new InvalidOperationException("test presentation programming defect");
            screen.Board.EventPresented += (_, _) => throw fault;
            var previous = System.Threading.SynchronizationContext.Current;
            var capture = new PresentationFaultContext();
            System.Threading.SynchronizationContext.SetSynchronizationContext(capture);
            try
            {
                TacticalSceneInputChecks.Move(screen, session);
            }
            finally { System.Threading.SynchronizationContext.SetSynchronizationContext(previous); }
            var playerNotice = text.Format("error.presentation.playback_failed");
            Require(expectedArtFailure ? capture.Fault is null : ReferenceEquals(capture.Fault, fault),
                "Only expected missing-art failures are recovered; programming faults escape the async UI boundary unchanged.");
            Require(Descendants(screen).OfType<Label>().Any(label => label.Text == playerNotice) == expectedArtFailure,
                "A programming defect must not be converted into a recoverable player notice.");
            Require(!screen.IsPresenting && !screen.Board.IsPresenting && session.GetSnapshot().Revision == 1,
                "Failure cleanup releases playback gates without undoing the already committed domain action.");
            RemoveChild(screen);
            screen.Free();
        }
    }

    private sealed class PresentationFaultContext : System.Threading.SynchronizationContext
    {
        public Exception? Fault
        {
            get; private set;
        }
        public override void Post(System.Threading.SendOrPostCallback callback, object? state)
        {
            try
            {
                callback(state);
            }
            catch (Exception error) { Fault = error; }
        }
    }

    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static System.Collections.Generic.IEnumerable<Button> Buttons(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Button button)
                yield return button;
            foreach (var descendant in Buttons(child))
                yield return descendant;
        }
    }
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
    // Only this synthetic replacement fixture rebases a revision. Production must never repair a mismatched stamp.
    private sealed class StampedQueries(ITacticalSelectionQueries inner, TacticalStamp stamp) : ITacticalSelectionQueries
    {
        public TacticalStamp Stamp => stamp;
        public string Phase => inner.Phase;
        public bool HumanInput => inner.HumanInput;
        public bool HumanTurn => inner.HumanTurn;
        public bool CanUndo => inner.CanUndo;
        public IReadOnlySet<BoardCell> InteractiveCells => inner.InteractiveCells;
        public TacticalUnit? Unit(int id) => inner.Unit(id);
        public TacticalUnit? UnitAt(BoardCell at) => inner.UnitAt(at);
        public bool AreEnemies(int firstUnit, int secondUnit) => inner.AreEnemies(firstUnit, secondUnit);
        public bool IsSite(BoardCell at) => inner.IsSite(at);
        public bool CanOpenRecruitment(BoardCell at) => inner.CanOpenRecruitment(at);
        public bool IsEnemyOfViewer(int unit) => inner.IsEnemyOfViewer(unit);
        public IReadOnlyList<int> Actors() => inner.Actors();
        public IReadOnlyList<BoardCell> MoveStops(int unit) => inner.MoveStops(unit);
        public IReadOnlyList<BoardCell>? PathTo(int unit, BoardCell at) => inner.PathTo(unit, at);
        public IReadOnlyList<BoardCell> Threat(int unit) => inner.Threat(unit);
        public IReadOnlyList<BoardCell> ControlZone(int unit) => inner.ControlZone(unit);
        public IReadOnlyList<TacticalCommand> CommandsAt(int unit, BoardCell at) => inner.CommandsAt(unit, at);
        public TacticalAttack Attack(int unit, BoardCell target, BoardCell from, string weapon) => inner.Attack(unit, target, from, weapon);
        public IReadOnlyList<int> DeploymentUnits() => inner.DeploymentUnits();
        public IReadOnlyList<BoardCell> DeploymentZone() => inner.DeploymentZone();
        public IReadOnlyList<TacticalTarget> DeploymentTargets(int unit) => inner.DeploymentTargets(unit);
        public IReadOnlyList<TacticalTactic> Tactics() => inner.Tactics();
        public IReadOnlyList<TacticalPassenger> Passengers(int carrier) => inner.Passengers(carrier);
        public IReadOnlyList<BoardCell> DirectionSteps(BoardCell from) => inner.DirectionSteps(from);
    }

}
