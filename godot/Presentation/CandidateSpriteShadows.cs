#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace Empire.Presentation;

public enum CandidateShadowKind { Topic, Environment, Standing, Foreground, FrameEdge, Figure, BattleProp, OwnerBanner, Route, BoardRelief }

/// <summary>Explicit board-lifetime cache for complete filtered compositions.</summary>
public sealed class CandidateShadowCache : IDisposable
{
    internal sealed record Frame(ImageTexture Texture, Vector2 Origin, float Density);
    private bool _disposed;
    internal bool IsDisposed => _disposed;
    internal int TextureCount => _frames.Count + _generatedSources.Count;
    private readonly Dictionary<object, Frame> _frames = new();
    private readonly Dictionary<Color, CandidateFilterComposition.Raster> _bodies = new();
    private readonly Dictionary<(string Svg, float Density), ImageTexture> _generatedSources = new();

    internal Frame Get(Texture2D source, CandidateShadowKind kind, float density) => Get((source.GetInstanceId(), kind, density), () =>
    {
        using var input = CandidateSpriteShadows.ReadFrame(source);
        var raster = CandidateShadowRaster.Filter(input, CandidateSpriteShadows.Drops(kind), density);
        return new(raster.Image, -Vector2.One * raster.Padding / density, density);
    });

    internal Frame Figure(Sprite2D source, Color team)
    {
        var scale = source.Scale.Abs();
        var offset = source.Offset * scale;
        var key = ("figure", source.Texture.GetInstanceId(), scale, offset, source.FlipH, source.FlipV, team);
        return Get(key, () => {
            if (!_bodies.TryGetValue(team, out var body)) _bodies.Add(team, body = CandidateBoardLighting.UnitBody(team));
            using var input = CandidateSpriteShadows.ReadFrame(source.Texture);
            if (source.FlipH) input.FlipX();
            if (source.FlipV) input.FlipY();
            var composition = CandidateFilterComposition.Compose(new[] {
                new CandidateFilterComposition.Layer(body.Image, new(body.Origin, new Vector2(body.Image.GetWidth(), body.Image.GetHeight()) / body.Density)),
                new CandidateFilterComposition.Layer(input, new(offset, source.Texture.GetSize() * scale)),
            }, 2);
            using (composition.Image) return CandidateFilterComposition.Filter(composition, CandidateShadowKind.Figure);
        });
    }

    internal Frame Get(object key, Func<CandidateFilterComposition.Raster> create)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_frames.TryGetValue(key, out var frame)) return frame;
        var raster = create();
        using (raster.Image) frame = new(ImageTexture.CreateFromImage(raster.Image), raster.Origin, raster.Density);
        _frames.Add(key, frame);
        return frame;
    }

    internal ImageTexture SvgSource(string svg, float density)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_generatedSources.TryGetValue((svg, density), out var texture)) return texture;
        using var image = new Image();
        var error = image.LoadSvgFromString(svg, density);
        if (error != Error.Ok) throw new InvalidOperationException($"Source SVG rasterization failed: {error}");
        texture = ImageTexture.CreateFromImage(image);
        _generatedSources.Add((svg, density), texture);
        return texture;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var frame in _frames.Values) frame.Texture.Dispose();
        _frames.Clear();
        foreach (var body in _bodies.Values) body.Image.Dispose();
        _bodies.Clear();
        foreach (var texture in _generatedSources.Values) texture.Dispose();
        _generatedSources.Clear();
    }
}

/// <summary>
/// Owns the complete filtered drawing, including source RGB. The original sprite remains
/// the immutable frame/anchor handle but its own draw is suppressed with SelfModulate.
/// Only this one composited child draws, so inherited opacity is post-filter group opacity.
/// </summary>
public partial class CandidateSpriteShadows : Sprite2D
{
    internal sealed record Drop(Vector2 Offset, float Sigma, Color Color);
    private CandidateShadowCache _cache = null!;
    private Color? _figureTeam;

    public static CandidateSpriteShadows Create(Sprite2D source, CandidateShadowKind kind, float texturePixelsPerStyleUnit, CandidateShadowCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        var effect = new CandidateSpriteShadows { Name = "SourceSpriteShadows", ShowBehindParent = true,
            TextureFilter = TextureFilterEnum.Linear, _cache = cache };
        try { effect.Synchronize(source, kind, texturePixelsPerStyleUnit); return effect; }
        catch { effect.Free(); throw; }
    }

