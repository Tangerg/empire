import { readFileSync, writeFileSync } from 'node:fs';
import { ContentPackInstaller, createBattleEngine, createContentCatalog } from '../../../packages/battle-engine/src/index';
import { IDLE } from '../../../packages/game-ui/src/ui/selection';
import { embarkUnit } from '../../../packages/battle-engine/src/transports';
import { GameSession } from '../../../packages/battle-engine/src/session';
import { areEnemies, recruitOptions, unitAt } from '../../../packages/battle-engine/src/state';
import { tacticOptions } from '../../../packages/battle-engine/src/commanders';
const packs = JSON.parse(readFileSync('godot/data/content-packs.json', 'utf8'));
packs.flatMap((p: any) => p.terrains ?? []).find((t: any) => t.id === 'plain').name = 'plain \ud800';
const content = createContentCatalog();
new ContentPackInstaller(content).install(...packs);
const engine = createBattleEngine({ content });
const original = JSON.parse(readFileSync('godot/tests/oracle/source-v1/grayflag-level.json', 'utf8'));
const level: any = { schema: 2, id: 'adapter', name: 'Adapter', width: 7, height: 4, terrain: ['.......', '.......', '.......', '.......'], owners: [], rules: {}, victory: [{ type: 'routEnemies' }], players: original.players.map((p: any) => ({ ...p, controller: 'human', objectives: undefined })), units: [{ key: 'ally', unit: 'soldier', owner: 1, x: 1, y: 1 }, { key: 'enemy', unit: 'soldier', owner: 2, x: 2, y: 1 }, { key: 'remote', unit: 'soldier', owner: 2, x: 6, y: 3 }] };
const cases: any[] = [];
function sample(name: string, change: (s: any) => void = () => { }, customLevel = level) {
    const session = new GameSession(customLevel, engine);
    change(session.state);
    const s = session.state;
    const board = engine.rules.space.board(s);
    const viewer = s.players.find(p => p.controller === 'human')?.id ?? s.currentPlayer;
    const visible = new Set(session.visibleUnits(viewer).map(u => u.id));
    const human = s.players.find(p => p.id === s.currentPlayer)?.controller === 'human';
    const coords = Array.from({ length: s.map.tiles.length }, (_, i) => board.coordOf(i));
    const roster = session.deploymentRoster();
    const units = s.units.map(u => ({ id: u.id, visible: visible.has(u.id), at: { x: u.x, y: u.y }, canAct: engine.canAct(s, u), enemyOfViewer: areEnemies(s, u.owner, viewer) }));
    const actors = engine.actors(s).filter(u => visible.has(u.id)).map(u => u.id);
    const facts = actors.map(id => {
        const unit = session.unit(id)!;
        const stops = [...session.moveField(unit).stops].map(index => board.coordOf(index));
        const positions = stops.map(at => {
            const commands = session.commandsAt(unit, at);
            const attacks = commands.flatMap(command => command.weapon ? command.targets.map(target => {
                const plan = session.attackPlan(unit, target, at, command.weapon);
                return {
                    target, weapon: command.weapon,
                    strikeDamage: plan.primaryUnit?.strike.damage ?? 0,
                    splashDamage: plan.unitHits.filter(hit => !hit.primary).reduce((sum, hit) => sum + hit.damage.damage, 0) +
                        plan.structureHits.filter(hit => !hit.primary).reduce((sum, hit) => sum + hit.forecast.damage, 0),
                    counterDamage: plan.primaryUnit?.counter?.damage ?? 0,
                    terrainDefense: content.terrains.get(s.map.tiles[board.indexOf(at)]).defense,
                    hasPrimaryUnit: !!plan.primaryUnit, affectedCells: plan.affectedCells,
                };
            }) : []);
            return {
                at, path: session.pathTo(unit, at), attacks,
                commands: commands.map(command => ({
                    key: command.key, ability: command.ability, weapon: command.weapon ?? null,
                    selfTargeted: command.selfTargeted,
                    hostile: engine.rules.abilities.get(command.ability).engagement !== null,
                    targets: command.targets,
                })),
            };
        });
        return {
            id, stops, positions,
            threat: [...session.threatOf(unit)].map(index => board.coordOf(index)),
            control: [...session.controlZoneAgainst(unit)].map(index => board.coordOf(index)),
            passengers: session.passengerOptions(unit).map(option => ({ id: option.unit.id, spots: option.spots })),
        };
    });
    cases.push({
        name, state: s, humanInput: human && s.phase !== 'over', humanTurn: human && s.phase === 'playing',
        units, actors, facts,
        tiles: coords.map(at => ({
            at, recruit: human && s.phase === 'playing' && !unitAt(s, at) && recruitOptions(engine.rules, s, at).length > 0,
            steps: board.grid.directions.map(direction => board.grid.step(at, direction.id)).filter(to => board.contains(to)),
        })),
        deploymentUnits: roster?.units.map(unit => unit.id) ?? [], deploymentZone: roster?.zone ?? [],
        deploymentSpots: roster?.units.map(unit => ({ id: unit.id, spots: session.deploymentSpots(unit).map(spot => spot.at) })) ?? [],
        tactics: s.commanders.flatMap(commander => tacticOptions(engine.rules, s, commander.id)
            .map(tactic => ({ commander: commander.id, id: tactic.id, targets: tactic.targets }))),
    });
}
sample('square4');
// Preserve the old malformed fixture as an admission-refusal vector. Valid
// comparisons author the tiling before unit initialization chooses its facing.
const malformedHexState = structuredClone(cases[0].state);
malformedHexState.rules.grid = 'hex';
for (const grid of ['square8', 'hex'])
    sample(grid, () => { }, { ...level, rules: { ...level.rules, grid } });
