#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Godot;
using Empire.Core.Determinism;

namespace Empire.Presentation;

/// <summary>Headless source-contract and native-resource tests, not visual acceptance.</summary>
public partial class LightingChecks : Node
{
    private int _checks;
    public override async void _Ready()
    {
        try
        {
            var argument = OS.GetCmdlineUserArgs().Single(value => value.StartsWith("--lighting-oracle=", StringComparison.Ordinal));
            var oracle = EcmaJson.Parse(System.IO.File.ReadAllText(argument["--lighting-oracle=".Length..]))!.AsObject();
            Fields(oracle); ShadowDeclarations(oracle); Shadows(); Contacts(); Compositions(oracle); JunctionComposition();
            await Integrated();
            GD.Print($"PASS {_checks} source lighting/shadow contracts. Headless resource checks only; live visual acceptance remains pending.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private async Task Integrated()
    {
        var fixture = PresentationJson.Load<JsonObject>("res://tests/oracle/grayflag-initial-raw.json");
        var level = fixture["level"]!.AsObject();
        var state = fixture["state"]!.AsObject();
        var before = EcmaJson.Stringify(state);
        var assets = AssetCatalog.Load();
        var scene = AuthoredScene.Load();
        var board = new BoardView { AdvanceAnimationsAutomatically = false };
        AddChild(board);
        try
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            board.Configure(assets, level, scene, EnvironmentCatalog.Load(), facing => facing == "west");
            board.ConfigureTerrain(WorldMaterialsManifest.Load());
            board.ConfigureDynamicState(new Empire.Core.State.ReadOnlyContent(PresentationJson.Load<JsonArray>("res://data/content-packs.json")));
            board.RenderState(state);
            GD.Print($"Integrated source-lighting board first projection: {clock.ElapsedMilliseconds}ms.");
            var terrain = board.GetNode<TerrainView>("AuthoredScene/Terrain");
            var sun = terrain.GetNode<Sprite2D>("ComposedTerrain/SourceFieldSunlight");
            var hearths = terrain.GetNode<Node2D>("ComposedTerrain/SourceHearthLights");
            var actor = board.GetNode<Node2D>("CanonicalProjection/Unit_2");
            var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
            var shadow = sprite.GetNode<CandidateSpriteShadows>("SourceSpriteShadows");
            var cached = shadow.Texture;
            Require(actor is CanvasGroup && actor.SelfModulate == Colors.White, "Outer unit opacity must belong to a native compositing group.");
            Require(hearths.ZIndex < sun.ZIndex && sun.ZIndex < actor.ZIndex, "Source non-standing lights changed board depth.");
            Require(shadow.GetMeta("composition_inputs").AsInt32() == 3 && sprite.SelfModulate.A == 0, "Figure must compose contact, ring and strip once, without a second original draw.");
            Require(shadow.GetMeta("source_texture").AsUInt64() == sprite.Texture.GetInstanceId(), "Integrated figure shadow sampled the wrong source frame.");
            foreach (var frame in board.GetNode<Node2D>("AuthoredScene/AuthoredFrame").GetChildren().OfType<Sprite2D>())
                Require(frame.GetNode<CandidateSpriteShadows>("SourceSpriteShadows").GetMeta("shadow_kind").AsString() == CandidateShadowKind.Foreground.ToString(), "Authored frame region was incorrectly inferred as an edge CSS class.");
            foreach (var structure in board.GetNode<Node2D>("CanonicalProjection").GetChildren().OfType<Node2D>().Where(node => node.Name.ToString().StartsWith("MapStructure_", StringComparison.Ordinal)))
                Require(structure.GetNodeOrNull<Sprite2D>("SourceStructureContact") is not null, "Map structure contact shadow is absent.");
            var standing = board.GetNode<Node2D>("AuthoredScene/AuthoredStanding");
            foreach (var (placement, index) in scene.Placements.Select((placement, index) => (placement, index)).Where(item => item.placement.Layer == "under-units"))
            {
                var prop = standing.GetNode<Sprite2D>($"Placement_{index}");
                var expected = placement.TopicId is not null ? CandidateShadowKind.Topic : placement.StandingShadow ? CandidateShadowKind.Standing : CandidateShadowKind.Environment;
                Require(prop.GetNode<CandidateSpriteShadows>("SourceSpriteShadows").GetMeta("shadow_kind").AsString() == expected.ToString(), "Authored prop ignored its explicit source shadow contract.");
            }
            clock.Restart(); board.RenderState(state);
            Require(ReferenceEquals(cached, board.GetNode<Sprite2D>("CanonicalProjection/Unit_2/AuthoredSprite/SourceSpriteShadows").Texture), "Board projection recreation lost its frame cache.");
            GD.Print($"Integrated cached repeat projection: {clock.ElapsedMilliseconds}ms.");

            // Presentation-only receipt fixture: verifies frame/facing playback, not legality.
            var unit = state["units"]!.AsArray().OfType<JsonObject>().Single(value => value["id"]!.GetValue<int>() == 2);
            var from = new JsonObject { ["x"] = unit["x"]!.DeepClone(), ["y"] = unit["y"]!.DeepClone() };
            var to = new JsonObject { ["x"] = unit["x"]!.GetValue<int>() + 1, ["y"] = unit["y"]!.DeepClone() };
            var action = new JsonObject { ["kind"] = "command", ["unit"] = 2, ["path"] = new JsonArray(from, to), ["order"] = new JsonObject { ["type"] = "wait" } };
            var task = board.PresentTransitionAsync(new(1, EcmaJson.ToElement(state), EcmaJson.ToElement(action), Array.Empty<System.Text.Json.JsonElement>(), EcmaJson.ToElement(state), false));
            var moving = board.GetNode<Sprite2D>("CanonicalProjection/Unit_2/AuthoredSprite");
            Require(moving.GetNode<CandidateSpriteShadows>("SourceSpriteShadows").GetMeta("source_texture").AsUInt64() == moving.Texture.GetInstanceId(), "Animation frame change left a stale silhouette.");
            Require(!ReferenceEquals(moving.GetNode<Sprite2D>("SourceSpriteShadows").Texture, cached), "Walk frame did not switch its cached silhouette.");
            board.AdvancePresentation(85);
            await task;
            Require(EcmaJson.Stringify(state) == before, "Integrated lighting/playback changed canonical state.");
            Require(board.GetNode<Sprite2D>("CanonicalProjection/Unit_2/AuthoredSprite/SourceSpriteShadows").Texture == cached, "Settled projection did not restore its idle silhouette.");
        }
        finally { board.Free(); }
    }

    private void ShadowDeclarations(JsonObject oracle)
    {
        var style = Regex.Replace(EcmaJson.ReadString(oracle["style"]), @"/\*[\s\S]*?\*/|</?style>", "");
        var selectors = new Dictionary<CandidateShadowKind, string>
        {
            [CandidateShadowKind.Topic] = ".candidate-scenery-topic",
            [CandidateShadowKind.Environment] = ".candidate-environment-prop",
            [CandidateShadowKind.Standing] = ".candidate-environment-prop.is-standing",
            [CandidateShadowKind.Foreground] = ".candidate-scene-foreground .candidate-environment-prop",
            [CandidateShadowKind.FrameEdge] = ".candidate-scene-foreground .is-frame-bottom",
            [CandidateShadowKind.Figure] = ".candidate-map .unit .figure",
            [CandidateShadowKind.BattleProp] = ".candidate-battle-prop",
            [CandidateShadowKind.OwnerBanner] = ".candidate-map .candidate-owner-banner",
            [CandidateShadowKind.Route] = ".candidate-ground-route",
        };
        foreach (var (kind, selector) in selectors)
        {
            var rule = Regex.Matches(style, @"([^{}]+)\{([^{}]*)\}").Cast<Match>()
                .Single(match => match.Groups[1].Value.Split(',').Select(value => value.Trim()).Contains(selector)
                    && match.Groups[2].Value.Contains("filter:", StringComparison.Ordinal)).Groups[2].Value;
            var effects = Regex.Matches(rule, @"drop-shadow\(([-\d.]+)(?:px)?\s+([-\d.]+)(?:px)?\s+([-\d.]+)(?:px)?\s+rgb\((\d+)\s+(\d+)\s+(\d+)\s*/\s*([\d.]+)%\)\)").Cast<Match>().ToArray();
            var native = CandidateSpriteShadows.Drops(kind);
            Require(effects.Length == native.Count && effects.Length > 0, $"Source filter count changed: {kind}.");
            for (var index = 0; index < effects.Length; index++)
            {
                float Part(int group) => float.Parse(effects[index].Groups[group].Value, CultureInfo.InvariantCulture);
                Require(native[index].Offset.IsEqualApprox(new(Part(1), Part(2))) && Math.Abs(native[index].Sigma - Part(3)) < .00001, $"Source shadow geometry changed: {kind}.");
                Require(native[index].Color.IsEqualApprox(new Color(Part(4) / 255, Part(5) / 255, Part(6) / 255, Part(7) / 100)), $"Source shadow color changed: {kind}.");
            }
        }
        var svg = XDocument.Parse("<svg>" + EcmaJson.ReadString(oracle["relief"]) + "</svg>");
        var primitives = svg.Descendants("feDropShadow").ToArray();
        var relief = CandidateSpriteShadows.Drops(CandidateShadowKind.BoardRelief);
        Require(primitives.Length == relief.Count, "Source SVG relief count changed.");
        for (var index = 0; index < primitives.Length; index++)
            Require(relief[index].Offset.IsEqualApprox(new(Attr(primitives[index], "dx"), Attr(primitives[index], "dy")))
                && Math.Abs(relief[index].Sigma - Attr(primitives[index], "stdDeviation")) < .00001
                && Math.Abs(relief[index].Color.A - Attr(primitives[index], "flood-opacity")) < .00001, "Source SVG relief primitive changed.");
    }

    private void Fields(JsonObject oracle)
    {
        var materials = WorldMaterialsManifest.Load();
        foreach (var field in oracle["fields"]!.AsArray().OfType<JsonObject>())
        {
            var before = EcmaJson.Stringify(field);
            var size = new Vector2I(field["width"]!.GetValue<int>(), field["height"]!.GetValue<int>());
            var tiles = field["tiles"]!.AsArray().Select(EcmaJson.ReadString).ToArray();
            var source = XDocument.Parse("<svg>" + EcmaJson.ReadString(field["hearths"]) + "</svg>");
            var rectangles = source.Descendants("rect").Where(element => ((string?)element.Attribute("fill"))?.StartsWith("url(#c01-hearth-", StringComparison.Ordinal) == true).ToArray();
            var lights = CandidateBoardLighting.CreateField(size, tiles, materials);
            try
            {
                Require(lights.Hearths.GetChildCount() == rectangles.Length, $"Source pool count differs: {field["id"]}");
                for (var index = 0; index < rectangles.Length; index++)
                {
                    var rect = rectangles[index]; var sprite = lights.Hearths.GetChild<Sprite2D>(index);
                    Require(sprite.Position.IsEqualApprox(new Vector2(Attr(rect, "x"), Attr(rect, "y"))), "Source pool placement changed.");
                    Require((sprite.Texture.GetSize() * sprite.Scale).IsEqualApprox(new Vector2(Attr(rect, "width"), Attr(rect, "height"))), "Source pool span changed.");
                    using var image = sprite.Texture.GetImage();
                    var center = image.GetPixel(image.GetWidth() / 2, image.GetHeight() / 2);
                    var expectedAlpha = Attr(rect, "width") == 96 ? .26f : .18f;
                    Require(Math.Abs(center.A - expectedAlpha) < .012, "Hearth gradient did not rasterize its authored peak.");
                    Require(image.GetPixel(0, 0).A == 0, "Hearth light has a rectangular matte.");
                }
                Require(lights.Sunlight.Texture.GetSize() * lights.Sunlight.Scale == new Vector2(size.X * 32, size.Y * 32), "Sunlight field geometry changed.");
                var original = XDocument.Parse("<svg>" + EcmaJson.ReadString(field["sunlight"]) + "</svg>");
                var native = XDocument.Parse("<svg>" + CandidateBoardLighting.SunlightMarkup(new Vector2(size.X * 32, size.Y * 32)) + "</svg>");
                var sourceGradients = original.Descendants().Where(element => element.Name.LocalName is "linearGradient" or "radialGradient").ToArray();
                var nativeGradients = native.Descendants().Where(element => element.Name.LocalName is "linearGradient" or "radialGradient").ToArray();
                Require(sourceGradients.Length == nativeGradients.Length, "Source sunlight gradient count changed.");
                for (var index = 0; index < sourceGradients.Length; index++) CompareGradient(sourceGradients[index], nativeGradients[index]);
                Require(EcmaJson.Stringify(field) == before, "Presentation mutated the canonical projection.");
            }
            finally { lights.Hearths.Free(); lights.Sunlight.Free(); }
        }
        var fixture = new WorldMaterialsManifest();
        fixture.GroundTagFamilies.Add("settlement", new[] { "home" });
        fixture.TerrainTags.Add("field", Array.Empty<string>()); fixture.TerrainTags.Add("bridge", Array.Empty<string>());
        fixture.TerrainTags.Add("settlement", new[] { "home" });
        var edgePools = CandidateBoardLighting.Pools(new(3, 3), new[] { "bridge", "field", "settlement", "bridge", "bridge", "field", "field", "bridge", "field" }, fixture);
        Require(edgePools.Count == 3, "Bridge lamps must occur only at the head of each vertical run.");
        using var shapeImage = Image.CreateEmpty(96, 96, false, Image.Format.Rgba8);
        shapeImage.Fill(Colors.White);
        var clipped = CandidateBoardLighting.CreateField(new(3, 3), Enumerable.Repeat("field", 9).ToArray(), fixture, ImageTexture.CreateFromImage(shapeImage));
        Require(clipped.Sunlight.HasMeta("ground_clipped") && clipped.Hearths.GetChildCount() == 0, "Only source sunlight uses the playable-shape clip.");
        clipped.Hearths.Free(); clipped.Sunlight.Free();
    }

    private void CompareGradient(XElement source, XElement native)
    {
        Require(source.Name == native.Name, "Gradient kind changed.");
        foreach (var attribute in source.Attributes().Where(value => value.Name != "id"))
            Require(Math.Abs(float.Parse(attribute.Value, CultureInfo.InvariantCulture) - Attr(native, attribute.Name.LocalName)) < .00001, "Gradient geometry changed.");
        var a = source.Elements("stop").ToArray(); var b = native.Elements("stop").ToArray();
        Require(a.Length == b.Length, "Gradient stop count changed.");
        for (var index = 0; index < a.Length; index++)
        {
            Require((string?)a[index].Attribute("stop-color") == (string?)b[index].Attribute("stop-color"), "Gradient color changed.");
            Require(Math.Abs(Attr(a[index], "offset") - Attr(b[index], "offset")) < .00001, "Gradient offset changed.");
            Require(Math.Abs(Attr(a[index], "stop-opacity") - Attr(b[index], "stop-opacity")) < .00001, "Gradient opacity changed.");
        }
    }

    private void Shadows()
    {
        using var sheet = Image.CreateEmpty(8, 4, false, Image.Format.Rgba8);
        sheet.Fill(Colors.Transparent); sheet.SetPixel(1, 1, Colors.White); sheet.SetPixel(6, 2, Colors.White);
        var texture = ImageTexture.CreateFromImage(sheet);
        var first = new AtlasTexture { Atlas = texture, Region = new Rect2(0, 0, 4, 4) };
        var second = new AtlasTexture { Atlas = texture, Region = new Rect2(4, 0, 4, 4) };
        var sprite = new Sprite2D { Texture = first, Centered = false, Offset = new(-1, -3), Scale = new(-.5f, .5f), Position = new(23, 41), ZIndex = 87 };
        var cache = new CandidateShadowCache();
        var shadow = CandidateSpriteShadows.Create(sprite, CandidateShadowKind.Figure, 2, cache);
        sprite.AddChild(shadow);
        try
        {
            using var isolated = CandidateSpriteShadows.ReadFrame(first);
            Require(isolated.GetWidth() == 4 && isolated.GetHeight() == 4 && isolated.GetPixel(1, 1).A == 1 && isolated.GetPixel(2, 2).A == 0, "Atlas sampling leaked a neighboring animation frame.");
            var cached = shadow.Texture;
            shadow.Synchronize(sprite, CandidateShadowKind.Figure, 2);
            Require(ReferenceEquals(cached, shadow.Texture), "Repeated shadow synchronization missed its frame cache.");
            sprite.Texture = second; sprite.FlipH = true;
            shadow.Synchronize(sprite, CandidateShadowKind.Figure, 2);
            Require(!ReferenceEquals(cached, shadow.Texture) && shadow.FlipH, "Shadow did not track a changed frame/flip.");
            Require(sprite.Offset == new Vector2(-1, -3) && sprite.Position == new Vector2(23, 41) && sprite.Scale == new Vector2(-.5f, .5f) && sprite.ZIndex == 87, "Shadow changed actor anchor, placement or depth.");
            Require(shadow.ShowBehindParent && shadow.Scale == Vector2.One && shadow.Position == Vector2.Zero, "Shadow must remain a local behind-art child.");
            sprite.Texture = first; shadow.Synchronize(sprite, CandidateShadowKind.Figure, 2);
            Require(ReferenceEquals(cached, shadow.Texture), "Returning to an animation frame rebaked it.");
        }
        finally { sprite.Free(); cache.Dispose(); }
        using var pixel = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8); pixel.Fill(Colors.White);
        var drops = new[] { new CandidateSpriteShadows.Drop(new(1, 0), 0, new Color(0, 0, 0, 1)), new CandidateSpriteShadows.Drop(new(1, 0), 0, new Color(0, 0, 0, 1)) };
        var chain = CandidateShadowRaster.Shadows(pixel, drops, 1);
        using (chain.Image)
        {
            Require(chain.Image.GetPixel(chain.Padding + 1, chain.Padding).A == 1, "First shadow offset changed.");
            Require(chain.Image.GetPixel(chain.Padding + 2, chain.Padding).A == 1, "Chained shadows must sample the previous result, not just SourceAlpha.");
            Require(chain.Image.GetPixel(chain.Padding, chain.Padding).A == 0, "Shadow helper copied original art into its shadow layer.");
            Require(chain.Image.GetPixel(0, 0).A == 0, "Shadow padding has an opaque matte.");
        }
        var impulse = new float[21 * 21]; impulse[10 * 21 + 10] = 1;
        var blurred = CandidateShadowRaster.Blur(impulse, 21, 21, 1);
        Require(Math.Abs(blurred.Sum() - 1) < .00001 && blurred[10 * 21 + 9] == blurred[10 * 21 + 11], "Gaussian alpha convolution changed mass or symmetry.");
        Require(CandidateSpriteShadows.Drops(CandidateShadowKind.Figure)[0].Sigma == .65f, "CSS shadow stdDeviation must not be halved like box-shadow radius.");
        foreach (var kind in Enum.GetValues<CandidateShadowKind>())
        {
            var result = CandidateShadowRaster.Shadows(pixel, CandidateSpriteShadows.Drops(kind), 2);
            using (result.Image) Require(result.Image.GetWidth() > 1 && result.Image.GetPixel(0, 0).A == 0, $"Preset {kind} has clipped spill or matte.");
        }
        var assets = AssetCatalog.Load();
        var actor = assets.CreateSprite(assets.ResolveUnit("c01.swordsman", "campaign-torren"));
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var actorCache = new CandidateShadowCache();
        var actorShadow = CandidateSpriteShadows.Create(actor, CandidateShadowKind.Figure, 2, actorCache);
        actor.AddChild(actorShadow);
        Require(actorShadow.Texture.GetWidth() > actor.Texture.GetWidth(), "Real imported actor silhouette lacks filter spill.");
        GD.Print($"Real imported actor shadow bake: {clock.ElapsedMilliseconds}ms (headless CPU resource check).");
        var anotherActor = assets.CreateSprite(assets.ResolveUnit("c01.swordsman", "campaign-torren"));
        var anotherShadow = CandidateSpriteShadows.Create(anotherActor, CandidateShadowKind.Figure, 2, actorCache);
        anotherActor.AddChild(anotherShadow);
        Require(ReferenceEquals(actorShadow.Texture, anotherShadow.Texture), "Board-lifetime cache did not share equal actor frame shadows.");
        actor.Free(); anotherActor.Free(); actorCache.Dispose();
    }

    private void Compositions(JsonObject oracle)
    {
        using var red = Image.CreateEmpty(2, 1, false, Image.Format.Rgba8);
        using var blue = Image.CreateEmpty(2, 1, false, Image.Format.Rgba8);
        red.Fill(new Color(1, 0, 0, .5f)); blue.Fill(new Color(0, 0, 1, .5f));
        var union = CandidateFilterComposition.Compose(new[] {
            new CandidateFilterComposition.Layer(red, new(0, 0, 2, 1)),
            new CandidateFilterComposition.Layer(blue, new(0, 0, 2, 1)),
        }, 1);
        using (union.Image)
        {
            var color = union.Image.GetPixel(0, 0);
            Require(Math.Abs(color.A - .75f) < .008 && Math.Abs(color.R - 1f / 3) < .008 && Math.Abs(color.B - 2f / 3) < .008,
                "Compound source-over alpha/RGB changed.");
            Require(Math.Abs(color.A * .5f - .375f) < .005 && Math.Abs(color.A * .5f - .4375f) > .05,
                "Postcomposition opacity must not fade overlapping inputs independently.");
        }
        red.Fill(Colors.Red); blue.Fill(Colors.Blue);
        var clipped = CandidateFilterComposition.Compose(new[] {
            new CandidateFilterComposition.Layer(red, new(0, 0, 2, 1), new[] { new Rect2(0, 0, 1, 1) }),
            new CandidateFilterComposition.Layer(blue, new(0, 0, 2, 1), new[] { new Rect2(1, 0, 1, 1) }),
        }, 1);
        using (clipped.Image)
        {
            Require(clipped.Image.GetPixel(0, 0) == Colors.Blue && clipped.Image.GetPixel(1, 0) == Colors.Red,
                "Each junction input must be clipped before their ordered source-over union.");
            var filtered = CandidateFilterComposition.Filter(clipped, CandidateShadowKind.Standing);
            using (filtered.Image)
            {
                var sourcePoint = (Vector2I)(-filtered.Origin);
                Require(filtered.Image.GetPixel(sourcePoint.X, sourcePoint.Y) == Colors.Blue
                    && filtered.Image.GetPixel(sourcePoint.X + 1, sourcePoint.Y) == Colors.Red,
                    "The complete filter result changed opaque authored source RGB.");
            }
        }
        Require(red.GetPixel(0, 0) == Colors.Red && blue.GetPixel(1, 0) == Colors.Blue, "Composition mutated input art.");
        var body = XDocument.Parse("<svg>" + EcmaJson.ReadString(oracle["unitBody"]) + "</svg>");
        var ring = body.Descendants("ellipse").Single(element => (string?)element.Attribute("class") == "runtime-unit-team-ring");
        var contact = body.Descendants("ellipse").Single(element => (string?)element.Attribute("class") == "runtime-unit-contact-shadow");
        Require(Attr(ring, "cx") == 16 && Attr(ring, "cy") == 29.5f && Attr(ring, "rx") == 11 && Attr(ring, "ry") == 2.25f,
            "Source compound team-ring geometry changed.");
        Require(Attr(contact, "cx") == 16 && Attr(contact, "cy") == 29.2f && Attr(contact, "rx") == 12.5f && Attr(contact, "ry") == 4.2f,
            "Source compound contact geometry changed.");
        var style = EcmaJson.ReadString(oracle["style"]);
        var ringStyle = Regex.Match(style, @"\.candidate-map \.runtime-unit-team-ring\s*\{([^}]+)\}").Groups[1].Value;
        Require(Regex.IsMatch(ringStyle, @"stroke-width:\s*1\.45px;") && Regex.IsMatch(ringStyle, @"opacity:\s*(?:0)?\.72;"),
            "Source compound ring CSS override changed.");
        var contactStyle = Regex.Match(style, @"\.candidate-map \.runtime-unit-contact-shadow\s*\{([^}]+)\}").Groups[1].Value;
        Require(Regex.IsMatch(contactStyle, @"opacity:\s*0\.62;") && Regex.IsMatch(contactStyle, @"filter:\s*blur\(0\.55px\);"),
            "Source contact CSS opacity/blur override changed.");
        var fullBody = CandidateBoardLighting.UnitBody(Colors.Blue);
        using (fullBody.Image)
        {
            var belowTile = (Vector2I)((new Vector2(0, 1.25f) - fullBody.Origin) * fullBody.Density);
            Require(fullBody.Image.GetPixel(belowTile.X, belowTile.Y).B > .2f,
                "Compound body must not clip the team-ring stroke at the tile's lower boundary.");
        }
        using var blank = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
        var sourceTexture = ImageTexture.CreateFromImage(blank);
        var sprite = new Sprite2D { Texture = sourceTexture, Centered = false, Offset = new(-16, -31), Scale = Vector2.One };
        var cache = new CandidateShadowCache();
        var figure = CandidateSpriteShadows.CreateFigure(sprite, new Color("#3465ab"), cache);
        sprite.AddChild(figure);
        try
        {
            Require(figure.GetMeta("composition_inputs").AsInt32() == 3 && sprite.SelfModulate.A == 0 && sprite.Modulate.A == 1,
                "Compound figure must suppress its original draw without hiding the composed child.");
            using var image = figure.Texture.GetImage();
            var foot = (Vector2I)((new Vector2(0, -1.8f) / figure.Scale) - figure.Offset);
            Require(image.GetPixel(foot.X, foot.Y).A > .62f, "Figure filter did not receive the contact body's alpha when its strip is transparent.");
            var banner = DynamicStateView.CreateOwnerBanner("#3465ab", cache);
            var repeatBanner = DynamicStateView.CreateOwnerBanner("#3465ab", cache);
            Require(ReferenceEquals(banner.Texture, repeatBanner.Texture)
                && ReferenceEquals(banner.GetNode<Sprite2D>("SourceSpriteShadows").Texture, repeatBanner.GetNode<Sprite2D>("SourceSpriteShadows").Texture),
                "Recreated procedural banners must share source and filtered textures instead of growing a per-turn cache.");
            banner.Free(); repeatBanner.Free();
            var saved = figure.Texture;
            sprite.Modulate = new Color(1, 1, 1, .5f);
            figure.Synchronize(sprite, CandidateShadowKind.Figure, 1);
            Require(ReferenceEquals(saved, figure.Texture) && figure.Modulate == Colors.White,
                "Group opacity must be inherited once after composition, not baked into inputs or duplicated on the output.");
            var other = new Sprite2D { Texture = sourceTexture, Centered = false, Offset = sprite.Offset, Scale = sprite.Scale };
            var otherFigure = CandidateSpriteShadows.CreateFigure(other, Colors.Red, cache);
            other.AddChild(otherFigure);
            Require(!ReferenceEquals(figure.Texture, otherFigure.Texture), "Different team rings incorrectly shared a compound figure cache entry.");
            other.Free();
        }
        finally { sprite.Free(); cache.Dispose(); }
    }

    private void JunctionComposition()
    {
        bool Bridge(int x, int y) => x == 2 && y >= 1 && y <= 3 || y == 2 && x >= 1 && x <= 3;
        bool Route(int x, int y) => x == 2 && (y == 0 || y == 4) || y == 2 && (x == 0 || x == 4);
        var topology = CrossingRenderer.Topology(5, 5, Bridge, Route);
        var source = WorldMaterialsManifest.Load().Crossings!;
        var manifest = new CrossingManifest { Horizontal = source.Horizontal, Vertical = source.Vertical, Junction = source.Junction,
            VerticalLegacyIds = new[] { "stone-bridge-vertical" } };
        var root = new Node2D(); var repeated = new Node2D(); var cache = new CandidateShadowCache();
        try
        {
            var diagnostics = new List<string>();
            CrossingRenderer.Add(root, manifest, EnvironmentCatalog.Load(), path => ResourceLoader.Load<Texture2D>(path), topology, diagnostics, cache);
            var junction = root.GetChild<Node2D>(0);
            Require(diagnostics.Count == 0 && junction.GetChildCount() == 3, "Native junction lost its two immutable source handles or one composed drawing.");
            var sources = junction.GetChildren().OfType<Sprite2D>().Where(sprite => sprite.Material is ShaderMaterial).ToArray();
            var composed = junction.GetNode<Sprite2D>("SourceJunctionComposition");
            Require(sources.Length == 2 && sources.All(sprite => !sprite.Visible) && composed.Visible
                && composed.GetMeta("composition_inputs").AsInt32() == 2, "Junction renders duplicate sources/shadows instead of one clipped union.");
            Require(composed.GetMeta("shadow_kind").AsString() == "Standing" && junction.Position == new Vector2(80, 80),
                "Junction changed source filter ownership or registered pivot.");
            CrossingRenderer.Add(repeated, manifest, EnvironmentCatalog.Load(), path => ResourceLoader.Load<Texture2D>(path), topology, diagnostics, cache);
            Require(ReferenceEquals(composed.Texture, repeated.GetChild(0).GetNode<Sprite2D>("SourceJunctionComposition").Texture),
                "Repeated junction composition missed the explicit board cache.");
        }
        finally { root.Free(); repeated.Free(); cache.Dispose(); }
    }

    private void Contacts()
    {
        var unit = CandidateBoardLighting.CreateUnitContact();
        var structure = CandidateBoardLighting.CreateStructureContact(new Vector2(80, 96), .8f);
        try
        {
            Require((unit.Position + unit.Texture.GetSize() * unit.Scale / 2).IsEqualApprox(new(0, -1.8f)), "Unit shadow moved the source foot-relative center.");
            Require((structure.Position + structure.Texture.GetSize() * structure.Scale / 2).IsEqualApprox(Vector2.Zero), "Structure shadow moved the source foot.");
            using var image = unit.Texture.GetImage();
            Require(Math.Abs(image.GetPixel(image.GetWidth() / 2, image.GetHeight() / 2).A - .62f) < .005, "CSS unit-contact opacity override was multiplied instead of replacing .48.");
            using var mass = structure.Texture.GetImage();
            Require(Math.Abs(mass.GetPixel(mass.GetWidth() / 2, mass.GetHeight() / 2).A - .22f) < .005, "Structure contact uses the wrong shared shadow weight.");
        }
        finally { unit.Free(); structure.Free(); }
    }

    private static float Attr(XElement element, string attribute) => float.Parse(element.Attribute(attribute)!.Value, CultureInfo.InvariantCulture);
    private void Require(bool condition, string message) { _checks++; if (!condition) throw new InvalidOperationException(message); }
}
