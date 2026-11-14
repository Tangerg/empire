#nullable enable
using System;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Presentation.Tactical;
using Godot;

namespace Empire.Presentation;

public partial class MiniatureCameraChecks : Node
{
    public override async void _Ready()
    {
        try
        {
            var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(l => l["id"]!.GetValue<string>() == "c01-01");
            var state = engine.CreateState(level);
            var before = EcmaJson.Stringify(state);
            var map = state["map"]!.AsObject();
            var width = map["width"]!.GetValue<int>();
            var playable = map["tiles"]!.AsArray().Select((t, i) => (t, i)).Where(pair =>
                !engine.Catalog.Definition("terrains", pair.t!.GetValue<string>())!.Value.GetProperty("tags").EnumerateArray().Any(t => t.GetString() == "outside"))
                .Select(pair => new Coord(pair.i % width, pair.i / width)).ToHashSet();
            var projection = new BoardProjection(engine.Grids.Get("square4"), BoardPlane.Dimetric, map, playable);
            foreach (var size in new[] { new Vector2(960, 640), new Vector2(1180, 720) })
            {
                var host = new Control { Size = size };
                AddChild(host);
                var view = new MiniatureBoardView(engine.Catalog);
                host.AddChild(view);
                view.Render(projection, state, null);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var focus = projection.CellCenter(new(35, 22));
                var owner = new BattlefieldCamera(new(projection.Size.X, projection.Size.Y), new(74, 62, 74, 74), new(focus.X, focus.Y));
                owner.Resize(new(size.X, size.Y), new(16, 110, 16, 134));
                view.SyncCamera(owner, size);
                var contacts = new[] { view.Geometry.Contact(new BoardCell(9, 18)), view.Geometry.Contact(new BoardCell(18, 13)) };
                if (!view.RevealContacts(owner, contacts, size))
                    throw new InvalidOperationException($"Actual 3D endpoints did not fit at {size}.");
                var position = owner.Position;
                var zoom = owner.Zoom;
                if (!view.RevealContacts(owner, contacts, size) || owner.Position != position || owner.Zoom != zoom)
                    throw new InvalidOperationException("Already visible contacts must preserve manual camera framing exactly.");
                var human = state["players"]!.AsArray().OfType<JsonObject>().First(p => p["controller"]!.GetValue<string>() == "human")["id"]!.GetValue<int>();
                var cohort = state["units"]!.AsArray().OfType<JsonObject>()
                    .Where(unit => unit["owner"]!.GetValue<int>() == human && unit["hp"]!.GetValue<double>() > 0)
                    .Select(unit => view.Geometry.Contact(new BoardCell(unit["x"]!.GetValue<int>(), unit["y"]!.GetValue<int>()))).ToArray();
                if (!view.RevealContacts(owner, cohort, size, true))
                    throw new InvalidOperationException("Handback cohort does not fit the real 3D safe rectangle.");
                GD.Print($"MINIATURE_CAMERA_REVEAL {size}: actual 3D safe rectangle and no-op repeat pass.");
                host.QueueFree();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            var aiState = state.DeepClone().AsObject();
            aiState["currentPlayer"] = state["players"]!.AsArray().OfType<JsonObject>().First(p => p["controller"]!.GetValue<string>() != "human")["id"]!.GetValue<int>();
            if (!BattleScreen.HumanHandback(EcmaJson.ToElement(aiState), EcmaJson.ToElement(state))
                || BattleScreen.HumanHandback(EcmaJson.ToElement(state), EcmaJson.ToElement(state))
                || BattleScreen.HumanHandback(EcmaJson.ToElement(state), EcmaJson.ToElement(aiState)))
                throw new InvalidOperationException("Handback must occur only on AI-to-human boundary, never every human revision.");
            if (before != EcmaJson.Stringify(state))
                throw new InvalidOperationException("Camera modified canonical state.");
            GD.Print("MINIATURE_CAMERA_OK");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
