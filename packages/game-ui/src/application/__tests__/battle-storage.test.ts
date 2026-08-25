// @vitest-environment jsdom
import { beforeEach, describe, expect, it } from 'vitest';
import { StoredDocumentError } from '@empire/battle-engine';
import { browserBattleSaves } from '../battle-storage';

describe('browser battle storage', () => {
  beforeEach(() => localStorage.clear());

  it('distinguishes an empty slot from an unreadable one', () => {
    const slot = browserBattleSaves('test-level');
    expect(slot.read()).toBeNull();

    localStorage.setItem('empire:battle:test-level', '{not json');
    expect(() => slot.read()).toThrow(StoredDocumentError);
    expect(() => slot.read()).toThrow(/无法解析/);
  });
});
