#nullable enable
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Campaign;
using Empire.Core.Ai;
using Empire.Core.Geometry;
using Empire.Core.Tactics;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Presentation.Feedback;
using Godot;
using File = System.IO.File;

namespace Empire.Presentation;

public partial class CampaignPlayedFlowChecks
{
    private async Task CheckCarriedOpening(GrayFlagGame game)
    {
        var manual = File.ReadAllBytes(manualPath);
        var manualStamp = File.GetLastWriteTimeUtc(manualPath);
        var engine = Field<BattleEngine>(game, "engine");
        var definition = Field<JsonObject>(game, "definition");
        var levels = Field<JsonArray>(game, "levels");
        var rules = new CampaignApplicationRules(engine, Field<SessionPresentationText>(game, "presentationText"));
        var outcomes = new List<string>();
        var total = 0;
        var first = Campaign(game).Battle!.Snapshot()["levelId"]!.GetValue<string>();
        Require(first is "c01-01" or "c01-02", "Carried play starts only at the first battle or its verified second-battle continuation.");
        foreach (var level in new[] { "c01-01", "c01-02" }.SkipWhile(level => level != first))
        {
            var campaign = Campaign(game);
            var session = campaign.Battle!;
            var screen = View<BattleScreen>(game);
            Require(screen.Board.AnimationsEnabled && screen.Board.AdvanceAnimationsAutomatically,
                "Carried battle acceptance requires normal automatic presentation on every new battlefield.");
            Require(session.Snapshot()["levelId"]!.GetValue<string>() == level && session.Revision == 0,
                "Carried play must begin the expected untouched battle, not an independent fresh-level fixture.");
            CheckOpeningCarry(game, level == "c01-01" ? 430 : 460);
            if (level == "c01-02")
                Require(session.Snapshot()["scenario"]!["engagementRules"]!.AsArray().OfType<JsonObject>()
                    .Any(rule => rule["id"]!.GetValue<string>() == "bridge-truce" && rule["mode"]!.GetValue<string>() == "no-attacks"),
                    "The accepted bridge truce must remain an actual no-attacks rule in the carried second battle.");
            var campaignBefore = campaign.Snapshot();
            var automaticBefore = File.ReadAllBytes(automaticPath);
            var automaticStamp = File.GetLastWriteTimeUtc(automaticPath);
            var expectedEvents = new List<(long Revision, int Index)>();
            var presentedEvents = new List<(long Revision, int Index)>();
            var presentedOnBattle = true;
            var completionCount = 0;
            void Entered(Node child)
            {
                if (child is BattleResultScreen)
                    completionCount++;
            }
            game.ChildEnteredTree += Entered;
            screen.Board.EventPresented += (transition, index) =>
            {
                presentedEvents.Add((transition.Revision, index));
                presentedOnBattle &= ReferenceEquals(View<Control>(game), screen) && screen.IsPresenting;
            };
            var step = 0;
            var observedTruceEnd = false;
            var request = campaign.RequestSnapshot()!;
            while (!session.IsConcluded)
            {
                Require(step < 1024, "The current test-player policy did not conclude its carried battle within the legal-order bound.");
                var revision = session.Revision;
                var human = !session.IsComputerTurn;
                var action = human ? ChoosePlayedOrder(engine, session.Snapshot()) : engine.Ai.ChooseAction(session.Snapshot());
                if (human)
                {
                    await Until(() => !screen.IsInputBlocked, "The carried player's previous order did not finish playback.");
                    Require(!session.IsComputerTurn && !session.IsConcluded,
                        $"The current player no longer owns the carried battle at {level}:{step}.");
                    var dispatched = session.Dispatch(EcmaJson.ToElement(action), revision);
                    Require(dispatched.Accepted, $"Current queried player order rejected at {level}:{step}: {dispatched.Message}");
                    screen.Refresh();
                }
                else
                {
                    Require(session.IsComputerTurn && !session.IsConcluded, $"Normal AI controller no longer matches {level}:{step}.");
                    await Until(() => session.Revision != revision, "The normal AI pacer did not commit its next carried-battle order.");
                }
                Require(session.Revision == revision + 1 && EcmaJson.DeepEquals(session.ActionLog.Last(), action),
                    $"Current independent AI/player execution diverged from its canonical query at {level}:{step}.");
                var receipt = session.GetSnapshot().Transition!;
                expectedEvents.AddRange(Enumerable.Range(0, receipt.Events.Count).Select(index => (receipt.Revision, index)));
                if (level == "c01-02")
                {
                    static bool HasTruce(System.Text.Json.JsonElement state) => state.GetProperty("scenario").GetProperty("engagementRules")
                        .EnumerateArray().Any(rule => rule.GetProperty("id").GetString() == "bridge-truce");
                    if (HasTruce(receipt.Before) && !HasTruce(receipt.After))
                    {
                        Require(!observedTruceEnd && receipt.IsComputerAction && receipt.Before.GetProperty("turn").GetInt32() == 3
                            && receipt.Before.GetProperty("currentPlayer").GetInt32() == 2 && receipt.Action.GetProperty("kind").GetString() == "endTurn"
                            && receipt.Events.Any(value => value.GetProperty("type").GetString() == "scenarioSignal"
                                && value.GetProperty("signal").GetString() == "accepted_bridge_truce"),
                            "The live truce must end exactly on the authored third AI turn handoff, with its consequence in that receipt.");
                        observedTruceEnd = true;
                    }
                }
                if (++step % 40 == 0)
                    GD.Print($"CARRIED_PLAY_PROGRESS level={level}; legal orders={step}; turn={session.Snapshot()["turn"]}; state={StateDigest.Hash(session.Snapshot())}");
            }
            await Until(() => View<Control>(game) is BattleResultScreen, "Played victory did not complete normal terminal presentation.");
            game.ChildEnteredTree -= Entered;
            var terminal = session.Snapshot();
            GD.Print($"PLAYED_TERMINAL level={level}; legal orders={step}; outcome={campaign.BattleSummary().Outcome}; completion views={completionCount}; events={expectedEvents.Count}; presented={presentedEvents.Count}; turn={terminal["turn"]}; state={StateDigest.Hash(terminal)}; conclusion={EcmaJson.Stringify(terminal["conclusion"])}");
            Require(session.IsConcluded && campaign.BattleSummary().Outcome == "victory" && completionCount == 1,
                "Continuous legal play must produce its own victory and exactly one completion, never a written terminal fixture.");
            Require(presentedOnBattle && presentedEvents.SequenceEqual(expectedEvents),
                "Every carried-battle receipt must play every event once and in order before leaving the battlefield.");
            Require(EcmaJson.DeepEquals(campaign.Snapshot(), campaignBefore) && File.ReadAllBytes(automaticPath).SequenceEqual(automaticBefore)
                && File.GetLastWriteTimeUtc(automaticPath) == automaticStamp,
                "Winning the actual battle must not advance or autosave the campaign before explicit settlement.");
            if (level == "c01-02")
            {
                var safe = terminal["scenario"]!["zones"]!["east-safety"]!.AsArray().OfType<JsonObject>().Select(TacticalQueries.Position).ToHashSet();
                var civilians = terminal["units"]!.AsArray().OfType<JsonObject>()
                    .Where(unit => unit["key"]?.GetValue<string>().StartsWith("refugee-", StringComparison.Ordinal) == true).ToArray();
                Require(terminal["conclusion"]!["kind"]!.GetValue<string>() == "objectiveCompleted"
                    && terminal["conclusion"]!["objective"]!.GetValue<string>() == "primary" && civilians.Length == 3
                    && civilians.All(unit => Number(unit["hp"]!) > 0 && safe.Contains(TacticalQueries.Position(unit)))
                    && terminal["units"]!.AsArray().OfType<JsonObject>().All(unit => unit["key"]?.GetValue<string>() != "unmarked-ballista")
                    && terminal["players"]!.AsArray().OfType<JsonObject>().Any(player => player["id"]!.GetValue<int>() == 2 && player["alive"]!.GetValue<bool>()),
                    "The carried second victory must complete the actual evacuation and ballista objective with enemies remaining, not take an elimination shortcut.");
            }
            var reference = new GrayFlagCampaign(rules, definition, levels);
            reference.Load(campaign.Save());
            reference.FinishBattle();
            var settle = Button(View<BattleResultScreen>(game), "Settle");
            NativePress(settle);
            settle.EmitSignal(Godot.Button.SignalName.Pressed);
            await Layout();
            Require(!campaign.HasPendingBattle && EcmaJson.DeepEquals(campaign.Snapshot(), reference.Snapshot()),
                "Actual Settle must project the real outcome, roster, resources, growth and history exactly once through the campaign owner.");
            var settled = campaign.Snapshot();
            Require(Number(settled["resources"]!["supplies"]!) == (level == "c01-01" ? 6 : 7)
                && Number(settled["resources"]!["treasury"]!) == 5
                && settled["battleHistory"]!.AsArray().Count == (level == "c01-01" ? 1 : 2),
                "Consecutive victories must earn the authored supply reward once without replacing treasury or history.");
            CheckPlayedRoster(engine, request, settled, terminal);
            CheckAutomatic(game, definition);
            await Activate(Button(View<BattleResultScreen>(game), "Proceed"));
            await CompleteStories(game);
            if (level == "c01-01")
            {
                await Activate(Button(View<StoryScreen>(game), "Choice_accept-truce"));
                var state = campaign.Snapshot();
                Require(state["flags"]!.AsArray().Any(flag => flag!.GetValue<string>() == "accepted_bridge_truce")
                    && Number(state["relations"]!["cain"]!) == 1,
                    "The bridge choice must carry its authored flag and relationship into the second battle.");
            }
            else
            {
                Require(observedTruceEnd, "The second-battle route must actually observe the authored truce handoff.");
            }
            Require(campaign.Node()["id"]!.GetValue<string>() == (level == "c01-01" ? "battle-02" : "battle-03"),
                "Real story continuation must reach the next authored battle without skipping progress.");
            CheckAutomatic(game, definition);
            await Activate(Button(View<CampaignStagingScreen>(game), "StartBattle"));
            Require(File.ReadAllBytes(manualPath).SequenceEqual(manual) && File.GetLastWriteTimeUtc(manualPath) == manualStamp,
                "Played victories and carried continuation must not overwrite the existing manual checkpoint.");
            total += step;
            outcomes.Add($"{level}:{StateDigest.Hash(terminal)}:{EcmaJson.Stringify(terminal["conclusion"])}");
            GD.Print($"CARRIED_VICTORY_OK level={level}; legal orders={step}; events={expectedEvents.Count}; terminal={StateDigest.Hash(terminal)}; campaign={StateDigest.Hash(settled)}; next={campaign.Node()["id"]}");
        }
        CheckOpeningCarry(game, 490);
        var third = Campaign(game).Battle!;
        var thirdScreen = View<BattleScreen>(game);
        Require(thirdScreen.Board.AnimationsEnabled && thirdScreen.Board.AdvanceAnimationsAutomatically,
            "The third-battle opening must also retain normal automatic presentation.");
        var thirdCampaign = Campaign(game).Snapshot();
        var thirdSlots = SaveSlots();
        var thirdOrders = 0;
        while (!(third.Snapshot()["turn"]!.GetValue<int>() >= 2 && !third.IsComputerTurn && !thirdScreen.IsInputBlocked))
        {
            Require(thirdOrders < 200 && !third.IsConcluded, "The carried third battle did not complete an ordinary first round.");
            var revision = third.Revision;
            if (!third.IsComputerTurn)
            {
                await Until(() => !thirdScreen.IsInputBlocked, "Third-battle player playback did not settle.");
                if (third.Snapshot()["turn"]!.GetValue<int>() >= 2)
                    break;
                var action = engine.Ai.ChooseAction(third.Snapshot());
                Require(third.Dispatch(EcmaJson.ToElement(action), revision).Accepted, "The deterministic third-battle test player issued an illegal order.");
                thirdScreen.Refresh();
            }
            else
                await Until(() => third.Revision != revision, "The carried third-battle AI did not act normally.");
            thirdOrders++;
            await Until(() => !thirdScreen.IsPresenting, "The carried third-battle receipt did not finish its normal presentation.");
        }
        Require(!third.IsConcluded && ReferenceEquals(View<Control>(game), thirdScreen)
            && third.Snapshot()["levelId"]!.GetValue<string>() == "c01-03" && !thirdScreen.IsInputBlocked,
            "The carried third battle must remain live and playable after the first full round.");
        Require(EcmaJson.DeepEquals(Campaign(game).Snapshot(), thirdCampaign) && SaveSlots() == thirdSlots,
            "Playing the carried third-battle opening must not settle rewards or alter either save slot.");
        GD.Print($"CARRIED_OPENING_OK battles played in this process={total} legal orders; third opening={thirdOrders} orders; third state={StateDigest.Hash(third.Snapshot())}; outcomes={string.Join(";", outcomes)}. No third-battle victory or full-campaign claim.");
    }

