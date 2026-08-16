import { describe, expect, it } from 'vitest';
import { createBattleEngine } from '../plugins/default';
import { BATTLE_SAVE_SCHEMA, BattleSaveMigrator, createBattleSave } from '../battle-save';
import { hashState } from '../replay';
import { GameSession } from '../session';
import { TEST_CONTENT, makeLevel, u } from './fixtures';
import type { Action, GameState, LevelData } from '../types';

const engine = () => createBattleEngine({ content: TEST_CONTENT });

const skirmish = (): LevelData =>
  makeLevel(['C..v..', '.T..T.', '..h...', 'v....C'], {
    units: [
      u(1, 0, 'soldier', 1),
      u(0, 2, 'archer', 1),
      u(4, 3, 'soldier', 2),
      u(5, 2, 'knight', 2),
    ],
    owners: [
      { x: 0, y: 0, owner: 1 },
      { x: 5, y: 3, owner: 2 },
      { x: 3, y: 0, owner: 0 },
    ],
    funds: [200, 200],
  });

/** Plays a while with both sides on the AI, so the state has some history. */
function battleInProgress(battle = engine(), actions = 24): GameState {
  const state = battle.createState(skirmish(), { seed: 99 });
  for (const player of state.players) player.controller = 'ai';
  for (let index = 0; index < actions && state.phase === 'playing'; index++) {
    battle.dispatch(state, battle.chooseAiAction(state));
  }
  return state;
}

/** JSON is the only shape a save ever really arrives in. */
const throughDisk = (value: unknown): unknown => JSON.parse(JSON.stringify(value));

