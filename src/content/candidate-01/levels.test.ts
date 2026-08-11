import { describe, expect, it } from 'vitest';
import { GameSession } from '../../core/session';
import { validateLevel } from '../../core/mapio';
import { CANDIDATE_01_LEVELS, CANDIDATE_01_ROSTER_BINDINGS } from './levels';
import { auditBattlefield } from './battlefield-audit';

describe('candidate-01 first three chapters', () => {
  it('ships sixteen ordered, structurally valid battles', () => {
    expect(CANDIDATE_01_LEVELS).toHaveLength(16);
    expect(new Set(CANDIDATE_01_LEVELS.map((level) => level.id)).size).toBe(16);
    const errors = CANDIDATE_01_LEVELS.flatMap((level) =>
      validateLevel(level)
        .filter((issue) => issue.severity === 'error')
        .map((issue) => `${level.id}: ${issue.message}`),
    );
    expect(errors).toEqual([]);
    for (const level of CANDIDATE_01_LEVELS) expect(() => new GameSession(level)).not.toThrow();
  });

  it('keeps every declared campaign binding attached to a stable level key', () => {
    for (const level of CANDIDATE_01_LEVELS) {
      const keys = new Set(level.units.map((unit) => unit.key));
      for (const binding of CANDIDATE_01_ROSTER_BINDINGS[level.id]) {
        expect(keys.has(binding.levelUnitKey)).toBe(true);
      }
    }
  });

  it('varies the tactical objective and map vocabulary across the campaign', () => {
    const objectiveKinds = new Set<string>();
    const collect = (objective: (typeof CANDIDATE_01_LEVELS)[number]['victory'][number]): void => {
      objectiveKinds.add(objective.type);
      if (objective.type === 'all' || objective.type === 'any' || objective.type === 'sequence') objective.objectives.forEach(collect);
      if (objective.type === 'optional' || objective.type === 'failOn') collect(objective.objective);
    };
    CANDIDATE_01_LEVELS.forEach((level) => level.victory.forEach(collect));
    const terrainSymbols = new Set(CANDIDATE_01_LEVELS.flatMap((level) => level.terrain.flatMap((row) => [...row])));
    expect(objectiveKinds.size).toBeGreaterThanOrEqual(8);
    expect([...terrainSymbols]).toEqual(expect.arrayContaining(['s', 'g', 'f', 'R', 'o']));
  });

  it('maintains production-scale battlefields instead of compact demo formations', () => {
    const audits = CANDIDATE_01_LEVELS.map(auditBattlefield);
    expect(audits.every((audit) => audit.width >= 20 && audit.height >= 13 && audit.cells >= 260)).toBe(true);
    expect(audits.every((audit) => audit.playerUnits >= 6 && audit.enemyUnits >= 7)).toBe(true);
    expect(audits.every((audit) => audit.playerSpan.width >= 4 && audit.playerSpan.height >= 5)).toBe(true);
    expect(audits.every((audit) => audit.enemySpan.height >= 5 && audit.enemySpan.area >= 40)).toBe(true);
    expect(audits.every((audit) => audit.occupiedSectors >= 4)).toBe(true);
    expect(audits.filter((audit) => audit.closestContact < 2 || audit.closestContact > 12).map((audit) => ({ id: audit.id, closestContact: audit.closestContact }))).toEqual([]);
    expect(audits.filter((audit) => audit.reinforcements > 0).length).toBeGreaterThanOrEqual(4);
    expect(new Set(audits.map((audit) => `${audit.width}x${audit.height}`)).size).toBeGreaterThanOrEqual(10);
    expect(CANDIDATE_01_LEVELS.every((level) => Number(level.extra?.fronts) >= 2 && typeof level.extra?.battleScale === 'string')).toBe(true);
  });

  it('treats story witnesses and captives as protected allies, not hidden rout targets', () => {
    for (const id of ['c01-09', 'c01-15']) {
      const level = CANDIDATE_01_LEVELS.find((entry) => entry.id === id)!;
      expect(level.players.find((player) => player.id === 3)?.team).toBe(1);
    }
    const silverwood = CANDIDATE_01_LEVELS.find((entry) => entry.id === 'c01-15')!;
    const serialized = JSON.stringify(silverwood.players.find((player) => player.id === 1)?.objectives);
    expect(serialized).toContain('"owner":2');
  });
});
