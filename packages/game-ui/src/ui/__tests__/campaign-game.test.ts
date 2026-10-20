// @vitest-environment jsdom
import { readFileSync } from 'node:fs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { CampaignRuntime } from '@empire/campaign-engine';
import {
  CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN,
  CANDIDATE_01_CONTENT_PACK,
} from '@empire/story-candidate-01';
import {
  candidate01CampaignAdapter,
  CANDIDATE_01_ART,
} from '@empire/story-candidate-01/presentation';
import { StoryCampaignController, type StoryCampaignAdapter } from '../campaign-game';

import { createBattleEngine } from '@empire/battle-engine';
import { createTestCatalog } from '@empire/test-content';

/** Composed per suite, exactly like an application composition root. */
const TEST_CATALOG = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const TEST_ENGINE = createBattleEngine({ content: TEST_CATALOG });
const TEST_OPTIONS = { engine: TEST_ENGINE, art: CANDIDATE_01_ART };

function click(root: HTMLElement, selector: string): void {
  const element = root.querySelector<HTMLElement>(selector);
  if (!element) throw new Error(`missing element: ${selector}`);
  element.click();
}

describe('candidate-01 campaign UI', () => {
  beforeEach(() => localStorage.clear());

  it('moves from novel presentation to a persistent choice and battle staging', () => {
    const controller = new StoryCampaignController(candidate01CampaignAdapter(), null, () => {}, TEST_OPTIONS);
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

  it.each([true, false])('resolves only explicit roster actor bindings (bound=%s)', (bound) => {
    const adapter = candidate01CampaignAdapter();
    adapter.definition = structuredClone(adapter.definition);
    adapter.definition.initial!.roster!.push({ id: 'unbound-soldier', unitType: 'c01.swordsman', owner: 1 });
    if (!bound) for (const node of adapter.definition.nodes) {
      if (node.type === 'battle' && node.rosterBindings) node.rosterBindings = node.rosterBindings.filter((binding) => binding.campaignUnit !== 'torren');
    }
    const definitionBefore = JSON.stringify(adapter.definition);
    const controller = new StoryCampaignController(adapter, null, () => {}, TEST_OPTIONS);
    for (let index = 0; index < 7; index++) click(controller.root, '[data-campaign-act="nextBeat"]');
    click(controller.root, '[data-choice="steady-advance"]');
    const rows = controller.root.querySelectorAll('.campaign-unit');
    expect(rows[1].innerHTML.includes('char-torren-01')).toBe(bound);
    expect(rows[2].innerHTML).not.toContain('char-torren-01');
    expect(rows[rows.length - 1].innerHTML).toContain('unit-swordsman@2x.png');
    expect(rows[rows.length - 1].innerHTML).not.toContain('char-torren-01');
    expect(JSON.stringify(adapter.definition)).toBe(definitionBefore);
    const saved = JSON.parse(localStorage.getItem(localStorage.key(0)!)!);
    expect(saved.state.roster.torren.unitType).toBe('c01.swordsman');
    expect(saved.state.roster['unbound-soldier'].unitType).toBe('c01.swordsman');
    controller.dispose();
  });

  it('enters the real battle controller and can return to campaign staging', () => {
    const controller = new StoryCampaignController(candidate01CampaignAdapter(), null, () => {}, TEST_OPTIONS);
    for (let index = 0; index < 4; index++) click(controller.root, '[data-campaign-act="nextBeat"]');
    for (let index = 0; index < 3; index++) click(controller.root, '[data-campaign-act="nextBeat"]');
    click(controller.root, '[data-choice="steady-advance"]');
    expect(controller.root.textContent).toContain('补给');
    expect(controller.root.textContent).toContain('国库');
    click(controller.root, '[data-campaign-act="battle"]');
    expect(controller.root.querySelector('.game-root')).not.toBeNull();
    expect(controller.root.querySelector('.layer-units image')).not.toBeNull();
    expect(controller.root.textContent).toContain('01 · 双子丘陵');
    click(controller.root, '[data-act="exit"]');
    expect(controller.root.querySelector('.staging-screen')).not.toBeNull();
    expect(controller.root.textContent).toContain('重新进入战斗');
    controller.dispose();
  });
});


describe('candidate-01 explicit scene availability', () => {
  const controllers: StoryCampaignController[] = [];
  let style: HTMLStyleElement;

  beforeEach(() => {
    localStorage.clear();
    style = document.createElement('style');
    style.textContent = readFileSync('packages/game-ui/src/styles/campaign.css', 'utf8');
    document.head.append(style);
  });

  afterEach(() => {
    for (const controller of controllers.splice(0)) {
      controller.dispose();
      controller.root.remove();
    }
    style.remove();
  });

  function atNode(nodeId: string, adapter = candidate01CampaignAdapter()): StoryCampaignController {
    expect(adapter.definition.nodes.some(node => node.id === nodeId)).toBe(true);
    const state = new CampaignRuntime(adapter.definition).snapshot();
    state.currentNode = nodeId;
    const controller = new StoryCampaignController(adapter, state, () => {}, TEST_OPTIONS);
    controllers.push(controller);
    document.body.append(controller.root);
    return controller;
  }

  function expectBackdrop(root: HTMLElement, url: string): void {
    const backdrops = root.querySelectorAll('.campaign-backdrop img');
    expect(backdrops).toHaveLength(2);
    for (const image of backdrops) expect(image.getAttribute('src')).toBe(url);
    for (const image of root.querySelectorAll('img')) {
      expect(image.getAttribute('src')).toBeTruthy();
      expect(image.getAttribute('src')).not.toMatch(/^(null|undefined)$/);
    }
  }

  function expectTextOnlyStaging(controller: StoryCampaignController, adapter: StoryCampaignAdapter, levelId: string): void {
    const level = adapter.level(levelId);
    const hero = controller.root.querySelector<HTMLElement>('.staging-hero--text-only');
    expect(hero).not.toBeNull();
    expect(controller.root.querySelector('.staging-hero img')).toBeNull();
    expect(hero!.querySelector('h1')!.textContent).toBe(level.name);
    expect(getComputedStyle(hero!).gridTemplateColumns).toBe('minmax(0, 1fr)');
    expect(controller.root.querySelector('[data-campaign-act="battle"]')).not.toBeNull();
    expectBackdrop(controller.root, adapter.chapterArt(adapter.chapterOf(level)));
  }

  it.each(['brief-04', 'aftermath-04', 'brief-10', 'brief-13'])
    ('keeps %s prose and chapter backdrop without resolving a missing scene', (nodeId) => {
      const adapter = candidate01CampaignAdapter();
      const resolve = vi.spyOn(adapter, 'storyArt');
      const story = adapter.story(`c01/${nodeId}`);
      const controller = atNode(nodeId, adapter);
      expect(controller.root.querySelector('h1')!.textContent).toBe(story.title);
      expect(controller.root.textContent).toContain(story.location);
      expect(controller.root.textContent).toContain(story.summary);
      expect(controller.root.textContent).toContain(story.beats[0].text);
      if (story.mission) expect(controller.root.textContent).toContain(story.mission.objective);
      expectBackdrop(controller.root, adapter.chapterArt(story.chapter));
      expect(controller.root.innerHTML).not.toContain('ch05-s26');
      expect(resolve).not.toHaveBeenCalled();
    });

  it.each(['04', '10', '13'])('stages battle-%s as an intentional one-column hero', (suffix) => {
    const adapter = candidate01CampaignAdapter();
    const resolve = vi.spyOn(adapter, 'storyArt');
    const controller = atNode(`battle-${suffix}`, adapter);
    expectTextOnlyStaging(controller, adapter, `c01-${suffix}`);
    expect(controller.root.innerHTML).not.toContain('ch05-s26');
    expect(resolve).not.toHaveBeenCalled();
  });

  it('retains the exact briefing image and separate chapter backdrop for a normal staging screen', () => {
    const adapter = candidate01CampaignAdapter();
    const controller = atNode('battle-01', adapter);
    const level = adapter.level('c01-01');
    const scene = adapter.story(adapter.briefingId(level)).scene;
    if (scene === null) throw new Error('battle-01 must retain a specific scene');
    expect(controller.root.querySelector('.staging-hero img')!.getAttribute('src')).toBe(adapter.storyArt(scene));
    expect(controller.root.querySelector('.staging-hero--text-only')).toBeNull();
    expectBackdrop(controller.root, adapter.chapterArt(adapter.chapterOf(level)));
  });

  it.each([
    ['aftermath-08', 'C01-SCENE-PUBLIC-RATIONS'],
    ['aftermath-10', 'C01-ARCH-GRAY-CAMP'],
    ['aftermath-13', 'C01-ARCH-FIELD-HOSPITAL'],
    ['chapter-three-ending', 'C01-CH05-S26'],
  ])('renders %s using the actual resolver URL for %s', (nodeId, topic) => {
    const adapter = candidate01CampaignAdapter();
    const controller = atNode(nodeId, adapter);
    const url = adapter.storyArt(topic);
    expectBackdrop(controller.root, url);
    if (nodeId === 'chapter-three-ending') expect(url).toContain('runtime-hd/narrative-static/ch05-s26');
  });

  it.each(['brief-04', 'battle-04'])('never hides invalid non-null selections on %s', (nodeId) => {
    for (const scene of ['unknown-scene', '']) {
      const adapter = candidate01CampaignAdapter();
      const story = adapter.story;
      adapter.story = id => ({ ...story(id), scene });
      expect(() => atNode(nodeId, adapter)).toThrow('unknown candidate-01 art topic');
    }
  });

  it.each(['04', '10', '13'])('preserves navigation, save and battle reentry after brief-%s', (suffix) => {
    const adapter = candidate01CampaignAdapter();
    const controller = atNode(`brief-${suffix}`, adapter);
    const story = adapter.story(`c01/brief-${suffix}`);
    for (const _beat of story.beats) click(controller.root, '[data-campaign-act="nextBeat"]');
    const choice = controller.root.querySelector<HTMLElement>('[data-campaign-act="choose"]');
    if (choice) choice.click();
    expectTextOnlyStaging(controller, adapter, `c01-${suffix}`);
    const savedBefore = JSON.parse(localStorage.getItem(localStorage.key(0)!)!);
    expect(savedBefore.state.currentNode).toBe(`battle-${suffix}`);
    expect(savedBefore.state.status).toBe('active');
    for (let entry = 0; entry < 2; entry++) {
      click(controller.root, '[data-campaign-act="battle"]');
      expect(controller.root.querySelector('.game-root')).not.toBeNull();
      expect(controller.root.querySelector('.layer-units image')).not.toBeNull();
      click(controller.root, '[data-act="exit"]');
      expectTextOnlyStaging(controller, adapter, `c01-${suffix}`);
      expect(controller.root.textContent).toContain('重新进入战斗');
    }
    const savedAfter = JSON.parse(localStorage.getItem(localStorage.key(0)!)!);
    expect(savedAfter.state).toEqual(savedBefore.state);
    controller.dispose();
    const restored = new StoryCampaignController(adapter, savedAfter.state, () => {}, TEST_OPTIONS);
    controllers.push(restored);
    document.body.append(restored.root);
    expectTextOnlyStaging(restored, adapter, `c01-${suffix}`);
  }, 30_000);

  it('advances the missing-art aftermath to the next authored briefing', () => {
    const adapter = candidate01CampaignAdapter();
    const controller = atNode('aftermath-04', adapter);
    for (const _beat of adapter.story('c01/aftermath-04').beats) click(controller.root, '[data-campaign-act="nextBeat"]');
    expect(controller.root.querySelector('h1')!.textContent).toBe(adapter.story('c01/brief-05').title);
    const saved = JSON.parse(localStorage.getItem(localStorage.key(0)!)!);
    expect(saved.state.currentNode).toBe('brief-05');
  });
});