sample('fog', s => { s.rules.fog = true; });
sample('ai', s => s.players[0].controller = 'ai');
sample('current AI with first human viewer', s => { s.currentPlayer = 2; s.players[1].controller = 'ai'; });
sample('no human current second player', s => { s.currentPlayer = 2; for (const player of s.players) player.controller = 'ai'; });
sample('allied owner is not enemy', s => { s.players[1].team = s.players[0].team; });
sample('AI fog first human viewer', s => { s.currentPlayer = 2; s.players[1].controller = 'ai'; s.rules.fog = true; });
sample('over', s => { s.phase = 'over'; s.winner = 1; });
for (const mode of ['normal', 'poor', 'capped', 'occupied', 'foreign'])
    sample('recruit ' + mode, s => { s.map.tiles[0] = 'castle'; s.map.owners[0] = mode === 'foreign' ? 2 : 1; if (mode === 'poor')
        s.players[0].resources.funds.current = 0; if (mode === 'capped')
        s.rules.maxUnitsPerPlayer = 1; if (mode === 'occupied') {
        s.units[0].x = 0;
        s.units[0].y = 0;
    } });
sample('deployment', () => { }, { ...level, scenario: { zones: [{ id: 'front', cells: [{ x: 1, y: 1 }, { x: 0, y: 1 }] }] }, deployment: { order: [1], zones: [{ player: 1, zone: 'front' }] } });
sample('tactics', s => { s.commanders = [{ id: 'commander\ud800', unitId: s.units[0].id, owner: 1, radius: 3, aura: { attackMultiplier: 1, defenseDelta: 0, movementDelta: 0 }, turnGrants: [], tactics: ['steady', 'rally'], usedTactics: [] }]; });
sample('transport', s => { embarkUnit(engine.rules, s, s.units[0].id, s.units[1].id, () => { }); }, { ...level, units: [{ key: 'passenger', unit: 'c01.swordsman', owner: 1, x: 1, y: 1 }, { key: 'carrier', unit: 'c01.supply-wagon', owner: 1, x: 2, y: 1 }, level.units[2]] });
sample('splash and cover', s => { s.map.tiles[8] = 'forest'; }, { ...level, units: [{ ...level.units[0], unit: 'mage' }, level.units[1], { ...level.units[2], x: 3, y: 1 }] });
const flows: any[] = [];
for (const name of ['square4', 'tactics', 'transport', 'deployment', 'recruit normal', 'recruit poor', 'recruit capped']) {
    const fixture = cases.find(c => c.name === name);
    const session = new GameSession(level, engine);
    session.state = structuredClone(fixture.state);
    const s = session.state;
    let action: any;
    let input: any;
    if (name === 'square4') {
        const unit = s.units[0], target = s.units[1];
        const context: any = { session, state: s, isHumanTurn: true, cursor: null, hoverTarget: null, canAct: (u: any) => engine.canAct(s, u), isVisible: (u: any) => session.isUnitVisible(1, u) };
        const picked = IDLE.click(context, { x: unit.x, y: unit.y }).selection;
        const aimed = picked.click(context, { x: target.x, y: target.y }).selection;
        action = aimed.click(context, { x: target.x, y: target.y }).action;
        input = { unit: { x: unit.x, y: unit.y }, target: { x: target.x, y: target.y } };
    }
    else if (name === 'tactics') {
        const commander = s.commanders[0];
        const tactic = tacticOptions(engine.rules, s, commander.id)[0];
        action = { kind: 'tactic', commander: commander.id, tactic: tactic.id, target: tactic.targets[0] };
    }
    else if (name === 'transport') {
        const carrier = s.units.find(u => session.passengerOptions(u).length)!;
        const passenger = session.passengerOptions(carrier)[0];
        action = { kind: 'disembark', carrier: carrier.id, unit: passenger.unit.id, at: passenger.spots[0] };
    }
    else if (name === 'deployment') {
        const unit = session.deploymentRoster()!.units[0];
        action = { kind: 'deployUnit', unit: unit.id, at: session.deploymentSpots(unit).at(-1)!.at };
    }
    else {
        action = { kind: 'recruit', unit: recruitOptions(engine.rules, s, { x: 0, y: 0 })[0].unit, at: { x: 0, y: 0 } };
    }
    const accepted = session.tryDispatch(action) !== null;
    flows.push({ name, input, action, accepted, state: session.state, events: session.log });
}
writeFileSync(process.argv[2], JSON.stringify({ packs, level, cases, flows, malformedHexState }));
