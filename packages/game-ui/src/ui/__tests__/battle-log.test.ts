import { describe, expect, it } from 'vitest';
import { BattleLog } from '../battle-log';

/**
 * What the log is for: the last things worth telling the player, in order.
 *
 * The screen shows six lines of it, which is what makes each of these a policy
 * rather than a detail — a log that is complete and unreadable has failed at the
 * only thing it does.
 */
describe('the battle log', () => {
  it('says nothing about nothing', () => {
    const log = new BattleLog();
    log.add('');
    expect(log.recent()).toEqual([]);
  });

  /**
   * Turning four units to face the same way filled all six visible lines with
   * `剑士 调整了朝向`, and the four things that actually happened had scrolled off
   * the top. A run of one line is one line with a count.
   */
  it('collapses a run of the same line into one with a count', () => {
    const log = new BattleLog();
    log.add('剑士 调整了朝向');
    log.add('剑士 调整了朝向');
    log.add('剑士 调整了朝向');
    expect(log.recent()).toEqual(['剑士 调整了朝向 ×3']);
  });

  /** Consecutive only: the order things happened in is the point of a log. */
  it('keeps a line that comes back after something else', () => {
    const log = new BattleLog();
    log.add('弓箭手 阵亡');
    log.add('弓箭手 阵亡');
    log.add('骑士 阵亡');
    log.add('弓箭手 阵亡');
    expect(log.recent()).toEqual(['弓箭手 阵亡 ×2', '骑士 阵亡', '弓箭手 阵亡']);
  });

  /**
   * The bound counts what the screen shows.
   *
   * A run is one entry, so a thousand identical lines no longer push the rest of
   * the battle out of a sixty-deep log — which was the other half of the same
   * defect.
   */
  it('bounds what it keeps, counting collapsed runs as one', () => {
    const log = new BattleLog(3);
    for (const line of ['a', 'b', 'c', 'd']) log.add(line);
    expect(log.recent()).toEqual(['b', 'c', 'd']);

    const flooded = new BattleLog(3);
    flooded.add('第 1 回合');
    for (let repeat = 0; repeat < 200; repeat++) flooded.add('剑士 调整了朝向');
    expect(flooded.recent()).toEqual(['第 1 回合', '剑士 调整了朝向 ×200']);
  });

  it('forgets everything when the battle restarts', () => {
    const log = new BattleLog();
    log.add('第 1 回合');
    log.clear();
    expect(log.recent()).toEqual([]);
  });

  /** A copy, so the screen cannot write the log by editing what it read. */
  it('hands out a copy', () => {
    const log = new BattleLog();
    log.add('第 1 回合');
    log.recent().push('第 2 回合');
    expect(log.recent()).toEqual(['第 1 回合']);
  });
});
