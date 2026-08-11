import { Registry } from '../registry';
import type { TacticDef, TacticId } from '../types';

export const Tactics = new Registry<TacticDef>('tactic');

export const tacticDef = (id: TacticId): TacticDef => Tactics.get(id);