describe('a battle interrupted mid-play', () => {
  it('resumes into exactly the battle that was saved', () => {
    const battle = engine();
    const state = battleInProgress(battle);
    const digest = hashState(state);

    const resumed = battle.loadBattle(throughDisk(battle.saveBattle(state)));
    expect(hashState(resumed)).toBe(digest);

    // And it is a battle, not a picture of one: play continues identically,
    // seeded stream included.
    for (let index = 0; index < 12 && state.phase === 'playing'; index++) {
      battle.dispatch(state, battle.chooseAiAction(state));
      battle.dispatch(resumed, battle.chooseAiAction(resumed));
    }
    expect(hashState(resumed)).toBe(hashState(state));
  });

  it('can be put down before the first turn and picked up still deploying', () => {
    // A phase that no interface could reach was a phase nothing ever saved. Now
    // that the board opens on it, a save taken there has to come back as a
    // deployment — not as a battle that has quietly already started.
    const battle = engine();
    const state = battle.createState(makeLevel(['....'], {
      units: [{ ...u(0, 0, 'soldier', 1), key: 'left' }, u(3, 0, 'soldier', 2)],
      scenario: { zones: [{ id: 'front', cells: [{ x: 0, y: 0 }, { x: 1, y: 0 }] }] },
      deployment: { order: [1], zones: [{ player: 1, zone: 'front' }] },
    }));
    expect(state.phase).toBe('deployment');

    const resumed = battle.loadBattle(throughDisk(battle.saveBattle(state)));
    expect(hashState(resumed)).toBe(hashState(state));
    expect(resumed.phase).toBe('deployment');

    // And it is still an arrangement in progress, not a picture of one.
    const unit = resumed.units.find((candidate) => candidate.key === 'left')!;
    battle.dispatch(resumed, { kind: 'deployUnit', unit: unit.id, at: { x: 1, y: 0 } });
    battle.dispatch(resumed, { kind: 'finishDeployment' });
    expect(resumed.phase).toBe('playing');
    expect(resumed.turn).toBe(1);
  });

  it('carries a header a slot list can read without loading the battle', () => {
    const battle = engine();
    const save = battle.saveBattle(battleInProgress(battle), '2026-01-01T00:00:00.000Z');
    expect(save).toMatchObject({
      schema: BATTLE_SAVE_SCHEMA,
      savedAt: '2026-01-01T00:00:00.000Z',
      battle: { levelId: 'test', phase: 'playing' },
    });
    expect(new BattleSaveMigrator().header(throughDisk(save)).turn).toBe(save.battle.turn);
  });

  it('is a document, not a view of the live battle', () => {
    const battle = engine();
    const state = battleInProgress(battle);
    const save = battle.saveBattle(state);
    const before = save.state.units[0].hp;
    state.units[0].hp = 1;
    expect(save.state.units[0].hp).toBe(before);
  });

  it('refuses a save the ruleset cannot honour, before replacing anything', () => {
    const battle = engine();
    const session = new GameSession(skirmish(), battle);
    const good = session.save();
    const digest = hashState(session.state);

    const foreignRule = throughDisk(good) as { state: GameState };
    foreignRule.state.units[0].directive = { mode: 'nonesuch', waypoints: [], cursor: 0 };
    expect(() => session.load(foreignRule)).toThrow(/未注册的常驻命令/);

    const foreignContent = throughDisk(good) as { state: GameState };
    foreignContent.state.units[0].type = 'archmage';
    expect(() => session.load(foreignContent)).toThrow(/目录里没有的「archmage」/);

    const truncated = throughDisk(good) as { state: unknown };
    truncated.state = { units: [] };
    expect(() => session.load(truncated)).toThrow(/不是一场战斗/);

    // Every refusal happened before the session's own battle was touched.
    expect(hashState(session.state)).toBe(digest);
  });

  it('names the field a damaged save is missing, whichever field it is', () => {
    // The shape check used to list six of the state's fields by hand, so a save
    // missing any of the other seventeen got past it and blew up in whatever
    // check reached the field first — a `TypeError`, which by the error contract
    // means "the caller is a defect", for a document that is merely corrupt.
    const battle = engine();
    const good = battle.saveBattle(battleInProgress(battle));

    const withoutField = (field: string): unknown => {
      const raw = throughDisk(good) as { state: Record<string, unknown> };
      delete raw.state[field];
      return raw;
    };

    for (const field of Object.keys(good.state)) {
      expect(() => battle.loadBattle(withoutField(field)))
        .toThrow(new RegExp(`战斗的「${field}」缺失或损坏`));
    }

    // And the nested aggregates the checks walk straight into answer for
    // themselves too, rather than being taken on trust because the parent existed.
    const noTiles = throughDisk(good) as { state: GameState };
    delete (noTiles.state.map as Partial<GameState['map']>).tiles;
    expect(() => battle.loadBattle(noTiles)).toThrow(/地图的「tiles」缺失或损坏/);

    const noSeed = throughDisk(good) as { state: GameState };
    (noSeed.state.random as Partial<GameState['random']>).seed = undefined;
    expect(() => battle.loadBattle(noSeed)).toThrow(/随机流的「seed」缺失或损坏/);

    // A battle that never had a deployment is not a damaged one.
    const noDeployment = throughDisk(good) as { state: GameState };
    noDeployment.state.deployment = null;
    expect(() => battle.loadBattle(noDeployment)).not.toThrow();
  });

  it('walks a save up its schema ladder, and refuses a gap', () => {
    const battle = engine();
    const current = battle.saveBattle(battleInProgress(battle));
    const older = { ...throughDisk(current) as object, schema: 0 };

    expect(() => battle.loadBattle(older)).toThrow(/no battle save migration from schema 0/);

    const ladder = new BattleSaveMigrator()
      .register(0, (raw) => ({ ...raw, schema: 1 }));
    expect(ladder.load(older, battle.rules).battle.levelId).toBe('test');
    expect(() => new BattleSaveMigrator().register(0, (raw) => raw).load(older, battle.rules))
      .toThrow(/did not advance schema/);
    expect(() => battle.loadBattle({ ...throughDisk(current) as object, schema: 7 }))
      .toThrow(/unsupported battle save schema 7/);
  });

  it('is a rule like any other, and can be replaced', () => {
    // A game that ships its own state shape registers its own migration on its
    // own engine, without a global ladder every engine in the process shares.
    const saves = new BattleSaveMigrator().register(0, (raw) => ({ ...raw, schema: 1 }));
    const battle = createBattleEngine({ content: TEST_CONTENT, saves });
    const older = { ...throughDisk(battle.saveBattle(battleInProgress(battle))) as object, schema: 0 };

    expect(battle.loadBattle(older).levelId).toBe('test');
    expect(() => engine().loadBattle(older)).toThrow(/no battle save migration/);
  });

  it('keeps the sitting out of the save', () => {
    // Undo history and the battle log belong to this sitting, not to the battle.
    const session = new GameSession(skirmish(), engine());
    const wait: Action = {
      kind: 'command',
      unit: session.state.units[0].id,
      path: [{ x: 1, y: 0 }],
      command: { ability: 'wait' },
    };
    session.dispatch(wait);
    expect(session.canUndo).toBe(true);

    session.load(throughDisk(session.save()));
    expect(session.canUndo).toBe(false);
    expect(session.log).toEqual([]);
    expect(Object.keys(createBattleSave(session.state))).toEqual(
      ['schema', 'battle', 'savedAt', 'state'],
    );
  });
});
