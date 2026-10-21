#nullable enable
using System.Collections.Generic;
using Empire.Core.Geometry;
using Godot;

namespace Empire.Presentation;

public sealed record BoardGroundRules(EnvironmentCatalog Environment, ITacticalGrid Grid, ShadowCache Shadows);
public delegate BoardGroundView BoardGroundFactory(BoardGroundRules rules, AuthoredScene scene);

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
    public abstract void ConfigureAuthoredGround(Node2D authoredGround);
    public abstract Node2D DetachAuthoredGround();
    public abstract void Render(BoardProjection projection, string[] tiles, int[] elevation, Vector2I size);
}
