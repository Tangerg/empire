#nullable enable
using System.Text.Json.Serialization;

namespace Empire.Presentation;

/// <summary>Resource-local geometry; placement and canonical site identity remain instance-owned.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SceneSurfaceDocument
{
    public required int Schema
    {
        get; init;
    }
    public required SceneSurfaceSupport[] Supports
    {
        get; init;
    }
    public required SceneSurfaceMember[] Members
    {
        get; init;
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SceneSurfaceSupport
{
    public required string Id
    {
        get; init;
    }
    public required double[] FrontPixels
    {
        get; init;
    }
    /// <summary>Physical measured support or explicitly authored structural continuation, never an implicit alpha search.</summary>
    public required string Kind
    {
        get; init;
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SceneSurfaceMember
{
    public required string Id
    {
        get; init;
    }
    public required string SupportId
    {
        get; init;
    }
    public required double[][] PolygonPixels
    {
        get; init;
    }
    public required double[][] UvPixels
    {
        get; init;
    }
    public required string Coverage
    {
        get; init;
    }
    public required string Tint
    {
        get; init;
    }
}
