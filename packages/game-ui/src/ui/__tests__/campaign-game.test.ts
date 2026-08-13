// @vitest-environment jsdom
import { beforeEach, describe, expect, it } from 'vitest';
import { CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN } from '@empire/story-candidate-01';
import { candidate01CampaignAdapter } from '@empire/story-candidate-01/presentation';
import { StoryCampaignController } from '../campaign-game';

import { cloneContentCatalog, createBattleEngine, GlobalContentCatalog } from '@empire/battle-engine';

/** Composed per suite, exactly like an application composition root. */
const TEST_CATALOG = cloneContentCatalog(GlobalContentCatalog);
const TEST_ENGINE = createBattleEngine({ content: TEST_CATALOG });

function click(root: HTMLElement, selector: string): void {
  const element = root.querySelector<HTMLElement>(selector);
  if (!element) throw new Error(`missing element: ${selector}`);
  element.click();
}

describe('candidate-01 campaign UI', () => {
  beforeEach(() => localStorage.clear());

  it('moves from novel presentation to a persistent choice and battle staging', () => {
    const controller = new StoryCampaignController(candidate01CampaignAdapter(), null, () => {}, TEST_ENGINE);
    expect(controller.root.textContent).toContain('一双没有补好的靴子');
    for (let index = 0; index < 4; index++) click(controller.root, '[data-campaign-act="nextBeat"]');
    expect(controller.root.textContent).toContain('双子丘陵');
    for (let index = 0; index < 3; index++) click(controller.root, '[data-campaign-act="nextBeat"]');
    expect(controller.root.textContent).toContain('莱娅如何开始第一次指挥');
    click(controller.root, '[data-choice="steady-advance"]');
    expect(controller.root.textContent).toContain('作战准备');
    expect(localStorage.length).toBe(1);
    const saved = JSON.parse(localStorage.getItem(localStorage.key(0)!)!);
    expect(saved.campaign.id).toBe(CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN.id);
    expect(saved.state.flags).toContain('twin_hills_steady');
    controller.dispose();
  });

  it('enters the real battle controller and can return to campaign staging', () => {
    const controller = new StoryCampaignController(candidate01CampaignAdapter(), null, () => {}, TEST_ENGINE);
    for (let index = 0; index < 4; index++) click(controller.root, '[data-campaign-act="nextBeat"]');
    for (let index = 0; index < 3; index++) click(controller.root, '[data-campaign-act="nextBeat"]');
    click(controller.root, '[data-choice="steady-advance"]');
    click(controller.root, '[data-campaign-act="battle"]');
    expect(controller.root.querySelector('.game-root')).not.toBeNull();
    expect(controller.root.textContent).toContain('01 · 双子丘陵');
    click(controller.root, '[data-act="exit"]');
    expect(controller.root.querySelector('.staging-screen')).not.toBeNull();
    expect(controller.root.textContent).toContain('重新进入战斗');
    controller.dispose();
  });
});
