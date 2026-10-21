"""Compile isolated bypasses and require each intended observable parity assertion to reject them."""
import pathlib
import shutil
import subprocess
import sys

root, scratch = map(pathlib.Path, sys.argv[1:])
effects = 'Combat/CombatEffects.cs'
damage = 'Combat/BattleDamage.cs'
departures = 'Lifecycle/UnitDepartureHandlers.cs'
mutants = [
    ('recreate-departure-catalog', damage,
     'departureRules = new(rules.Content, rules.Grids, this, rules.UnitDepartures);',
     'departureRules = new(new ReadOnlyContent(rules.Content.CopyPacks()), rules.Grids, this, rules.UnitDepartures);',
     'departure dependencies: exact injected identity'),
    ('hardcoded-rank-policy', effects,
     'public int RankFor(double progress) => progression.RankFor(progress);',
     'public int RankFor(double progress) => new ThresholdRankProgressionPolicy().RankFor(progress);',
     'rank sequence {"veteran":10,"elite":30}: events'),
    ('hardcoded-threshold-query', effects,
     'public double? NextRankThreshold(int rank) => progression.NextThreshold(rank);',
     'public double? NextRankThreshold(int rank) => new ThresholdRankProgressionPolicy().NextThreshold(rank);',
     'policy {"veteran":10,"elite":30}: result'),
    ('drop-career-award', effects, '        AwardCareerProgress(unit, gained, emit);', '',
     'rank sequence {}: events'),
    ('allow-rank-demotion', effects, '        if (next <= previous) return;',
     '        if (next == previous) return;', 'rank sequence {"kind":"custom"}: events'),
    ('rank-event-before-career', effects,
     '        AwardCareerProgress(unit, gained, emit);\n'
     '        emit(new JsonObject { ["type"] = "rankProgressChanged", ["unit"] = unit["id"]!.DeepClone(),\n'
     '            ["amount"] = gained, ["current"] = unit["rankProgress"]!.DeepClone() });',
     '        emit(new JsonObject { ["type"] = "rankProgressChanged", ["unit"] = unit["id"]!.DeepClone(),\n'
     '            ["amount"] = gained, ["current"] = unit["rankProgress"]!.DeepClone() });\n'
     '        AwardCareerProgress(unit, gained, emit);', 'rank sequence {}: events'),
    ('reverse-departure-order', departures, 'foreach (var handler in All()) handler.Handle(departure);',
     'foreach (var handler in All().Reverse()) handler.Handle(departure);',
     'departure default commander nested: events'),
    ('bypass-departure-registry', damage,
     'departureRules.UnitDepartures.Announce(new(departureRules, state, unit, emit));',
     'DefaultUnitDepartureHandlers.Create().Announce(new(departureRules, state, unit, emit));',
     'departure append commander nested: events'),
    ('duplicate-default-consequences', damage,
     'departureRules.UnitDepartures.Announce(new(departureRules, state, unit, emit));',
     'AnnounceTwice(state, unit, emit);\n\n'
     '    private void AnnounceTwice(JsonObject state, JsonObject unit, Action<JsonObject> emit)\n'
     '    {\n'
     '        DefaultUnitDepartureHandlers.Create().Announce(new(departureRules, state, unit, emit));\n'
     '        departureRules.UnitDepartures.Announce(new(departureRules, state, unit, emit));\n'
     '    }', 'departure default commander nested: events'),
    ('drop-lethal-departure', damage, '            AnnounceDeparture(state, casualty, emit);', '',
     'departure default commander nested: events'),
    ('drop-rout-departure', damage,
     '            ["type"] = "unitRouted", ["unit"] = id,\n'
     '            ["marker"] = StateJson.Integer(marker, "id"), ["at"] = Coordinate(at),\n'
     '        });\n        AnnounceDeparture(state, unit, emit);',
     '            ["type"] = "unitRouted", ["unit"] = id,\n'
     '            ["marker"] = StateJson.Integer(marker, "id"), ["at"] = Coordinate(at),\n'
     '        });', 'departure default commander nested: events'),
    ('drop-surrender-departure', damage,
     '        emit(surrendered);\n        AnnounceDeparture(state, unit, emit);', '        emit(surrendered);',
     'custom departure surrender route: events'),
    ('drop-scenario-withdraw-departure', damage,
     '        AnnounceDeparture(state, unit, emit);\n'
     '        emit(new JsonObject { ["type"] = "unitWithdrawn", ["unit"] = unitId, ["at"] = Coordinate(at) });',
     '        emit(new JsonObject { ["type"] = "unitWithdrawn", ["unit"] = unitId, ["at"] = Coordinate(at) });',
     'custom departure withdraw route: events'),
    ('swallow-departure-failure', departures,
     'foreach (var handler in All()) handler.Handle(departure);',
     'foreach (var handler in All()) { try { handler.Handle(departure); } catch (DomainInvariantException) { } }',
     'departure throw commander nested: error'),
    ('clone-snapshot-per-handler', departures,
     'foreach (var handler in All()) handler.Handle(departure);',
     'foreach (var handler in All()) handler.Handle(departure with { Unit = departure.Unit.DeepClone().AsObject() });',
     'departure snapshot shared by handlers but detached from marker: event-hashes'),
    ('share-departure-marker-snapshot', damage,
     '            corpse = CreateMarker(state, target, "corpse", at);',
     '            corpse = CreateMarker(state, target, "corpse", at);\n'
     '            casualty = StateJson.Object(corpse["fallenUnit"], "fallenUnit");',
     'departure snapshot shared by handlers but detached from marker: event-hashes'),
]
for name, file, before, after, expected in mutants:
    target = scratch / ('mutant-' + name)
    core = target / 'Core'
    shutil.copytree(root / 'godot/Empire.Core', core,
                    ignore=shutil.ignore_patterns('obj', 'bin', '*.uid'))
    path = core / file
    original = path.read_text()
    if original.count(before) != 1:
        raise RuntimeError(f'Guard target must match exactly once: {name}')
    path.write_text(original.replace(before, after))
    build = subprocess.run(['dotnet', 'build', str(scratch / 'DepartureRankChecks.csproj'),
        '--configfile', str(scratch / 'NuGet.Config'), '--disable-build-servers', '--nologo',
        f'-p:EmpireRoot={root}', f'-p:EmpireCoreRoot={core}', f'-p:OutputPath={target / "bin"}'],
        capture_output=True, text=True)
    (target / 'build.log').write_text(build.stdout + build.stderr)
    if build.returncode:
        raise RuntimeError(f'Mutant did not compile: {name}; inspect {target / "build.log"}')
    check = subprocess.run(['dotnet', str(target / 'bin/DepartureRankChecks.dll'), str(scratch / 'oracle.json')],
                           capture_output=True, text=True)
    log = check.stdout + check.stderr
    (target / 'guard.log').write_text(log)
    if check.returncode == 0 or 'Mismatch in ' + expected not in log:
        raise RuntimeError(f'Mutant missed its intended assertion: {name}; {log}')
    print(f'PASS deliberate semantic mutant: {name}', flush=True)
