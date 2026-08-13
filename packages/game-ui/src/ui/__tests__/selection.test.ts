import { describe, expect, it } from 'vitest';
import { createBattleEngine } from '@empire/battle-engine';
import { normaliseLevel } from '@empire/battle-engine/mapio';
import { GameSession } from '@empire/battle-engine/session';
import { createTestCatalog } from '@empire/test-content';
import type { Coord, LevelData, Unit } from '@empire/battle-engine/types';
import { emptyOverlay } from '../board';
import {
  DestinationSelection,
  IDLE,
  RecruitSelection,
  TacticTargetSelection,
  TargetSelection,
  UnitSelection,
  type Selection,
  type SelectionContext,
} from '../selection';

/**
 * Selections answer for themselves, so they can be asked without a browser.
 * The controller used to hold these answers and needed a mounted DOM, a board
 * and a HUD before a single one of them could be checked.
 */

const CONTENT = createTestCatalog();
const ENGINE = createBattleEngine({ content: CONTENT });

const level = (): LevelData => normaliseLevel({
  schema: 2,
  id: 'selection',
  name: 'selection',
  width: 6,
  height: 2,
  terrain: ['..b...', '......'],
  owners: [{ x: 2, y: 0, owner: 1 }],
  units: [
    { x: 0, y: 0, unit: 'soldier', owner: 1 },
    { x: 3, y: 0, unit: 'soldier', owner: 2 },
  ],
  players: [
    { id: 1, name: 'P1', team: 1, color: '#3f7fd8', controller: 'human', resources: { funds: { current: 9999, capacity: null } } },
    { id: 2, name: 'P2', team: 2, color: '#d8483f', controller: 'ai', resources: {} },
  ],
  rules: {},
  victory: [{ type: 'routEnemies' }],
});

function context(session: GameSession, cursor: Coord | null = null, selection: Selection = IDLE): SelectionContext {
  const targets = selection.targets;
  return {
    session,
    state: session.state,
    isHumanTurn: true,
    cursor,
    hoverTarget: cursor && targets.some((t) => t.x === cursor.x && t.y === cursor.y) ? cursor : null,
    canAct: (unit: Unit) => session.engine.canAct(session.state, unit),
    isVisible: () => true,
  };
}

const session = () => new GameSession(level(), ENGINE);

describe('picking something', () => {
  it('takes command of a unit that may act', () => {
    const s = session();
    const outcome = IDLE.click(context(s), { x: 0, y: 0 });
    expect(outcome.selection).toBeInstanceOf(UnitSelection);
    expect(outcome.selection.unitId).toBe(s.state.units[0].id);
    expect(outcome.action).toBeUndefined();
  });

  it('opens a barracks on an owned production tile, and nothing on bare ground', () => {
    const s = session();
    expect(IDLE.click(context(s), { x: 2, y: 0 }).selection).toBeInstanceOf(RecruitSelection);
    expect(IDLE.click(context(s), { x: 5, y: 1 }).selection).toBe(IDLE);
  });

  it('refuses to take command during the enemy turn', () => {
    const s = session();
    const enemyTurn: SelectionContext = { ...context(s), isHumanTurn: false };
    expect(IDLE.click(enemyTurn, { x: 0, y: 0 }).selection).toBe(IDLE);
  });
});

describe('composing an order', () => {
  it('proposes a move when the destination offers a choice', () => {
    const s = session();
    const unit = new UnitSelection(s.state.units[0].id);
    // (2,0) is next to the enemy, so there is an attack to weigh against waiting.
    const outcome = unit.click(context(s), { x: 2, y: 0 });

    expect(outcome.selection).toBeInstanceOf(DestinationSelection);
    expect((outcome.selection as DestinationSelection).dest).toEqual({ x: 2, y: 0 });
    expect(outcome.action).toBeUndefined();
  });

  it('skips the menu when waiting is the only thing to do there', () => {
    const s = session();
    const unit = new UnitSelection(s.state.units[0].id);
    const outcome = unit.click(context(s), { x: 1, y: 1 });

    expect(outcome.action).toMatchObject({ kind: 'command', command: { ability: 'wait' } });
    expect(outcome.selection).toBe(IDLE);
  });

  it('arms an attack from the best firing position when an enemy is clicked', () => {
    const s = session();
    const unit = new UnitSelection(s.state.units[0].id);
    const outcome = unit.click(context(s), { x: 3, y: 0 });

    const armed = outcome.selection as TargetSelection;
    expect(armed).toBeInstanceOf(TargetSelection);
    expect(armed.ability).toBe('attack');
    // Chosen, not walked to: the firing position is adjacent to the target.
    expect(Math.abs(armed.dest.x - 3) + Math.abs(armed.dest.y - 0)).toBe(1);
    expect(armed.targets).toContainEqual({ x: 3, y: 0 });
  });

  it('submits the order when a target is clicked, and abandons it otherwise', () => {
    const s = session();
    const id = s.state.units[0].id;
    const aiming = new TargetSelection(id, { x: 2, y: 0 }, [{ x: 0, y: 0 }, { x: 2, y: 0 }], 'attack', undefined, [{ x: 3, y: 0 }]);

    expect(aiming.click(context(s), { x: 3, y: 0 }).action).toMatchObject({
      kind: 'command',
      unit: id,
      command: { ability: 'attack', target: { x: 3, y: 0 } },
    });
    expect(aiming.click(context(s), { x: 5, y: 1 }).action).toBeUndefined();
    expect(aiming.click(context(s), { x: 5, y: 1 }).selection).toBe(IDLE);
  });

  it('walks back one step at a time', () => {
    const id = 7;
    const path = [{ x: 0, y: 0 }, { x: 1, y: 0 }];
    const aiming = new TargetSelection(id, { x: 1, y: 0 }, path, 'attack', undefined, []);
    const menu = aiming.back();
    expect(menu).toBeInstanceOf(DestinationSelection);
    expect(menu.back()).toBeInstanceOf(UnitSelection);
    expect(menu.back().back()).toBe(IDLE);
    expect(IDLE.back()).toBe(IDLE);
  });
});

describe('what each selection puts on the board', () => {
  it('shows reach, threat and held ground for a unit under command', () => {
    const s = session();
    const overlay = emptyOverlay();
    new UnitSelection(s.state.units[0].id).paint(context(s, { x: 1, y: 0 }), overlay);

    expect(overlay.move.size).toBeGreaterThan(0);
    expect(overlay.attack.size).toBeGreaterThan(0);
    expect(overlay.path.at(-1)).toEqual({ x: 1, y: 0 });
  });

  it('shows a tactic\'s reach even though it has no unit of its own', () => {
    const s = session();
    const overlay = emptyOverlay();
    const tactic = new TacticTargetSelection('cmd', 'rally', [{ x: 4, y: 1 }]);

    expect(tactic.unitId).toBeNull();
    tactic.paint(context(s), overlay);
    expect(overlay.heal.size).toBe(1);
  });

  it('adds nothing for a selection that means nothing', () => {
    const s = session();
    const overlay = emptyOverlay();
    IDLE.paint(context(s), overlay);
    new RecruitSelection({ x: 2, y: 0 }).paint(context(s), overlay);
    expect(overlay).toEqual(emptyOverlay());
  });
});
