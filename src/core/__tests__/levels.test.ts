import { describe, expect, it } from 'vitest';
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { chooseAction } from '../ai';
import { applyAction } from '../actions';
import { mapFromLevel, normaliseLevel, terrainRows, validateLevel } from '../mapio';
import { createState } from '../state';
import type { LevelData } from '../types';

const dir = join(import.meta.dirname, '../../content/ancient-empires/levels');
const files = readdirSync(dir).filter((f) => f.endsWith('.json'));

const levels: { file: string; level: LevelData }[] = files.map((file) => ({
  file,
  level: normaliseLevel(JSON.parse(readFileSync(join(dir, file), 'utf8'))),
}));

describe('built-in levels', () => {
  it('ships at least three levels', () => {
    expect(levels.length).toBeGreaterThanOrEqual(3);
  });

  for (const { file, level } of levels) {
    describe(file, () => {
      it('passes validation with no errors', () => {
        const issues = validateLevel(level);
        const errors = issues.filter((i) => i.severity === 'error');
        expect(errors, errors.map((e) => e.message).join('\n')).toEqual([]);
      });

      it('has rows matching the declared size', () => {
        expect(level.terrain.length).toBe(level.height);
        for (const row of level.terrain) expect(row.length).toBe(level.width);
      });

      it('round-trips through the terrain serialiser', () => {
        expect(terrainRows(mapFromLevel(level))).toEqual(level.terrain);
      });

      it('is playable: 12 AI-driven turns without an illegal action', () => {
        const s = createState(level);
        for (const p of s.players) p.controller = 'ai';
        for (let turn = 0; turn < 12 && s.phase === 'playing'; turn++) {
          for (let guard = 0; guard < 300; guard++) {
            const action = chooseAction(s);
            applyAction(s, action);
            if (action.kind === 'endTurn' || s.phase !== 'playing') break;
          }
        }
        expect(s.units.length).toBeGreaterThan(0);
      });
    });
  }
});