    private void CheckOpeningCarry(GrayFlagGame game, int funds)
    {
        var campaign = Campaign(game);
        var engine = Field<BattleEngine>(game, "engine");
        var request = campaign.RequestSnapshot()!;
        var opening = campaign.Battle!.Snapshot();
        Require(EcmaJson.DeepEquals(opening, engine.CreateState(request["level"]!.AsObject()))
            && opening["players"]!.AsArray().OfType<JsonObject>().Single(player => player["id"]!.GetValue<int>() == 1)["resources"]!["funds"]!["current"]!.GetValue<int>() == funds,
            "A carried battle must open from its authoritative request, including context-derived funding.");
        foreach (var binding in request["rosterBindings"]!.AsArray().OfType<JsonObject>())
        {
            var roster = campaign.Snapshot()["roster"]![binding["campaignUnit"]!.GetValue<string>()]!;
            var unit = opening["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["key"]!.GetValue<string>() == binding["levelUnitKey"]!.GetValue<string>());
            var content = engine.Catalog.Definition("units", roster["unitType"]!.GetValue<string>())!.Value;
            var maximumMorale = content.TryGetProperty("morale", out var morale) && morale.TryGetProperty("maximum", out var maximum)
                ? maximum.GetDouble() : 100;
            Require(unit["type"]!.GetValue<string>() == roster["unitType"]!.GetValue<string>()
                && unit["hp"]!.GetValue<double>() == Math.Max(1, Math.Floor(content.GetProperty("maxHp").GetDouble() * roster["hpRatio"]!.GetValue<double>() + 0.5))
                && unit["morale"]!["current"]!.GetValue<double>() == Math.Max(1, Math.Floor(maximumMorale * roster["moraleRatio"]!.GetValue<double>() + 0.5)),
                "The next opening must reconstruct the carried survivor's actual health and morale, not heal or reset them.");
            Require(roster["disposition"]!.GetValue<string>() == "available" && EcmaJson.DeepEquals(unit["rank"], roster["rank"])
                && EcmaJson.DeepEquals(unit["rankProgress"], roster["rankProgress"] ?? JsonValue.Create(0))
                && EcmaJson.DeepEquals(unit["resources"], roster["resources"] ?? new JsonObject())
                && EcmaJson.DeepEquals(unit["career"]!["current"], roster["career"])
                && EcmaJson.DeepEquals(unit["career"]!["mastery"], roster["careerMastery"] ?? new JsonObject())
                && EcmaJson.DeepEquals(unit["career"]!["unlocked"], roster["unlockedCareers"] ?? new JsonArray())
                && EcmaJson.DeepEquals(unit["learnedAbilities"], roster["learnedAbilities"] ?? new JsonArray()),
                "Carried survivors must retain actual progression and resources when reconstructed for play.");
        }
        var declared = campaign.Node()["rosterBindings"]!.AsArray().OfType<JsonObject>().ToArray();
        var expectedBindings = declared.Where(binding => campaign.Snapshot()["roster"]![binding["campaignUnit"]!.GetValue<string>()]!["disposition"]!.GetValue<string>() == "available").ToArray();
        Require(EcmaJson.DeepEquals(request["rosterBindings"], new JsonArray(expectedBindings.Select(binding => binding.DeepClone()).ToArray())),
            "The next battle must include every available declared survivor and omit every unavailable casualty binding.");
        foreach (var binding in declared.Except(expectedBindings))
            Require(opening["units"]!.AsArray().OfType<JsonObject>().All(unit => unit["key"]!.GetValue<string>() != binding["levelUnitKey"]!.GetValue<string>()),
                "An unavailable casualty must not reappear as an unbound unit in the next battle.");
    }

    private void CheckPlayedRoster(BattleEngine engine, JsonObject request, JsonObject campaign, JsonObject terminal)
    {
        var roster = campaign["roster"]!;
        var progressed = false;
        foreach (var binding in request["rosterBindings"]!.AsArray().OfType<JsonObject>())
        {
            var id = binding["campaignUnit"]!.GetValue<string>();
            var key = binding["levelUnitKey"]!.GetValue<string>();
            var active = terminal["units"]!.AsArray().OfType<JsonObject>().SingleOrDefault(unit => unit["key"]!.GetValue<string>() == key)
                ?? terminal["embarkedUnits"]!.AsArray().OfType<JsonObject>().Select(entry => entry["unit"]!.AsObject())
                    .SingleOrDefault(unit => unit["key"]!.GetValue<string>() == key);
            var marker = terminal["markers"]!.AsArray().OfType<JsonObject>().SingleOrDefault(marker => marker["fallenUnit"]?["key"]?.GetValue<string>() == key);
            var unit = active ?? marker?["fallenUnit"] as JsonObject ?? throw new InvalidOperationException("Actual roster unit disappeared without a departure fact.");
            var disposition = active is not null ? "available" : GrayFlagCampaignPolicy.MarkerDispositions.GetValueOrDefault(marker!["kind"]!.GetValue<string>(), "missing");
            var heroRecovery = disposition != "available" && roster[id]!["tags"]!.AsArray().Any(tag => tag!.GetValue<string>() == "hero");
            var maxHp = engine.Catalog.Definition("units", unit["type"]!.GetValue<string>())!.Value.GetProperty("maxHp").GetDouble();
            var hpRatio = Math.Clamp(unit["hp"]!.GetValue<double>() / maxHp, 0, 1);
            var moraleRatio = Math.Clamp(unit["morale"]!["current"]!.GetValue<double>() / unit["morale"]!["maximum"]!.GetValue<double>(), 0, 1);
            Require(roster[id]!["disposition"]!.GetValue<string>() == (heroRecovery ? "available" : disposition)
                && roster[id]!["unitType"]!.GetValue<string>() == unit["type"]!.GetValue<string>()
                && Math.Abs(roster[id]!["hpRatio"]!.GetValue<double>() - (heroRecovery ? Math.Max(0.3, hpRatio) : hpRatio)) < 1e-12
                && Math.Abs(roster[id]!["moraleRatio"]!.GetValue<double>() - (heroRecovery ? Math.Max(0.4, moraleRatio) : moraleRatio)) < 1e-12,
                "Settlement must independently retain actual injury and departure facts, with only the authored hero recovery floor.");
            Require(EcmaJson.DeepEquals(roster[id]!["rank"], unit["rank"])
                && EcmaJson.DeepEquals(roster[id]!["rankProgress"], unit["rankProgress"])
                && EcmaJson.DeepEquals(roster[id]!["resources"], unit["resources"])
                && EcmaJson.DeepEquals(roster[id]!["career"], unit["career"]!["current"])
                && EcmaJson.DeepEquals(roster[id]!["unlockedCareers"], unit["career"]!["unlocked"])
                && EcmaJson.DeepEquals(roster[id]!["careerMastery"], unit["career"]!["mastery"])
                && EcmaJson.DeepEquals(roster[id]!["learnedAbilities"], unit["learnedAbilities"]),
                "Settlement must retain the played survivor/casualty's actual advancement and resources.");
            progressed |= unit["rankProgress"]!.GetValue<double>() > 0;
            GD.Print($"CARRIED_ROSTER unit={id}; disposition={roster[id]!["disposition"]}; hpRatio={roster[id]!["hpRatio"]}; moraleRatio={roster[id]!["moraleRatio"]}; rank={roster[id]!["rank"]}; progress={roster[id]!["rankProgress"]}");
        }
        Require(progressed, "The carried route must exercise actual nonzero progression, not only zero-value round trips.");
    }

    private void CheckAutomatic(GrayFlagGame game, JsonObject definition)
    {
        var saved = EcmaJson.Parse(File.ReadAllText(automaticPath))!;
        Require(EcmaJson.DeepEquals(CampaignSave.Load(saved, definition)["state"], Campaign(game).Snapshot()),
            "The automatic slot must contain the exact committed carried campaign.");
    }

    private async Task CompleteStories(GrayFlagGame game)
    {
        var beats = 0;
        while (Campaign(game).Node()["type"]!.GetValue<string>() == "story")
        {
            Require(++beats <= 40, "Authored continuation did not reach its next choice or battle.");
            await Activate(Button(View<StoryScreen>(game), "StoryPrimary"));
        }
    }

    private static JsonObject ChoosePlayedOrder(BattleEngine engine, JsonObject state)
    {
        if (state["levelId"]!.GetValue<string>() != "c01-02")
            return engine.Ai.ChooseAction(state, 0.58);
        var player = state["currentPlayer"]!.GetValue<int>();
        var context = new AiTurnContext(engine.Ai.Planning, state, player, 0.58);
        var actors = engine.Turns.Actors(state).Where(unit => engine.Turns.CanAct(state, unit)).ToArray();
        var healing = new List<(JsonObject Action, double Risk, int Priority, double Amount)>();
        foreach (var healer in actors.Where(unit => engine.Progression.Careers.UnitAbilityIds(unit).Contains("heal")))
        {
            var field = engine.Space.ComputeMoveField(state, healer);
            foreach (var stop in field.Stops)
            {
                var at = context.Board.CoordOf(stop);
                var path = field.TracePath(stop);
                if (path is null)
                    continue;
                foreach (var option in engine.CommandsAt(state, healer, at).Where(option => option.Ability == "heal"))
                    foreach (var target in option.Targets)
                    {
                        var ally = TacticalQueries.UnitAt(state, target)!;
                        var amount = engine.HealAmount(ally);
                        if (amount < 15)
                            continue;
                        var key = ally["key"]?.GetValue<string>() ?? "";
                        var priority = key == "campaign-laiya" || key.StartsWith("refugee-", StringComparison.Ordinal) ? 2 : 1;
                        healing.Add((new JsonObject
                        {
                            ["kind"] = "command",
                            ["unit"] = healer["id"]!.DeepClone(),
                            ["path"] = TacticalQueries.PathJson(path),
                            ["command"] = new JsonObject { ["ability"] = "heal", ["target"] = TacticalQueries.CoordinateJson(target) }
                        }, context.Threat.GetValueOrDefault(stop), priority, amount));
                    }
            }
        }
        if (healing.Count > 0)
            return healing.OrderBy(candidate => candidate.Risk).ThenByDescending(candidate => candidate.Priority)
                .ThenByDescending(candidate => candidate.Amount).First().Action;
        var safeCells = state["scenario"]!["zones"]!["east-safety"]!.AsArray().OfType<JsonObject>().Select(TacticalQueries.Position).ToArray();
        var bridge = state["scenario"]!["zones"]!["middle-bridge"]!.AsArray().OfType<JsonObject>().Select(TacticalQueries.Position).ToArray();
        var entry = bridge.OrderBy(cell => cell.Y).ThenBy(cell => cell.X).First();
        foreach (var civilian in actors.Where(unit => unit["key"]?.GetValue<string>().StartsWith("refugee-", StringComparison.Ordinal) == true))
        {
            var at = TacticalQueries.Position(civilian);
            var arrived = safeCells.Contains(at);
            var goals = at.Y < entry.Y ? new[] { entry } : safeCells;
            return QueriedWait(engine, context, civilian, goals, arrived ? safeCells : null);
        }
        var proposed = engine.Ai.ChooseAction(state, 0.58);
        if (proposed["kind"]!.GetValue<string>() != "command" || proposed["command"]!["ability"]!.GetValue<string>() != "wait")
            return proposed;
        // This test player uses the authored crossing instead of the old map's coordinates or a greedy river-bank approach.
        var actor = TacticalQueries.RequireUnit(state, proposed["unit"]!.GetValue<int>());
        var actorAt = TacticalQueries.Position(actor);
        if (actorAt.Y < entry.Y)
            return QueriedWait(engine, context, actor, new[] { entry });
        if (actorAt.X < safeCells.Min(cell => cell.X))
            return QueriedWait(engine, context, actor, safeCells);
        return proposed;
    }

    private static JsonObject QueriedWait(BattleEngine engine, AiTurnContext context, JsonObject unit, IReadOnlyList<Coord> goals, IReadOnlyCollection<Coord>? admitted = null)
    {
        var field = engine.Space.ComputeMoveField(context.State, unit);
        foreach (var stop in field.Stops.OrderBy(index =>
            context.Board.NearestDistance(context.Board.CoordOf(index), goals) * 10 + context.Threat.GetValueOrDefault(index) * 4)
            .ThenBy(index => field.Tiles[index].Cost))
        {
            var at = context.Board.CoordOf(stop);
            var path = field.TracePath(stop);
            if (path is null || admitted is not null && !admitted.Contains(at)
                || !engine.CommandsAt(context.State, unit, at).Any(option => option.Ability == "wait"))
                continue;
            return new JsonObject
            {
                ["kind"] = "command",
                ["unit"] = unit["id"]!.DeepClone(),
                ["path"] = TacticalQueries.PathJson(path),
                ["command"] = new JsonObject { ["ability"] = "wait" }
            };
        }
        throw new InvalidOperationException("The live test-player actor has no legal queried wait destination.");
    }

    private static double Number(JsonNode value) => EcmaJson.ToElement(value).GetDouble();

    private async Task Until(Func<bool> condition, string failure)
    {
        var deadline = Time.GetTicksMsec() + 30_000;
        while (!condition())
        {
            Require(Time.GetTicksMsec() < deadline, failure);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

}
