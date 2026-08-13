import { Registry } from '../registry';
import type { MovementClass, MovementProfileDef } from '../types';

/**
 * Movement is content, not a closed engine enum. The initial fantasy pack uses
 * the first four profiles; naval and amphibious are shared primitives required
 * by both the stellar and historical campaign candidates.
 */
export const MovementProfiles = new Registry<MovementProfileDef>('movement profile');

export const movementProfile = (id: MovementClass): MovementProfileDef => MovementProfiles.get(id);

export const movementLabels = (): Record<string, string> =>
  Object.fromEntries(MovementProfiles.all().map((profile) => [profile.id, profile.name]));
