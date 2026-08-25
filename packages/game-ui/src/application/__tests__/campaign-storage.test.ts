// @vitest-environment jsdom
import { beforeEach, describe, expect, it } from 'vitest';
import {
  CampaignActionError,
  CampaignInvariantError,
  CampaignRuntime,
  createCampaignSave,
  type CampaignDefinition,
} from '@empire/campaign-engine';
import { loadCampaignState, saveCampaignState } from '../campaign-storage';

const definition = (): CampaignDefinition => ({
  schema: 1,
  id: 'storage-test',
  version: 1,
  start: 'ending',
  contentPacks: { 'test.content': 1 },
  nodes: [{ id: 'ending', type: 'ending', outcome: 'completed', presentation: 'ending' }],
});

const key = 'empire:campaign:storage-test:v1';

describe('campaign browser storage boundary', () => {
  beforeEach(() => localStorage.clear());

  it('distinguishes no save, an unreadable document and a valid campaign', () => {
    expect(loadCampaignState(definition()))
      .toEqual({ state: null, rejected: null });

    localStorage.setItem(key, '{broken json');
    expect(loadCampaignState(definition())).toMatchObject({
      state: null,
      rejected: expect.any(String),
    });

    const campaign = definition();
    const state = new CampaignRuntime(campaign).snapshot();
    localStorage.setItem(key, JSON.stringify(createCampaignSave(campaign, state)));
    expect(loadCampaignState(campaign)).toMatchObject({
      state: { currentNode: 'ending' },
      rejected: null,
    });
  });

  it('reports document rejection but propagates a definition defect', () => {
    const campaign = definition();
    const save = createCampaignSave(campaign, new CampaignRuntime(campaign).snapshot());
    localStorage.setItem(key, JSON.stringify({ ...save, schema: 0 }));

    expect(loadCampaignState(campaign)).toMatchObject({
      state: null,
      rejected: expect.stringContaining('schema'),
    });

    localStorage.setItem(key, JSON.stringify(save));
    expect(() => loadCampaignState({ ...campaign, start: 'missing' }))
      .toThrow(CampaignInvariantError);
  });

  it('refuses to disguise a pending battle as a successful campaign save', () => {
    const campaign = definition();
    const state = new CampaignRuntime(campaign).snapshot();
    state.pendingBattle = { requestId: 'pending', node: 'ending', level: 'level' };

    expect(() => saveCampaignState(campaign, state)).toThrow(CampaignActionError);
  });
});
