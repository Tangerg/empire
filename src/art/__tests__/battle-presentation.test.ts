import { describe, expect, it } from 'vitest';
import { candidate01Level } from '../../content/candidate-01/levels';
import { mapFromLevel } from '../../core/mapio';
import { battlePresentation } from '../battle-presentation';
import { createSceneViewport } from '../scene-viewport';

describe('battle presentation', () => {
  it('resolves story art at the composition edge', () => {
    const presentation = battlePresentation('c01-01');
    const map = mapFromLevel(candidate01Level('c01-01'));
    const viewport = createSceneViewport(
      map.width,
      map.height,
      32,
      presentation.sceneProfile('c01-01'),
    );

    expect(presentation.id).toBe('candidate-01');
    expect(presentation.boardClass).toBe('candidate-map');
    expect(presentation.sceneFrame('c01-01', map, viewport).backdrop).toContain('authored-wide');
    expect(presentation.sceneLayers('c01-01', map).ground).toContain('candidate-ground-route');
  });

  it('keeps unknown campaigns on the story-neutral fallback', () => {
    const presentation = battlePresentation('sandbox-01');

    expect(presentation.id).toBe('generic');
    expect(presentation.sceneProfile('sandbox-01')).toEqual({});
    expect(presentation.sceneLayers('sandbox-01', mapFromLevel(candidate01Level('c01-01')))).toEqual({
      ground: '',
      underUnits: '',
      overUnits: '',
    });
    expect(presentation.effect('unknown', 16, 16)).toBe('');
  });
});
