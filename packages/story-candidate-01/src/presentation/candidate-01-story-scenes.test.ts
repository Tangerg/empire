import { describe, expect, it } from 'vitest';
import { CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN } from '../campaign';
import { candidate01Story } from '../story';
import { candidate01CampaignAdapter } from './index';
import { candidate01Asset } from './candidate-01-assets';
import { candidate01StoryArt } from './candidate-01-story';

const presentations = CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN.nodes.flatMap(node =>
  node.type === 'story' || node.type === 'ending' ? [candidate01Story(node.presentation)] : []);

const missingScenes = ['c01/brief-04', 'c01/aftermath-04', 'c01/brief-10', 'c01/brief-13'];

describe('candidate-01 semantic story scene selection', () => {
  it.each([
    ['c01/brief-04', null],
    ['c01/aftermath-04', null],
    ['c01/aftermath-08', 'C01-SCENE-PUBLIC-RATIONS'],
    ['c01/brief-10', null],
    ['c01/aftermath-10', 'C01-ARCH-GRAY-CAMP'],
    ['c01/brief-13', null],
    ['c01/aftermath-13', 'C01-ARCH-FIELD-HOSPITAL'],
    ['c01/chapter-three-ending', 'C01-CH05-S26'],
  ] as const)('selects the audited setting for %s', (id, expected) => {
    expect(candidate01Story(id).scene).toBe(expected);
  });

  it('uses council only for the deliberate thematic chapter-three ending', () => {
    expect(presentations.filter(story => story.scene === 'C01-CH05-S26').map(story => story.id))
      .toEqual(['c01/chapter-three-ending']);
  });

  it('marks only the four documented presentations as missing specific art', () => {
    expect(presentations.filter(story => story.scene === null).map(story => story.id)).toEqual(missingScenes);
  });

  it('resolves every other graph presentation to a registered narrative scene', () => {
    expect(presentations).toHaveLength(35);
    for (const story of presentations) {
      if (story.scene === null) continue;
      const asset = candidate01Asset(story.scene);
      expect(asset.category).toBe('narrative-static');
      expect(asset.kind).toBe('scene');
      expect(candidate01StoryArt(story.scene)).toBe(asset.url);
    }
  });

  it('derives all sixteen staging heroes from the same authored briefings', () => {
    const adapter = candidate01CampaignAdapter();
    const battles = adapter.definition.nodes.filter(node => node.type === 'battle');
    expect(battles).toHaveLength(16);
    const missingLevels: string[] = [];
    for (const node of battles) {
      const level = adapter.level(node.level);
      const briefingId = adapter.briefingId(level);
      const scene = adapter.story(briefingId).scene;
      expect(scene).toBe(candidate01Story(briefingId).scene);
      expect(scene).not.toBe('C01-CH05-S26');
      if (scene === null) missingLevels.push(level.id);
      else expect(adapter.storyArt(scene)).toBe(candidate01Asset(scene).url);
    }
    expect(missingLevels).toEqual(['c01-04', 'c01-10', 'c01-13']);
  });

  it.each(['unknown-scene', ''])('rejects invalid non-null topic %j', (topic) => {
    expect(() => candidate01StoryArt(topic)).toThrow('unknown candidate-01 art topic');
  });
});
