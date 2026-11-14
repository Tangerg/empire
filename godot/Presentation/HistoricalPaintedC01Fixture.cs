#nullable enable
using System;
using System.Text.Json.Nodes;
using Empire.Core.Determinism;

namespace Empire.Presentation;

/// <summary>
/// Read-only historical renderer fixture, never registered as a playable campaign level
/// or selected as a C01 fallback. The sole frozen level lives under export-excluded tests.
/// </summary>
internal static class HistoricalPaintedC01Fixture
{
    internal static JsonObject Level
    {
        get
        {
            var level = PresentationJson.Load<JsonObject>("res://tests/native/c01-painted-historical-level.json");
            if (EcmaJson.Sha256(level) != "ba5f3eb89fd6cd786ddb9cd4c6fe8ac90b19285343207d877ec1e20638894327")
                throw new InvalidOperationException("The detached historical painted fixture was changed.");
            return level;
        }
    }

    // A detached technical scene, not the current shipped C01 manifest. Existing source
    // artwork is borrowed only by renderer checks; no duplicate legacy art is created.
    internal static AuthoredScene Scene => new()
    {
        LevelId = "c01-01",
        MapSize = [36, 30],
        OpeningFocus = [13, 14],
        HudPresentationPath = "res://data/c01-hud-presentation.json",
        RuntimeReady = true,
        AssembledGroundPath = "res://data/scenes/c01-01-assembled-ground.json",
        FramePlacements = []
    };
}
