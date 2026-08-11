import { normaliseLevel } from '../../../core/mapio';
import type { LevelData } from '../../../core/types';
import twinHills from './01-twin-hills.json';
import threeBridges from './02-three-bridges.json';
import siege from './03-siege.json';
import cliffTraining from './04-cliff-training.json';

/** Campaign content only; persistence and editor workflows live in application adapters. */
export const ANCIENT_EMPIRES_LEVELS: LevelData[] = [twinHills, threeBridges, siege, cliffTraining].map((raw) =>
  normaliseLevel(raw),
);
