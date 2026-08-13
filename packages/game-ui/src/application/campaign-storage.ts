import { createCampaignSave, DefaultCampaignSaveMigrator, type CampaignDefinition, type CampaignState } from '@empire/campaign-engine';

const keyFor = (definition: CampaignDefinition): string => `empire:campaign:${definition.id}:v${definition.version}`;

/**
 * A save that could not be read is not the same as no save at all.
 *
 * This used to be `catch { return null }`, so a migration that failed — or an
 * engine defect inside it — presented as "you have never played". The player
 * lost a campaign and was told nothing. The reason cannot always be classified,
 * but it can always be reported, which is what the sibling custom-level loader
 * already does.
 */
export interface LoadedCampaign {
  /** null when there was nothing stored, or nothing usable. */
  readonly state: CampaignState | null;
  /** Why a stored save went unused; null when there simply was none. */
  readonly rejected: string | null;
}

export function loadCampaignState(definition: CampaignDefinition): LoadedCampaign {
  let raw: string | null;
  try {
    raw = localStorage.getItem(keyFor(definition));
  } catch (error) {
    return { state: null, rejected: (error as Error).message };
  }
  if (!raw) return { state: null, rejected: null };

  try {
    const save = DefaultCampaignSaveMigrator.load(JSON.parse(raw), definition);
    // Battle-local action history is deliberately not reconstructed. Saves are
    // therefore only written between battles, and a legacy pending save is ignored.
    return { state: save.state.pendingBattle ? null : save.state, rejected: null };
  } catch (error) {
    return { state: null, rejected: (error as Error).message };
  }
}

export function saveCampaignState(definition: CampaignDefinition, state: CampaignState): void {
  if (state.pendingBattle) return;
  localStorage.setItem(keyFor(definition), JSON.stringify(createCampaignSave(definition, state)));
}

export function deleteCampaignState(definition: CampaignDefinition): void {
  localStorage.removeItem(keyFor(definition));
}
