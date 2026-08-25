import {
  CampaignActionError,
  createCampaignSave,
  loadCampaignSave,
  type CampaignDefinition,
  type CampaignState,
} from '@empire/campaign-engine';
import { errorMessage, StoredDocumentError } from '@empire/battle-engine';

const keyFor = (definition: CampaignDefinition): string => `empire:campaign:${definition.id}:v${definition.version}`;

/**
 * A save that could not be read is not the same as no save at all.
 *
 * This used to be `catch { return null }`, so a failed save validation — or an
 * engine defect inside it — presented as "you have never played". The player
 * lost a campaign and was told nothing. Syntax and stored-document failures are
 * reported here; a campaign-definition defect is deliberately propagated
 * instead of being blamed on the player's save.
 */
export interface LoadedCampaign {
  /** null when there was nothing stored, or nothing usable. */
  readonly state: CampaignState | null;
  /** Why a stored save went unused; null when there simply was none. */
  readonly rejected: string | null;
}

export function loadCampaignState(
  definition: CampaignDefinition,
): LoadedCampaign {
  let raw: string | null;
  try {
    raw = localStorage.getItem(keyFor(definition));
  } catch (error) {
    return { state: null, rejected: errorMessage(error) };
  }
  if (!raw) return { state: null, rejected: null };

  let document: unknown;
  try {
    document = JSON.parse(raw);
  } catch (error) {
    if (!(error instanceof SyntaxError)) throw error;
    return { state: null, rejected: error.message };
  }
  try {
    const save = loadCampaignSave(document, definition);
    // Battle-local action history is deliberately not reconstructed. A stored
    // pending battle is named as unusable, never confused with no save.
    return save.state.pendingBattle
      ? { state: null, rejected: '战役存档包含一场尚未接入恢复通道的进行中战斗' }
      : { state: save.state, rejected: null };
  } catch (error) {
    if (!(error instanceof StoredDocumentError)) throw error;
    return { state: null, rejected: error.message };
  }
}

export function saveCampaignState(definition: CampaignDefinition, state: CampaignState): void {
  if (state.pendingBattle) {
    throw new CampaignActionError('cannot save campaign state while a battle is pending');
  }
  localStorage.setItem(keyFor(definition), JSON.stringify(createCampaignSave(definition, state)));
}

export function deleteCampaignState(definition: CampaignDefinition): void {
  localStorage.removeItem(keyFor(definition));
}
