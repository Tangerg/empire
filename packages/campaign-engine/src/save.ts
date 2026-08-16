import { SchemaMigrator, type SchemaMigration } from '@empire/battle-engine';
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

/**
 * Explicit, sequential schema migration, then the checks only a campaign can
 * make: same story, same version, same content packs, and a state its own
 * definition still recognises.
 */
export class CampaignSaveMigrator {
  private readonly ladder = new SchemaMigrator<CampaignSave>('campaign save', CAMPAIGN_SAVE_SCHEMA);

  register(fromSchema: number, migrate: SchemaMigration): this {
    this.ladder.register(fromSchema, migrate);
    return this;
  }

  load(raw: unknown, definition: CampaignDefinition): CampaignSave {
    const save = this.ladder.load(raw);
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
