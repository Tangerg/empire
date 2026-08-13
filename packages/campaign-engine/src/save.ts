import { validateCampaignDefinition, validateCampaignState } from './aggregate';
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
  if (state.definitionId !== definition.id || state.definitionVersion !== definition.version) {
    throw new Error('cannot save state for a different campaign definition');
  }
  return {
    schema: CAMPAIGN_SAVE_SCHEMA,
    campaign: { id: definition.id, version: definition.version },
    contentPacks: { ...definition.contentPacks },
    savedAt,
    state: structuredClone(state),
  };
}

type SaveMigration = (raw: Record<string, unknown>) => Record<string, unknown>;

/** Explicit, sequential schema migration; no permissive best-effort loading. */
export class CampaignSaveMigrator {
  private readonly migrations = new Map<number, SaveMigration>();

  register(fromSchema: number, migrate: SaveMigration): this {
    if (!Number.isInteger(fromSchema) || fromSchema < 0) throw new Error('migration schema must be non-negative');
    if (this.migrations.has(fromSchema)) throw new Error(`save migration ${fromSchema} already registered`);
    this.migrations.set(fromSchema, migrate);
    return this;
  }

  load(raw: unknown, definition: CampaignDefinition): CampaignSave {
    if (!raw || typeof raw !== 'object' || Array.isArray(raw)) throw new Error('campaign save must be an object');
    let value = structuredClone(raw) as Record<string, unknown>;
    let schema = Number(value.schema);
    if (!Number.isInteger(schema) || schema < 0) throw new Error('campaign save has invalid schema');
    while (schema < CAMPAIGN_SAVE_SCHEMA) {
      const migrate = this.migrations.get(schema);
      if (!migrate) throw new Error(`no campaign save migration from schema ${schema}`);
      value = migrate(value);
      const next = Number(value.schema);
      if (!Number.isInteger(next) || next <= schema) throw new Error(`campaign save migration ${schema} did not advance schema`);
      schema = next;
    }
    if (schema !== CAMPAIGN_SAVE_SCHEMA) throw new Error(`unsupported campaign save schema ${schema}`);
    const save = value as unknown as CampaignSave;
    this.validate(save, definition);
    return structuredClone(save);
  }

  private validate(save: CampaignSave, definition: CampaignDefinition): void {
    validateCampaignDefinition(definition);
    if (!save.campaign || save.campaign.id !== definition.id || save.campaign.version !== definition.version) {
      throw new Error('campaign save definition identity/version mismatch');
    }
    for (const [id, version] of Object.entries(definition.contentPacks)) {
      if (save.contentPacks?.[id] !== version) throw new Error(`campaign save content pack mismatch: "${id}"`);
    }
    if (!save.state || save.state.definitionId !== definition.id ||
      save.state.definitionVersion !== definition.version) {
      throw new Error('campaign save state identity/version mismatch');
    }
    validateCampaignState(definition, save.state);
  }
}

export const DefaultCampaignSaveMigrator = new CampaignSaveMigrator();
