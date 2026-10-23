#nullable enable
using System.Collections.Generic;
using System.Linq;
using Empire.Core.Geometry;
using Empire.Core.State;
using Godot;

namespace Empire.Presentation;

public sealed record BoardGroundRules(EnvironmentCatalog Environment, ITacticalGrid Grid, ReadOnlyContent Content, ShadowCache Shadows);
public delegate BoardGroundView BoardGroundFactory(BoardGroundRules rules, AuthoredScene scene);

/// <summary>A unique authored site binding, not one binding per drawing part.</summary>
public sealed record GroundSite(string Id, Coord Cell, bool OwnsMapStructureBody);

/// <summary>The board owns the returned node; a ground renderer owns its transferred authored subtree.</summary>
public abstract partial class BoardGroundView : Node2D
{
    public abstract IReadOnlyList<string> Diagnostics
    {
        get;
    }
    public abstract IReadOnlyList<TerrainDepthEntry> DepthEntries
    {
        get;
    }
    public abstract IReadOnlyList<GroundSite> Sites
    {
        get;
    }
    public bool OwnsMapStructureBody(Coord cell) => Sites.Any(site => site.Cell == cell && site.OwnsMapStructureBody);
    public abstract void ConfigureAuthoredGround(Node2D authoredGround);
    public abstract Node2D DetachAuthoredGround();
    public abstract void Render(BoardProjection projection, string[] tiles, int[] elevation, Vector2I size);
}