    /// <summary>The source figure owns contact, team ring and current strip, never badges.</summary>
    public static CandidateSpriteShadows CreateFigure(Sprite2D source, Color team, CandidateShadowCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        if (source.Centered || source.Rotation != 0 || source.Scale.X == 0 || source.Scale.Y <= 0)
            throw new ArgumentException("A source figure requires its explicit foot-relative unrotated frame.");
        var effect = new CandidateSpriteShadows { Name = "SourceSpriteShadows", TextureFilter = TextureFilterEnum.Linear,
            _cache = cache, _figureTeam = team };
        try { effect.Synchronize(source, CandidateShadowKind.Figure, 2); return effect; }
        catch { effect.Free(); throw; }
    }

    public void Synchronize(Sprite2D source, CandidateShadowKind kind, float texturePixelsPerStyleUnit)
    {
        if (source.Texture is null || source.Hframes != 1 || source.Vframes != 1 || source.RegionEnabled)
            throw new ArgumentException("Filter source must expose one already-clipped texture frame.");
        if (!float.IsFinite(texturePixelsPerStyleUnit) || texturePixelsPerStyleUnit <= 0)
            throw new ArgumentOutOfRangeException(nameof(texturePixelsPerStyleUnit));
        var cache = _cache ?? throw new InvalidOperationException("Create filters with an explicit board cache before synchronizing.");
        if (_figureTeam is { } team)
        {
            if (kind != CandidateShadowKind.Figure) throw new ArgumentException("A compound figure cannot change its filter owner.");
            var frame = cache.Figure(source, team);
            Texture = frame.Texture;
            Centered = false;
            Offset = frame.Origin * frame.Density;
            Scale = Vector2.One / source.Scale.Abs() / frame.Density;
            FlipH = FlipV = false; // Frame-local flips are already composed; foot-facing scale is inherited.
            SetMeta("composition_inputs", 3);
        }
        else
        {
            var frame = cache.Get(source.Texture, kind, texturePixelsPerStyleUnit);
            Texture = frame.Texture;
            Centered = source.Centered;
            Offset = source.Offset + (Centered ? Vector2.Zero : frame.Origin * frame.Density);
            FlipH = source.FlipH;
            FlipV = source.FlipV;
            SetMeta("composition_inputs", 1);
        }
        source.SelfModulate = new Color(1, 1, 1, 0);
        SetMeta("source_texture", source.Texture.GetInstanceId());
        SetMeta("shadow_kind", kind.ToString());
    }

    internal static Image ReadFrame(Texture2D texture)
    {
        if (texture is AtlasTexture atlas)
        {
            if (atlas.Margin != new Rect2()) throw new ArgumentException("Shadow atlas margins must be resolved before sampling.");
            using var sheet = atlas.Atlas.GetImage();
            return sheet.GetRegion(new Rect2I((Vector2I)atlas.Region.Position, (Vector2I)atlas.Region.Size));
        }
        return texture.GetImage();
    }

    // candidate-01-board-style.ts. CSS drop-shadow's third length is stdDeviation,
    // not box-shadow's blur radius (W3C Filter Effects 1, section 6.1). BoardRelief
    // below retains its separately authored SVG stdDeviation values verbatim.
    internal static IReadOnlyList<Drop> Drops(CandidateShadowKind kind)
    {
        static Drop Css(float x, float y, float blur, string color, float alpha) => new(new(x, y), blur, new Color(color, alpha));
        return kind switch
        {
            CandidateShadowKind.Topic => new[] { Css(0, 1, 1, "#000000", .42f) },
            CandidateShadowKind.Environment => new[] { Css(0, 1, 0, "#050c09", .62f), Css(0, 2, 2, "#050c09", .34f) },
            CandidateShadowKind.Standing => new[] { Css(0, 1, 0, "#040a07", .74f), Css(0, 3, 3, "#040a07", .42f) },
            CandidateShadowKind.Foreground => new[] { Css(0, 2, 1, "#030906", .78f), Css(0, 6, 5, "#030906", .46f) },
            CandidateShadowKind.FrameEdge => new[] { Css(0, 3, 1, "#030906", .82f), Css(0, 8, 7, "#030906", .52f) },
            CandidateShadowKind.Figure => new[] { Css(0, 0, .65f, "#fff6de", .45f), Css(0, 2, 1.2f, "#000000", .75f) },
            CandidateShadowKind.BattleProp => new[] { Css(0, 2, 2, "#000000", .42f) },
            CandidateShadowKind.OwnerBanner => new[] { Css(0, 1, 1.1f, "#000000", .7f) },
            CandidateShadowKind.Route => new[] { Css(0, 1, 0, "#ffe6b0", .1f) },
            CandidateShadowKind.BoardRelief => new[] { new Drop(new(0, 1), 0, new Color(0, 0, 0, .8f)), new Drop(new(1, 0), 0, new Color(0, 0, 0, .42f)), new Drop(new(0, 2), 1, new Color(0, 0, 0, .36f)) },
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }
}
