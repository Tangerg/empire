import { describe, expect, it } from 'vitest';
import { CANDIDATE_01_ENVIRONMENT, candidate01EnvironmentScene } from './candidate-01-environment';
import { candidate01Asset } from './candidate-01-assets';
import { EnvironmentCatalog, type EnvironmentManifest } from '@empire/game-ui';
import { fixedNumbers, oneOf } from './candidate-01-documents';

/**
 * What the pack's three generated documents used to promise on their own word.
 *
 * They were imported as `json as unknown as TheShapeIWanted`, so nothing compared
 * the file to the type. Two things a JSON import cannot state are checked on the
 * way in now, and these are the checks: a closed set of names, and an array that
 * is meant to be a fixed number of numbers long.
 */
describe('a generated document is read, not asserted', () => {
  it('refuses a name outside the set the field may hold', () => {
    expect(oneOf(['a', 'b'], 'b', 'kind', 'subject')).toBe('b');
    expect(() => oneOf(['a', 'b'], 'c', 'kind', 'subject'))
      .toThrow('candidate-01: "subject" has unknown kind "c"');
  });

  it('refuses a coordinate that is the wrong length or not a number', () => {
    expect(fixedNumbers(2, [3, 4], 'anchor', 'subject')).toEqual([3, 4]);
    expect(() => fixedNumbers(2, [3], 'anchor', 'subject'))
      .toThrow('has a anchor of 1 numbers, expected 2');
    expect(() => fixedNumbers(2, [3, Number.NaN], 'anchor', 'subject')).toThrow('expected 2');
  });

  /** The shipped documents pass their own checks — the import above ran them. */
  it('reads every shipped document at import', () => {
    const scene = candidate01EnvironmentScene('c01-01');
    expect(scene?.mapSize).toHaveLength(2);
    expect(scene?.placements.length).toBeGreaterThan(20);
    for (const placement of scene?.placements ?? []) {
      expect(['foundation', 'ground-decal', 'under-units', 'over-units']).toContain(placement.layer);
    }
    for (const zone of scene?.zones ?? []) expect(zone.bounds).toHaveLength(4);
    expect(candidate01Asset('C01-MISSION-BORDER-FARMER').category).toBe('mission-unit');
  });

  /**
   * The check the environment manifest's own type cannot make.
   *
   * `anchor` is `readonly number[]` in the document and two numbers in fact, and
   * the catalog is where the difference is settled — a generator that dropped a
   * coordinate used to reach the renderer and draw the cell at `NaN`.
   */
  it('refuses an atlas whose anchor is not a point', () => {
    const atlas = CANDIDATE_01_ENVIRONMENT.atlas('forest-temperate');
    expect(atlas.anchor).toHaveLength(2);

    const manifest = (anchor: readonly number[]): EnvironmentManifest => ({
      atlases: [{
        id: 'probe',
        category: 'surfaces',
        png: 'probe.png',
        columns: 1,
        rows: 1,
        cellWidth: 32,
        cellHeight: 32,
        componentCount: 1,
        anchor,
      }],
    });
    const resolve = () => 'probe.png';
    expect(() => new EnvironmentCatalog(manifest([16, 31]), resolve)).not.toThrow();
    expect(() => new EnvironmentCatalog(manifest([16]), resolve))
      .toThrow('Environment anchor of probe must be two finite numbers');
  });
});
