import { describe, expect, it } from 'vitest';
import { CampaignBattleBridge, CampaignRuntime, validateCampaignDefinition } from '../../campaign';
import { GameSession } from '../../core/session';
import {
  CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN,
  applyCandidate01BattleContext,
  applyCandidate01BattleResultPolicy,
} from './campaign';
import { candidate01Level } from './levels';
import { CANDIDATE_01_CHOICES, CANDIDATE_01_STORY } from './story';

describe('candidate-01 campaign', () => {
  it('binds every presentation and all sixteen battles into one valid graph', () => {
    expect(() => validateCampaignDefinition(CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN)).not.toThrow();
    const nodes = CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN.nodes;
    expect(nodes.filter((node) => node.type === 'battle')).toHaveLength(16);
    for (const node of nodes) {
      if (!node.presentation) continue;
      if (node.type === 'choice') expect(CANDIDATE_01_CHOICES.has(node.presentation)).toBe(true);
      else expect(CANDIDATE_01_STORY.has(node.presentation)).toBe(true);
    }
  });

  it('can deterministically advance from the prologue through chapter three', () => {
    const bridge = new CampaignBattleBridge(candidate01Level);
    const runtime = new CampaignRuntime(CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN);
    let battles = 0;
    let guard = 0;
    while (runtime.state.status === 'active') {
      expect(++guard).toBeLessThan(100);
      const node = runtime.node();
      if (node.type === 'choice') {
        runtime.choose(runtime.choices()[0].id);
      } else if (node.type === 'battle') {
        const request = applyCandidate01BattleContext(runtime.beginBattle(bridge), runtime.state);
        const session = new GameSession(request.level);
        runtime.completeBattle(bridge.result(request, session.state, [], 'victory'));
        battles++;
      } else {
        runtime.advance();
      }
    }
    expect(runtime.state.status).toBe('completed');
    expect(battles).toBe(16);
    expect(runtime.state.variables.victories).toBe(16);
    expect(runtime.state.flags).toEqual(expect.arrayContaining(['chapter_1_complete', 'chapter_2_complete', 'chapter_3_complete']));
    expect(runtime.state.battleHistory).toHaveLength(16);
  });

  it('projects pivotal choices into battle snapshots without story logic in the bridge', () => {
    const bridge = new CampaignBattleBridge(candidate01Level);
    const runtime = new CampaignRuntime(CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN);
    runtime.advance();
    runtime.advance();
    runtime.choose('rush-north-hill');
    const raw = runtime.beginBattle(bridge);
    const adapted = applyCandidate01BattleContext(raw, runtime.state);
    const torren = adapted.level.units.find((unit) => unit.key === 'campaign-torren');
    expect(torren).toMatchObject({ x: 4, y: 0, hp: 72 });
    expect(raw.level.units.find((unit) => unit.key === 'campaign-torren')).not.toMatchObject({ x: 4, y: 0 });
  });

  it('turns named-hero defeat into a persistent wound while retainers remain mortal', () => {
    const bridge = new CampaignBattleBridge(candidate01Level);
    const runtime = new CampaignRuntime(CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN);
    runtime.advance();
    runtime.advance();
    runtime.choose('steady-advance');
    const request = runtime.beginBattle(bridge);
    const session = new GameSession(request.level);
    const result = bridge.result(request, session.state, [], 'victory');
    const laiya = result.units.find((unit) => unit.campaignUnit === 'laiya')!;
    const torren = result.units.find((unit) => unit.campaignUnit === 'torren')!;
    Object.assign(laiya, { disposition: 'fallen', hpRatio: 0, moraleRatio: 0 });
    Object.assign(torren, { disposition: 'fallen', hpRatio: 0, moraleRatio: 0 });
    const projected = applyCandidate01BattleResultPolicy(result, runtime.state);
    expect(projected.units.find((unit) => unit.campaignUnit === 'laiya')).toMatchObject({ disposition: 'available', hpRatio: 0.3, moraleRatio: 0.4 });
    expect(projected.units.find((unit) => unit.campaignUnit === 'torren')?.disposition).toBe('fallen');
    expect(projected.signals).toContain('hero_wounded:laiya');
  });
});
