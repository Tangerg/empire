import {
  readCurrentDocument,
  StoredDocumentError,
} from '@empire/battle-engine';
import { validateCampaignDefinition, validateCampaignState } from './aggregate';
import { CampaignInvariantError } from './errors';
import type { CampaignDefinition, CampaignState } from './types';

export const CAMPAIGN_SAVE_SCHEMA = 1;

export interface CampaignSave {
  schema: 1;
  campaign: { id: string; version: number };
  contentPacks: Record<string, number>;
  savedAt: string;
  state: CampaignState;
}

export function createCampaignSave(
  definition: CampaignDefinition,
  state: CampaignState,
  savedAt = new Date().toISOString(),
): CampaignSave {
  validateCampaignDefinition(definition);
  if (!Number.isFinite(Date.parse(savedAt))) {
    throw new CampaignInvariantError('cannot save campaign with an invalid timestamp');
  }
  if (state.definitionId !== definition.id || state.definitionVersion !== definition.version) {
    throw new CampaignInvariantError('cannot save state for a different campaign definition');
  }
  validateCampaignState(definition, state);
  return {
    schema: CAMPAIGN_SAVE_SCHEMA,
    campaign: { id: definition.id, version: definition.version },
    contentPacks: { ...definition.contentPacks },
    savedAt,
    state: structuredClone(state),
  };
}

/** Reads one current-schema campaign save against its owning definition. */
export function loadCampaignSave(raw: unknown, definition: CampaignDefinition): CampaignSave {
  const save = readCurrentDocument<CampaignSave>('campaign save', CAMPAIGN_SAVE_SCHEMA, raw);
  validateCampaignDefinition(definition);
  requireCampaignSaveShape(save);
  if (save.campaign.id !== definition.id || save.campaign.version !== definition.version) {
    reject('definition identity/version mismatch');
  }
  const packIds = new Set([
    ...Object.keys(definition.contentPacks),
    ...Object.keys(save.contentPacks),
  ]);
  for (const id of packIds) {
    if (save.contentPacks[id] !== definition.contentPacks[id]) {
      reject(`content pack mismatch: "${id}"`);
    }
  }
  if (save.state.definitionId !== definition.id ||
    save.state.definitionVersion !== definition.version) {
    reject('state identity/version mismatch');
  }
  try {
    validateCampaignState(definition, save.state);
  } catch (error) {
    if (error instanceof CampaignInvariantError) {
      throw new StoredDocumentError(`campaign save state is invalid: ${error.message}`, { cause: error });
    }
    throw error;
  }
  return save;
}

type ShapeCheck = (value: unknown) => boolean;
type Shape<T> = Record<keyof T, ShapeCheck>;

const anArray: ShapeCheck = Array.isArray;
const anObject: ShapeCheck = (value) =>
  typeof value === 'object' && value !== null && !Array.isArray(value);
const aString: ShapeCheck = (value) => typeof value === 'string';
const aNumber: ShapeCheck = (value) => typeof value === 'number' && Number.isFinite(value);
const anInteger: ShapeCheck = (value) => Number.isInteger(value);
const nullable = (check: ShapeCheck): ShapeCheck => (value) => value === null || check(value);

const CAMPAIGN_SHAPE: Shape<CampaignSave['campaign']> = {
  id: aString,
  version: anInteger,
};

const SAVE_SHAPE: Shape<CampaignSave> = {
  schema: anInteger,
  campaign: anObject,
  contentPacks: anObject,
  savedAt: aString,
  state: anObject,
};

const STATE_SHAPE: Shape<CampaignState> = {
  definitionId: aString,
  definitionVersion: anInteger,
  currentNode: aString,
  status: aString,
  flags: anArray,
  variables: anObject,
  resources: anObject,
  relations: anObject,
  features: anArray,
  roster: anObject,
  completedNodes: anArray,
  battleHistory: anArray,
  pendingBattle: nullable(anObject),
  battleSequence: anInteger,
};

function reject(message: string): never {
  throw new StoredDocumentError(`campaign save ${message}`);
}

function requireShape<T>(value: unknown, shape: Shape<T>, owner: string): void {
  if (!anObject(value)) reject(`${owner} must be an object`);
  const fields = value as Record<string, unknown>;
  for (const [field, check] of Object.entries(shape) as [string, ShapeCheck][]) {
    if (!check(fields[field])) reject(`${owner}.${field} is missing or invalid`);
  }
}

function requireStringArray(value: readonly unknown[], owner: string): void {
  if (value.some((entry) => typeof entry !== 'string')) reject(`${owner} must contain strings`);
}

function requireFiniteRecord(value: Record<string, number>, owner: string): void {
  const invalid = Object.entries(value).find(([key, entry]) => !key.trim() || !Number.isFinite(entry));
  if (invalid) reject(`${owner}.${invalid[0]} is invalid`);
}

/** Raw shape fence: semantic validation may only run after every field it walks exists. */
function requireCampaignSaveShape(save: CampaignSave): void {
  requireShape(save, SAVE_SHAPE, 'save');
  requireShape(save.campaign, CAMPAIGN_SHAPE, 'campaign');
  if (!anObject(save.contentPacks)) reject('contentPacks must be an object');
  if (!aString(save.savedAt) || Number.isNaN(Date.parse(save.savedAt))) reject('savedAt is invalid');
  requireShape(save.state, STATE_SHAPE, 'state');

  const invalidPack = Object.entries(save.contentPacks)
    .find(([id, version]) => !id.trim() || !Number.isInteger(version) || version < 1);
  if (invalidPack) reject(`contentPacks.${invalidPack[0]} is invalid`);

  const state = save.state;
  requireStringArray(state.flags, 'state.flags');
  requireStringArray(state.features, 'state.features');
  requireStringArray(state.completedNodes, 'state.completedNodes');
  requireFiniteRecord(state.resources, 'state.resources');
  requireFiniteRecord(state.relations, 'state.relations');
  for (const [key, value] of Object.entries(state.variables)) {
    if (!key.trim() || !['string', 'number', 'boolean'].includes(typeof value) ||
      (typeof value === 'number' && !Number.isFinite(value))) {
      reject(`state.variables.${key} is invalid`);
    }
  }
  for (const [id, unit] of Object.entries(state.roster)) {
    if (!anObject(unit) || typeof unit.id !== 'string' || !aNumber(unit.hpRatio) || !aNumber(unit.moraleRatio)) {
      reject(`state.roster.${id} is invalid`);
    }
  }
  for (const [index, record] of state.battleHistory.entries()) {
    if (!anObject(record) || !aString(record.requestId) || !aString(record.node) ||
      !aString(record.level) || !aString(record.outcome) || !anInteger(record.turns) ||
      !anArray(record.signals)) {
      reject(`state.battleHistory.${index} is invalid`);
    }
    requireStringArray(record.signals, `state.battleHistory.${index}.signals`);
  }
  if (state.pendingBattle && (!aString(state.pendingBattle.requestId) ||
    !aString(state.pendingBattle.node) || !aString(state.pendingBattle.level))) {
    reject('state.pendingBattle is invalid');
  }
}
