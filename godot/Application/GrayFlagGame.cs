using Godot;
using System.Text.Json.Nodes;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Presentation;

namespace Empire.Application;

/// <summary>Loads content, composes the native engine, and supplies it to the view.</summary>
public partial class GrayFlagGame : Node
{
    private GrayFlagSession session = null!;
    private BattleScreen screen = null!;

    public override void _Ready()
    {
        var level = PresentationJson.Load<JsonObject>("res://data/grayflag-level.json");
        var packs = PresentationJson.Load<JsonArray>("res://data/content-packs.json");
        var engine = new GrayFlagEngine(packs);
        session = new GrayFlagSession(engine, level);
        screen = new BattleScreen();
        AddChild(screen);
        screen.BindSession(session, AssetCatalog.Load(), level, AuthoredScene.Load(), EnvironmentCatalog.Load(), facing =>
        {
            if (facing is null) return false;
            var grid = engine.Grids.Get(level["rules"]?["grid"]?.GetValue<string>() ?? "square4");
            return grid.Step(new Coord(0, 0), facing).X < 0;
        });
        screen.ConfigureTerrain(WorldMaterialsManifest.Load());
    }
}
