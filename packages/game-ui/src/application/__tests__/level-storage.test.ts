// @vitest-environment jsdom
import { beforeEach, describe, expect, it } from 'vitest';
import { normaliseLevel, type LevelData } from '@empire/battle-engine';
import { makeLevel, u } from '@empire/test-content';
import {
  CUSTOM_LEVELS_KEY,
  loadCustomLevels,
  readCustomLevels,
  deleteCustomLevel,
  saveCustomLevel,
} from '../level-storage';

const level = (id: string): LevelData =>
  makeLevel(['..'], { id, units: [u(0, 0, 'soldier', 1)] });

describe('custom level storage', () => {
  beforeEach(() => localStorage.clear());

  it('round-trips saved levels newest first', () => {
    saveCustomLevel(level('one'));
    saveCustomLevel(level('two'));
    expect(loadCustomLevels().map((stored) => stored.level.id)).toEqual(['two', 'one']);
  });

  it('drops only the unreadable entry, never the whole collection', () => {
    saveCustomLevel(level('good'));
    const raw = JSON.parse(localStorage.getItem(CUSTOM_LEVELS_KEY)!);
    raw.push({ savedAt: 1, level: { schema: 99, id: 'from-the-future' } });
    localStorage.setItem(CUSTOM_LEVELS_KEY, JSON.stringify(raw));

    const result = readCustomLevels();
    expect(result.levels.map((stored) => stored.level.id)).toEqual(['good']);
    expect(result.rejected).toEqual([
      { id: 'from-the-future', reason: expect.stringContaining('schema') },
    ]);
  });

  it('keeps an unreadable entry on disk for manual recovery', () => {
    localStorage.setItem(CUSTOM_LEVELS_KEY, JSON.stringify([
      { savedAt: 1, level: { schema: 99, id: 'from-the-future' } },
    ]));
    saveCustomLevel(level('new'));

    const stored = JSON.parse(localStorage.getItem(CUSTOM_LEVELS_KEY)!);
    expect(stored.map((entry: { level: { id: string } }) => entry.level.id))
      .toEqual(['new', 'from-the-future']);
  });

  it('survives a corrupted store instead of throwing', () => {
    localStorage.setItem(CUSTOM_LEVELS_KEY, '{not json');
    const result = readCustomLevels();
    expect(result.levels).toEqual([]);
    expect(result.rejected).toHaveLength(1);
  });

  /**
   * Reading a broken slot reports it; *writing* one has no safe move at all.
   *
   * The writer used to answer "there was nothing there" and then write, so one
   * corrupt byte plus one save erased every level the author had — underneath a
   * comment promising unreadable entries are kept verbatim.
   */
  it('refuses to overwrite a slot it could not read', () => {
    localStorage.setItem(CUSTOM_LEVELS_KEY, '{not json');

    expect(() => saveCustomLevel(level('new'))).toThrow(/无法解析/);
    expect(() => deleteCustomLevel('new')).toThrow(/无法解析/);
    expect(localStorage.getItem(CUSTOM_LEVELS_KEY)).toBe('{not json');
  });

  it('also refuses a valid JSON value with the wrong slot shape', () => {
    localStorage.setItem(CUSTOM_LEVELS_KEY, JSON.stringify({ levels: [] }));

    expect(readCustomLevels()).toEqual({
      levels: [],
      rejected: [{ id: '*', reason: '自定义关卡存储必须是数组' }],
    });
    expect(() => saveCustomLevel(level('new'))).toThrow(/必须是数组/);
    expect(localStorage.getItem(CUSTOM_LEVELS_KEY)).toBe('{"levels":[]}');
  });

  it('keeps an unreadable entry when another level is deleted', () => {
    localStorage.setItem(CUSTOM_LEVELS_KEY, JSON.stringify([
      { savedAt: 2, level: { schema: 99, id: 'from-the-future' } },
      { savedAt: 1, level: level('doomed') },
    ]));

    deleteCustomLevel('doomed');

    const stored = JSON.parse(localStorage.getItem(CUSTOM_LEVELS_KEY) ?? '[]');
    expect(stored.map((entry: { level: { id: string } }) => entry.level.id)).toEqual(['from-the-future']);
  });
});

describe('level schema boundary', () => {
  it('refuses any schema other than the current one', () => {
    expect(() => normaliseLevel({ schema: 99, terrain: ['..'] })).toThrow(/schema/);
  });

  it('leaves a current-schema level unchanged', () => {
    // A loaded document is owned by the normaliser, never by the caller.
    const current = level('unchanged');
    expect(normaliseLevel(current)).toEqual(current);
    expect(normaliseLevel(current)).not.toBe(current);
  });
});
