import { Registry } from '../registry';
import type { StatusDef } from '../types';

/** Pure status-definition registry. Runtime lifecycle behavior lives in statuses.ts. */
export const Statuses = new Registry<StatusDef>('status');
